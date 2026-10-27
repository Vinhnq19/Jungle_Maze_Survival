"""TEL-01 balance metrics from merged events (definitions: TEL-01 section 2.5, Schema.vi.md section 8).

Everything is grouped by cfgKey and two cfgKeys are never mixed. A match takes the cfgKey of its
`match_start` (server envelope); without one, the first record of the match that has a `cfg`.
`perf_server` (no matchId) joins every match whose [match_start.utc, match_end.utc + windowSec] holds its utc.

These are measurement definitions, not balance targets: nothing here says pass/fail for TTK (M5).
"""
from __future__ import annotations

import math
import statistics
from collections import defaultdict

import tel_schema

TICK_HZ = 30
DEFAULT_GAP_SEC = 5.0
LOW_SAMPLE = 10          # G-5: groups with n < 10 are "low sample"
UNATTACHED = "(unattached)"
CAUSES = ("WEAPON", "ZONE", "HAZARD", "DISCONNECT", "OTHER")
PHASES = (-1, 0, 1, 2, 3, 4)
CC_KINDS = ("STUN", "ROOT", "REVERSE")


def median(values):
    return statistics.median(values) if values else None


def percentile(values, q):
    """Nearest rank: element ceil(q * n) (1-based) of the ascending list, like ServerMetrics.Percentile."""
    if not values:
        return None
    s = sorted(values)
    return s[max(1, math.ceil(q * len(s))) - 1]


def mean(values):
    return sum(values) / len(values) if values else None


def pct(part, whole):
    return 100.0 * part / whole if whole else None


# ----------------------------------------------------------------------------------------------
# Grouping
# ----------------------------------------------------------------------------------------------

class Dataset:
    """Merged events split per cfgKey and per match."""

    def __init__(self, events):
        self.events = events
        self.match_key: dict[str, str] = {}
        for e in events:
            if e["type"] == "match_start" and e["matchId"] not in self.match_key:
                self.match_key[e["matchId"]] = tel_schema.cfg_key(e)
        for e in sorted(events, key=lambda r: r["src"] != "server"):
            mid = e.get("matchId")
            if mid and mid not in self.match_key and e.get("cfg"):
                self.match_key[mid] = tel_schema.cfg_key(e)
        # by_match[mid][type] -> records (merge order: tick, src, seq)
        self.by_match: dict[str, dict[str, list]] = defaultdict(lambda: defaultdict(list))
        perf = []
        for e in events:
            if e["type"] == "perf_server" and not e.get("matchId"):
                perf.append(e)
            elif e.get("matchId"):
                self.by_match[e["matchId"]][e["type"]].append(e)
        self.groups: dict[str, list[str]] = defaultdict(list)
        for mid in sorted(self.by_match):
            self.groups[self.match_key.get(mid, "-/-/-")].append(mid)
        self.perf: dict[str, list] = defaultdict(list)
        windows = []
        for mid, recs in self.by_match.items():
            if recs.get("match_start") and recs.get("match_end"):
                windows.append((recs["match_start"][0]["utc"], recs["match_end"][0]["utc"], self.match_key[mid]))
        for p in perf:
            keys = sorted({k for s, e, k in windows if s <= p["utc"] <= e + p["windowSec"] * 1000})
            for k in keys or [UNATTACHED]:
                self.perf[k].append(p)

    def cfg_keys(self) -> list[str]:
        return sorted(set(self.groups) | set(self.perf))

    def records(self, key, rtype) -> list[dict]:
        return [r for mid in self.groups.get(key, []) for r in self.by_match[mid].get(rtype, [])]

    def spawns(self, key) -> int:
        return len(self.records(key, "player_spawn"))


# ----------------------------------------------------------------------------------------------
# Metrics
# ----------------------------------------------------------------------------------------------

def ttk_kills(ds: Dataset, key: str, gap_ticks: int) -> tuple[list[dict], int]:
    """Engagements ending in a WEAPON elimination with a killer. Returns (kills, eliminations without chain)."""
    kills, no_chain = [], 0
    for mid in ds.groups.get(key, []):
        recs = ds.by_match[mid]
        hits = defaultdict(list)
        for d in recs.get("damage", []):
            if d["cause"] == "WEAPON" and d["attacker"] is not None:
                hits[(d["victim"], d["attacker"])].append(d)
        for el in recs.get("elimination", []):
            if el["cause"] != "WEAPON" or el["killer"] is None:
                continue
            chain = []
            prev = el["tick"]
            for d in reversed([d for d in hits.get((el["victim"], el["killer"]), []) if d["tick"] <= el["tick"]]):
                if prev - d["tick"] > gap_ticks:
                    break
                chain.append(d)
                prev = d["tick"]
            if not chain:
                no_chain += 1
                continue
            first = chain[-1]
            weapon = el.get("weapon") or chain[0].get("weapon") or "(unknown)"
            kills.append({"matchId": mid, "victim": el["victim"], "killer": el["killer"], "weapon": weapon,
                          "ttk": (el["tick"] - first["tick"]) / TICK_HZ, "hits": len(chain),
                          "oneShot": len(chain) == 1 and first["tick"] == el["tick"]})
    return kills, no_chain


def ttk_by_weapon(kills) -> list[dict]:
    per = defaultdict(list)
    for k in kills:
        per[k["weapon"]].append(k)
    rows = []
    for w, ks in per.items():
        t = [k["ttk"] for k in ks]
        rows.append({"weapon": w, "kills": len(ks), "median": median(t), "p90": percentile(t, 0.9),
                     "hitsToKill": mean([k["hits"] for k in ks]), "oneShot": sum(k["oneShot"] for k in ks),
                     "lowSample": len(ks) < LOW_SAMPLE})
    rows.sort(key=lambda r: (-r["kills"], r["weapon"]))
    return rows


def death_causes(ds: Dataset, key: str) -> dict:
    elims = ds.records(key, "elimination")
    by_cause = {c: 0 for c in CAUSES}
    phases = {p: 0 for p in PHASES}
    for e in elims:
        by_cause[e["cause"]] += 1
        if e["cause"] == "ZONE":
            phases[e["zonePhase"]] += 1
    zone_hit = {(d["matchId"], d["victim"]) for d in ds.records(key, "damage") if d["cause"] == "ZONE"}
    spawns = ds.spawns(key)
    return {"eliminations": len(elims), "byCause": by_cause, "zoneByPhase": phases,
            "zoneRate": pct(by_cause["ZONE"], len(elims)), "zoneHitPlayers": len(zone_hit), "spawns": spawns,
            "zoneHitRate": pct(len(zone_hit), spawns)}


def _equipped_state(changes, until_tick):
    weapon, helmet, armor = "HAND", None, None
    for c in changes:
        if c["tick"] > until_tick:
            break
        on = c["action"] in ("EQUIP", "SWAP")
        if c["slot"] == "WEAPON":
            weapon = c["item"] if on else "HAND"
        elif c["slot"] == "HELMET":
            helmet = c["item"] if on else None
        else:
            armor = c["item"] if on else None
    return weapon, helmet, armor


def loot(ds: Dataset, key: str) -> dict:
    drops = ds.records(key, "loot_drop")
    pickups = ds.records(key, "item_pickup")
    spawns = ds.records(key, "player_spawn")
    by_container = defaultdict(int)
    items = 0
    for d in drops:
        by_container[d["container"]] += 1
        items += sum(i["qty"] for i in d["items"])
    by_from = defaultdict(int)
    for p in pickups:
        by_from[p["from"]] += 1
    changes = defaultdict(list)
    for c in ds.records(key, "equip_change"):
        changes[(c["matchId"], c["netId"])].append(c)
    first_equip, never = [], 0
    for s in spawns:
        t = None
        for c in changes.get((s["matchId"], s["netId"]), []):
            if c["action"] == "EQUIP" and ((c["slot"] == "WEAPON" and c["item"] != "HAND")
                                           or c["slot"] in ("HELMET", "ARMOR")):
                t = (c["tick"] - s["tick"]) / TICK_HZ
                break
        if t is None:
            never += 1
        else:
            first_equip.append(t)
    elims = ds.records(key, "elimination")
    starved = 0
    for e in elims:
        weapon, helmet, armor = _equipped_state(changes.get((e["matchId"], e["victim"]), []), e["tick"])
        if weapon == "HAND" and helmet is None and armor is None:
            starved += 1
    n_players = len(spawns)
    return {"matches": len(ds.groups.get(key, [])), "players": n_players, "drops": len(drops),
            "dropsByContainer": dict(sorted(by_container.items())), "itemsDropped": items,
            "pickups": len(pickups), "pickupsByFrom": dict(sorted(by_from.items())),
            "pickupsPerPlayer": len(pickups) / n_players if n_players else None,
            "firstEquipMedian": median(first_equip), "firstEquipN": len(first_equip), "neverEquipped": never,
            "starvedDeaths": starved, "eliminations": len(elims)}


def cc(ds: Dataset, key: str) -> dict:
    spans = ds.records(key, "cc_span")
    players = ds.spawns(key)
    rows = []
    groups = defaultdict(list)
    for s in spans:
        groups[(s["ccKind"], "ALL")].append(s)
        groups[(s["ccKind"], s["by"])].append(s)
    for kind in CC_KINDS:
        bys = sorted(b for k, b in groups if k == kind and b != "ALL")
        for by in (["ALL"] + bys) if groups.get((kind, "ALL")) else []:
            ss = groups[(kind, by)]
            secs = sum((s["endTick"] - s["startTick"]) / TICK_HZ for s in ss)
            rows.append({"ccKind": kind, "by": by, "n": len(ss), "seconds": secs,
                         "perPlayer": len(ss) / players if players else None,
                         "secondsPerPlayer": secs / players if players else None,
                         "cleansed": sum(s["end"] == "CLEANSED" for s in ss),
                         "cleansedPct": pct(sum(s["end"] == "CLEANSED" for s in ss), len(ss))})
    return {"spans": len(spans), "players": players, "rows": rows,
            "cleansedPct": pct(sum(s["end"] == "CLEANSED" for s in spans), len(spans))}


def item_use(ds: Dataset, key: str) -> list[dict]:
    """Per item: n, mean (actual - planned) ms over DONE uses, CANCELLED and INTERRUPTED rates."""
    per = defaultdict(list)
    for u in ds.records(key, "item_use"):
        per[u["item"]].append(u)
    rows = []
    for item in sorted(per):
        us = per[item]
        done = [u for u in us if u["result"] == "DONE"]
        deltas = [(u["endTick"] - u["startTick"]) * 1000.0 / TICK_HZ - u["plannedMs"] for u in done]
        rows.append({"item": item, "n": len(us), "done": len(done), "meanDeltaMs": mean(deltas),
                     "cancelledPct": pct(sum(u["result"] == "CANCELLED" for u in us), len(us)),
                     "interruptedPct": pct(sum(u["result"] == "INTERRUPTED" for u in us), len(us)),
                     "lowSample": len(us) < LOW_SAMPLE})
    return rows


def hide(ds: Dataset, key: str) -> dict:
    spans = ds.records(key, "hide_span")
    players = ds.spawns(key)
    hid = {(s["matchId"], s["netId"]) for s in spans}
    return {"spans": len(spans), "meanSeconds": mean([(s["endTick"] - s["startTick"]) / TICK_HZ for s in spans]),
            "players": players, "hidPlayers": len(hid), "hidPct": pct(len(hid), players),
            "diedHidden": sum(s["end"] == "DEATH" for s in spans),
            "attackedWhileHidden": sum(bool(s["attackedWhileHidden"]) for s in spans)}


def perf(ds: Dataset, key: str) -> dict:
    srv = ds.perf.get(key, [])
    cli = ds.records(key, "perf_client")
    out = {"serverWindows": len(srv), "clientWindows": len(cli)}
    if srv:
        out.update({
            "tickP95Median": median([p["tickP95Ms"] for p in srv]), "tickP95Max": max(p["tickP95Ms"] for p in srv),
            "tickP99Max": max(p["tickP99Ms"] for p in srv), "ticksPerSecondMin": min(p["ticksPerSecond"] for p in srv),
            "droppedTicks": sum(p["droppedTicks"] for p in srv), "tickOverruns": sum(p["tickOverruns"] for p in srv),
            "cpuMedian": median([p["cpuOneCorePercent"] for p in srv]),
            "workingSetMax": max(p["workingSetMiB"] for p in srv)})
    if cli:
        secs = sum(c["windowSec"] for c in cli)
        rtt = [c["rttP95"] for c in cli if c["rttP95"] is not None]
        jit = [c["jitterMs"] for c in cli if c["jitterMs"] is not None]
        loss = [c["lossPct"] for c in cli if c["lossPct"] is not None]
        transports = defaultdict(int)
        for c in cli:
            transports[c["transport"] or "-"] += 1
        out.update({
            "fpsAvg": sum(c["frames"] for c in cli) / secs if secs else None,
            "fps1LowMedian": median([c["fps1Low"] for c in cli]), "fps1LowMin": min(c["fps1Low"] for c in cli),
            "frameMsP95Median": median([c["frameMsP95"] for c in cli]),
            "rttP95Median": median(rtt), "jitterMedian": median(jit), "lossPctMean": mean(loss),
            "transports": dict(sorted(transports.items()))})
    return out


def coverage(events, schema: tel_schema.Schema) -> list[dict]:
    """real/fake count per type and status: real, fake-only, mixed, none (AC22, AC39)."""
    counts = {t: [0, 0] for t in schema.type_names()}
    for e in events:
        counts[e["type"]][1 if e.get("fake") is True else 0] += 1
    rows = []
    for t in schema.type_names():
        real, fake = counts[t]
        status = "mixed" if real and fake else "real" if real else "fake-only" if fake else "none"
        rows.append({"n": schema.types[t]["n"], "type": t, "source": schema.types[t]["source"], "real": real,
                     "fake": fake, "fakePct": pct(fake, real + fake), "status": status})
    return rows


def compute(events, schema: tel_schema.Schema, gap_sec: float = DEFAULT_GAP_SEC) -> dict:
    """All metrics for all cfgKeys: {"cfgKeys": {key: {...}}, "coverage": [...]}."""
    ds = Dataset(events)
    gap_ticks = int(round(gap_sec * TICK_HZ))
    out = {}
    for key in ds.cfg_keys():
        kills, no_chain = ttk_kills(ds, key, gap_ticks)
        out[key] = {
            "matches": len(ds.groups.get(key, [])), "records": sum(len(v) for mid in ds.groups.get(key, [])
                                                                    for v in ds.by_match[mid].values()),
            "fake": sum(1 for mid in ds.groups.get(key, []) for v in ds.by_match[mid].values() for r in v
                        if r.get("fake") is True),
            "ttk": ttk_by_weapon(kills), "kills": kills, "killsWithoutChain": no_chain,
            "causes": death_causes(ds, key), "loot": loot(ds, key), "cc": cc(ds, key),
            "itemUse": item_use(ds, key), "hide": hide(ds, key), "perf": perf(ds, key),
        }
    return {"cfgKeys": out, "coverage": coverage(events, schema), "gapSec": gap_sec}
