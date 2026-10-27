"""TEL-01 deterministic fake event generator (ticket 2.10, plan 4.4).

Builds N fake 10-player matches with all 18 record types, written the way the real writers would
write them (one JSONL file per match and source, `_header` first, `_trailer` last). Every record
carries `"fake": true`. The same seed and options give byte-identical files: nothing reads the
clock, `utc` is a fixed epoch plus tick x 33 ms, and no set/dict of strings is iterated in hash order.

Fault injection (counts go to inject_manifest.json so tests compare exact numbers):
  --reconnect        one client drops at T and resumes at T+6 s (inside the 15 s grace) in match 0
  --semantic-dup N   N records of 'once' types reported a second time with a new seq
  --dup PCT          PCT % of record lines duplicated byte for byte (technical duplicates)
  --conflict N       N records written twice under the same (matchId, src, wid, seq) with other content
  --gap K            K record lines removed after writing (seq holes)
  --reorder          record lines shuffled inside every file
"""
from __future__ import annotations

import hashlib
import heapq
import json
import math
import random
import uuid
from pathlib import Path

import tel_schema

TICK_HZ = 30
BASE_UTC = 1793059200000          # 2026-10-27T00:00:00Z, fixed so output never depends on the clock
MS_PER_TICK = 33                  # plan 4.4: utc = base + tick x 33 ms
MATCH_TICK_STRIDE = 16000         # ticks reserved per match (lobby + 420 s + ending)
LOBBY_TICKS = 600                 # Waiting/Loading before InGame
MATCH_SECONDS = 420
ENDING_TICKS = 300
GRACE_SECONDS = 15
PERF_WINDOW_TICKS = 150          # perf_client: one sample per 5 s window
GAP_TICKS_HINT = 150
CFG_VERSION = "1.0.0"
ROOM_ID = "room-1"
BUILD = "dev"
B_BOOSTED = ("KNIFE", "AXE", "CROSSBOW")   # cfg B: damage x 1.5 on these, so TTK differs on purpose
B_FACTOR = 1.5
STUN_WEAPONS = ("MACE", "BANANA", "SEED", "SAHUR_BAT")
LOOT_WEAPONS = ("TREE_BRANCH", "KNIFE", "BONE", "AXE", "MACE", "RUBBER_GUN", "SCEPTER", "CROSSBOW",
                "SHOTGUN", "BANANA", "METEORITE_FRAGMENT", "CRYSTAL", "SEED", "ROPE", "BELL", "SAHUR_BAT")
HEALS = ("GOD_LEAF", "SPIRIT_TEAR", "MERMAID_TAIL", "PHOENIX_FEATHERS")
HELMETS = ("WOODEN_HELMET", "SPIKE_HELMET", "MAGIC_HELMET")
ARMORS = ("WOODEN_ARMOR", "ARMOR", "MAGIC_ARMOR")

# Display names and the room code exist only inside the generator. The writer never serialises them;
# AC4 tests scan every output byte for these strings and expect zero hits.
FAKE_DISPLAY_NAMES = ("Tester Alpha", "Tester Bravo", "Tester Charlie", "Tester Delta", "Tester Echo",
                      "Tester Foxtrot", "Tester Golf", "Tester Hotel", "Tester India", "Tester Juliet")
FAKE_ROOM_CODE = "QX7K2M"


def sha256_hex(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def cfg_hash_for(cfg: str) -> str:
    return sha256_hex(f"tel-fake-cfg-{cfg}")


def bal_hash_for(cfg: str) -> str:
    return sha256_hex(f"tel-fake-bal-{cfg}")


def utc_of(tick: int, skew_ms: int = 0) -> int:
    return BASE_UTC + tick * MS_PER_TICK + skew_ms


def _hex8(rng: random.Random) -> str:
    return f"{rng.getrandbits(32):08x}"


def _load_balance(schema: tel_schema.Schema):
    def load(name):
        src = Path(schema.raw["idSets"][name]["source"])
        data = json.loads((tel_schema.REPO_ROOT / src).read_text(encoding="utf-8"))
        return {item["id"]: item for item in data}
    return load("weapon"), load("equipment")


def _load_zone():
    path = tel_schema.REPO_ROOT / "Assets" / "StreamingAssets" / "JungleMaze" / "runtime" / "zone_schedule.json"
    return json.loads(path.read_text(encoding="utf-8"))


# ----------------------------------------------------------------------------------------------
# Match simulation
# ----------------------------------------------------------------------------------------------

class _Player:
    def __init__(self, net_id, x, y):
        self.net_id = net_id
        self.x, self.y = x, y
        self.hp = 100
        self.alive = True
        self.weapon = "HAND"
        self.helmet = None
        self.helmet_dur = 0
        self.armor = None
        self.armor_dur = 0
        self.heals: list[str] = []
        self.ammo = 0
        self.death_tick = None
        self.dropped = False
        self.left = False
        self.last_hit_tick = -10_000
        self.using = None          # [item, startTick, endTick, plannedMs, hpBefore, result]
        self.cc = None             # [kind, by, source, startTick, endTick, plannedMs, end]
        self.lingerer = False
        self.starved = False
        self.protected_until = 0   # session-scenario player is kept out of fights until the event


class _Zone:
    """Zone schedule on the match timeline (phase 0..4 = interval index - 1)."""

    def __init__(self, sched, t0, rng):
        self.t0 = t0
        self.warn = sched["warningDurationSeconds"]
        self.phases = []
        cx, cy = sched["initialCenter"]["x"], sched["initialCenter"]["y"]
        for i, iv in enumerate(sched["intervals"]):
            ncx = round(min(40.0, max(10.0, cx + rng.uniform(-2.0, 2.0))), 3)
            ncy = round(min(40.0, max(10.0, cy + rng.uniform(-2.0, 2.0))), 3)
            self.phases.append({
                "phase": i, "cx": ncx, "cy": ncy,
                "warnTick": t0 + int((iv["startSecond"] - self.warn) * TICK_HZ),
                "startTick": t0 + int(iv["startSecond"] * TICK_HZ),
                "endTick": t0 + int(iv["endSecond"] * TICK_HZ),
                "r0": float(iv["startRadius"]), "r1": float(iv["endRadius"]),
            })
            cx, cy = ncx, ncy
        self.init_center = (sched["initialCenter"]["x"], sched["initialCenter"]["y"])
        self.init_r = float(sched["initialRadius"])
        self.dps = sched["damagePerSecond"]

    def state(self, tick):
        """(cx, cy, r) at tick."""
        cx, cy = self.init_center
        r = self.init_r
        for p in self.phases:
            if tick < p["warnTick"]:
                break
            cx, cy = p["cx"], p["cy"]
            if tick < p["startTick"]:
                r = p["r0"]
            elif tick < p["endTick"]:
                f = (tick - p["startTick"]) / max(1, p["endTick"] - p["startTick"])
                r = p["r0"] + (p["r1"] - p["r0"]) * f
            else:
                r = p["r1"]
        return cx, cy, r

    def phase_at(self, tick):
        ph = -1
        for p in self.phases:
            if tick >= p["warnTick"]:
                ph = p["phase"]
        return ph

    def outside(self, tick, x, y):
        cx, cy, r = self.state(tick)
        if r <= 0:
            return True
        return math.hypot(x - cx, y - cy) > r


class MatchSim:
    """One fake match. Produces server records and per-client perf_client records (envelope-less)."""

    def __init__(self, seed, cfg, index, players, sample_hz, weapons, equips, zone_sched):
        self.rng = random.Random(f"tel-fake:{seed}:{cfg}:m{index}")
        self.cfg = cfg
        self.index = index
        self.n_players = players
        self.sample_every = max(1, int(round(TICK_HZ / sample_hz)))
        self.weapons = weapons
        self.equips = equips
        self.match_id = str(uuid.UUID(int=self.rng.getrandbits(128), version=4))
        self.t_lobby = index * MATCH_TICK_STRIDE + 1000
        self.t0 = self.t_lobby + LOBBY_TICKS
        self.zone = _Zone(zone_sched, self.t0, self.rng)
        self.server: list[dict] = []
        self.client: dict[int, list[dict]] = {}
        self.client_stop: dict[int, int] = {}
        self._order = 0
        self._heap: list = []
        self.end_tick = None
        self.engaged: set[int] = set()   # ints only: iteration order is deterministic
        # internal roster with PII-like data that must never reach a record (AC4)
        self.roster_names = {i: FAKE_DISPLAY_NAMES[i % len(FAKE_DISPLAY_NAMES)] for i in range(players)}
        self.session_ids = {i: f"{self.rng.getrandbits(64):016x}" for i in range(players)}

    # -- record helpers -----------------------------------------------------------------------
    def emit(self, tick, rtype, **fields):
        self._order += 1
        rec = {"_t": tick, "_o": self._order, "type": rtype}
        rec.update(fields)
        self.server.append(rec)
        return rec

    def schedule(self, tick, kind, *payload):
        self._order += 1
        heapq.heappush(self._heap, (tick, self._order, kind, payload))

    def weapon_damage(self, wid):
        w = self.weapons[wid]
        dmg = w.get("damage", 0) * w.get("pellets", 1)
        if self.cfg == "B" and wid in B_BOOSTED:
            dmg = dmg * B_FACTOR
        return int(round(dmg))

    def weapon_interval(self, wid):
        w = self.weapons[wid]
        if "fireRatePerSecond" in w:
            secs = 1.0 / w["fireRatePerSecond"]
        else:
            secs = w.get("cooldownSeconds", 1.5)
        return max(12, int(round(secs * TICK_HZ)))

    @staticmethod
    def attack_kind(category):
        return {"Melee": "MELEE", "Ranged": "SHOT", "Thrown": "THROW"}.get(category, "SPECIAL")

    def alive_ids(self):
        return [p.net_id for p in self.players if p.alive]

    # -- simulation -----------------------------------------------------------------------------
    def run(self):
        rng = self.rng
        self.players = [_Player(i, round(rng.uniform(5, 45), 3), round(rng.uniform(5, 45), 3))
                        for i in range(self.n_players)]
        order = list(range(self.n_players))
        rng.shuffle(order)
        for i in order[:3]:
            self.players[i].lingerer = True
        self.players[order[2]].starved = True
        t0 = self.t0
        self.emit(t0, "match_start", profile="main", players=self.n_players, minPlayers=6,
                  seedHash=sha256_hex(self.match_id)[:16])
        for p in self.players:
            sess = sha256_hex("tel1:" + self.session_ids[p.net_id])[:16]
            self.emit(t0, "player_spawn", netId=p.net_id, sess=sess, x=p.x, y=p.y, loadout=[])
        for p in self.zone.phases:
            self.schedule(p["warnTick"], "zone", p["phase"], "WARNING")
            self.schedule(p["startTick"], "zone", p["phase"], "SHRINK_START")
            self.schedule(p["endTick"], "zone", p["phase"], "SHRINK_END")
        last = self.zone.phases[-1]
        self.schedule(last["endTick"], "zone", last["phase"], "FINAL")
        for tick in range(t0 + self.sample_every, t0 + MATCH_SECONDS * TICK_HZ + 1, self.sample_every):
            self.schedule(tick, "sample")
        for tick in range(t0 + TICK_HZ, t0 + MATCH_SECONDS * TICK_HZ + 1, TICK_HZ):
            self.schedule(tick, "zonedmg")
        for tick in range(t0 + PERF_WINDOW_TICKS, t0 + MATCH_SECONDS * TICK_HZ + 1, PERF_WINDOW_TICKS):
            self.schedule(tick, "perf")
        for p in self.players:
            if p.starved:
                continue
            for _ in range(rng.randint(2, 4)):
                self.schedule(t0 + rng.randint(150, 3300), "loot", p.net_id)
            if rng.random() < 0.5:
                self.schedule(t0 + rng.randint(600, 6000), "floor", p.net_id)
        self.schedule(t0 + 210 * TICK_HZ, "airdrop")
        self.schedule(t0 + rng.randint(40, 70) * TICK_HZ, "engage")
        self.schedule(t0 + rng.randint(60, 300) * TICK_HZ, "lava", order[3])
        # session scenario: even matches = disconnect past grace, odd = player leaves (cause OTHER)
        sess_player = order[4]
        self.players[sess_player].protected_until = t0 + 125 * TICK_HZ
        self.players[sess_player].lingerer = False
        self.schedule(t0 + rng.randint(40, 140) * TICK_HZ, "swamp")
        if self.index % 2 == 0:
            self.schedule(t0 + 100 * TICK_HZ, "drop", sess_player)
        else:
            self.schedule(t0 + 120 * TICK_HZ, "leave", sess_player)
        self.schedule(t0 + MATCH_SECONDS * TICK_HZ, "timeout")

        while self._heap and self.end_tick is None:
            tick, _, kind, payload = heapq.heappop(self._heap)
            getattr(self, "_on_" + kind)(tick, *payload)
        self._finish()
        return self

    # zone ---------------------------------------------------------------------------------------
    def _on_zone(self, tick, phase, state):
        p = self.zone.phases[phase]
        if state in ("WARNING", "SHRINK_START"):
            r, target = p["r0"], p["r1"]
        else:
            r = target = p["r1"]
        self.emit(tick, "zone_phase", phase=phase, state=state, cx=p["cx"], cy=p["cy"], r=r, targetR=target)

    def _on_zonedmg(self, tick):
        hits = []
        for p in self.players:
            if p.alive and not p.dropped and self.zone.outside(tick, p.x, p.y):
                hits.append(p)
        for p in hits:
            self._damage(tick, p, None, "ZONE", None, "ZONE", self.zone.dps, armor=False)
        self._check_end(tick)

    def _on_swamp(self, tick):
        cands = [i for i in self.alive_ids() if not self.players[i].dropped and self.players[i].cc is None]
        if cands:
            p = self.players[cands[self.rng.randrange(len(cands))]]
            p.cc = ["ROOT", "SWAMP", None, tick, tick + TICK_HZ, 1000, "EXPIRED"]
            self.schedule(tick + TICK_HZ, "cc_end", p.net_id, tick)

    def _on_lava(self, tick, net_id):
        p = self.players[net_id]
        if p.alive and not p.dropped:
            self._damage(tick, p, None, "HAZARD", None, "LAVA", 10, armor=False)
            self._check_end(tick)

    # movement, sampling, healing, perf ------------------------------------------------------------
    def _on_sample(self, tick):
        rng = self.rng
        cx, cy, r = self.zone.state(tick)
        for p in self.players:
            if not p.alive:
                continue
            if not p.dropped:
                pull = 0.0 if p.lingerer else 0.6
                dx, dy = cx - p.x, cy - p.y
                dist = math.hypot(dx, dy) or 1.0
                if dist > max(1.0, r * 0.6):
                    p.x += dx / dist * pull * 3.0
                    p.y += dy / dist * pull * 3.0
                p.x = round(min(49.5, max(0.5, p.x + rng.uniform(-1.5, 1.5))), 3)
                p.y = round(min(49.5, max(0.5, p.y + rng.uniform(-1.5, 1.5))), 3)
            self.emit(tick, "position_sample", netId=p.net_id, x=p.x, y=p.y, hp=p.hp, weapon=p.weapon,
                      inZone=not self.zone.outside(tick, p.x, p.y))
            if (p.using is None and not p.dropped and p.heals and p.hp < 70
                    and tick - p.last_hit_tick > 90):
                self._start_use(tick, p)

    def _start_use(self, tick, p):
        rng = self.rng
        item = p.heals.pop(0)
        eq = self.equips[item]
        planned = int(round(eq["useSeconds"] * 1000))
        dur = int(round(eq["useSeconds"] * TICK_HZ)) + rng.randint(0, 3)
        result = "CANCELLED" if rng.random() < 0.12 else "DONE"
        end = tick + (dur // 2 if result == "CANCELLED" else dur)
        p.using = [item, tick, end, planned, p.hp, result]
        self.schedule(end, "use_end", p.net_id, tick)

    def _on_use_end(self, tick, net_id, start):
        p = self.players[net_id]
        if p.using is None or p.using[1] != start:
            return  # already interrupted or closed
        item, start, end, planned, hp_before, result = p.using
        if result == "DONE" and p.alive:
            p.hp = min(100, p.hp + self.equips[item]["healAmount"])
        self._close_use(tick, p, result)

    def _close_use(self, tick, p, result):
        item, start, _, planned, hp_before, _ = p.using
        p.using = None
        self.emit(tick, "item_use", netId=p.net_id, item=item, startTick=start, endTick=tick,
                  plannedMs=planned, result=result, hpBefore=hp_before, hpAfter=p.hp)

    def _on_perf(self, tick):
        rng = self.rng
        alive = len(self.alive_ids())
        for p in self.players:
            stop = self.client_stop.get(p.net_id)
            if stop is not None and tick > stop:
                continue
            fps = rng.uniform(52.0, 64.0) - (0.3 * alive)
            frames = int(round(fps * 5))
            fps_avg = round(frames / 5.0, 3)
            low = round(fps_avg * rng.uniform(0.62, 0.8), 3)
            loss = None if rng.random() < 0.1 else round(rng.uniform(0.0, 2.5), 3)
            rec = {"_t": tick, "_o": self._order, "type": "perf_client", "netId": p.net_id, "windowSec": 5.0,
                   "frames": frames, "fpsAvg": fps_avg, "fps1Low": low,
                   "frameMsP95": round(1000.0 / low * 0.9, 3), "frameMsMax": round(1000.0 / low * 1.6, 3),
                   "transport": "WSS" if p.net_id == self.n_players - 1 else "UDP",
                   "rttP50": round(rng.uniform(18.0, 40.0), 3), "rttP95": round(rng.uniform(40.0, 75.0), 3),
                   "jitterMs": round(rng.uniform(1.0, 6.0), 3), "lossPct": loss, "entities": alive}
            self._order += 1
            self.client.setdefault(p.net_id, []).append(rec)

    # loot ---------------------------------------------------------------------------------------------
    def _pick_weapon(self):
        return LOOT_WEAPONS[self.rng.randrange(len(LOOT_WEAPONS))]

    def _on_loot(self, tick, net_id):
        p = self.players[net_id]
        if not p.alive or p.dropped:
            return
        rng = self.rng
        if rng.random() < 0.55:
            container = "CHEST_WOODEN"
            roll = rng.random()
            first = (self._pick_weapon() if roll < 0.5 else
                     HELMETS[rng.randrange(3)] if roll < 0.75 else ARMORS[rng.randrange(3)])
            items = [{"id": first, "qty": 1}]
            if rng.random() < 0.5:
                items.append({"id": HEALS[rng.randrange(len(HEALS))], "qty": 1})
        else:
            container = "POT_CLAY"
            if rng.random() < 0.6:
                items = [{"id": HEALS[rng.randrange(len(HEALS))], "qty": rng.randint(1, 2)}]
            else:
                items = [{"id": "AMMO", "qty": rng.randint(10, 30)}]
        lx, ly = round(p.x + rng.uniform(-1, 1), 3), round(p.y + rng.uniform(-1, 1), 3)
        self.emit(tick, "loot_drop", container=container, by=p.net_id, items=items, x=lx, y=ly)
        for k, it in enumerate(items):
            self._pickup(tick + 6 + 3 * k, p, it["id"], it["qty"], "CONTAINER")

    def _on_floor(self, tick, net_id):
        p = self.players[net_id]
        if p.alive and not p.dropped:
            self._pickup(tick, p, HEALS[self.rng.randrange(len(HEALS))], 1, "FLOOR")

    def _on_airdrop(self, tick):
        rng = self.rng
        cx, cy, _ = self.zone.state(tick)
        items = [{"id": "MAGIC_ARMOR", "qty": 1}, {"id": "UNICORN_BLOOD", "qty": 1}]
        self.emit(tick, "loot_drop", container="AIRDROP_CRATE", by=None, items=items, x=cx, y=cy)
        alive = [i for i in self.alive_ids() if not self.players[i].dropped]
        if alive:
            p = self.players[alive[rng.randrange(len(alive))]]
            for k, it in enumerate(items):
                self._pickup(tick + 300 + 3 * k, p, it["id"], it["qty"], "AIRDROP")

    def _pickup(self, tick, p, item, qty, src):
        self.emit(tick, "item_pickup", netId=p.net_id, item=item, qty=qty, from_=src, x=p.x, y=p.y)
        if item == "AMMO":
            p.ammo += qty
        elif item in self.weapons:
            if p.weapon == "HAND":
                self.emit(tick, "equip_change", netId=p.net_id, action="EQUIP", slot="WEAPON", item=item, from_="HAND")
                p.weapon = item
            elif self.rng.random() < 0.4:
                self.emit(tick, "equip_change", netId=p.net_id, action="SWAP", slot="WEAPON", item=item, from_=p.weapon)
                p.weapon = item
            if self.weapons[item]["category"] == "Ranged":
                p.ammo += self.weapons[item].get("magazineSize", 0)
        elif item in HELMETS or item in ARMORS:
            slot = "HELMET" if item in HELMETS else "ARMOR"
            cur = p.helmet if slot == "HELMET" else p.armor
            if cur is None or self.rng.random() < 0.5:
                self.emit(tick, "equip_change", netId=p.net_id, action="EQUIP" if cur is None else "SWAP",
                          slot=slot, item=item, from_=cur)
                dur = self.equips[item]["durabilityPoints"]
                if slot == "HELMET":
                    p.helmet, p.helmet_dur = item, dur
                else:
                    p.armor, p.armor_dur = item, dur
        elif self.equips.get(item, {}).get("healAmount", 0) > 0:
            p.heals.append(item)

    # fights -------------------------------------------------------------------------------------------
    def _on_engage(self, tick):
        rng = self.rng
        self.schedule(tick + rng.randint(14, 34) * TICK_HZ, "engage")
        free = [i for i in self.alive_ids()
                if i not in self.engaged and not self.players[i].dropped and self.players[i].protected_until <= tick]
        if len(free) < 2:
            return
        armed = [i for i in free if self.players[i].weapon != "HAND"]
        k = (armed or free)[rng.randrange(len(armed or free))]
        others = [i for i in free if i != k]
        v = others[rng.randrange(len(others))]
        self.engaged.update((k, v))
        eng = {"victim": v, "attackers": [k], "budget": {k: rng.randint(2, 9)}, "open": True}
        self.schedule(tick, "hit", eng, k, v)
        if rng.random() < 0.45:
            eng["budget"][v] = rng.randint(2, 6)
            self.schedule(tick + rng.randint(8, 25), "hit", eng, v, k)
        rest = [i for i in free if i not in (k, v)]
        if rest and rng.random() < 0.15:
            third = rest[rng.randrange(len(rest))]
            self.engaged.add(third)
            eng["attackers"].append(third)
            eng["budget"][third] = rng.randint(2, 6)
            self.schedule(tick + rng.randint(20, 60), "hit", eng, third, v)

    def _end_engagement(self, eng):
        if eng["open"]:
            eng["open"] = False
            for i in [eng["victim"]] + eng["attackers"]:
                self.engaged.discard(i)

    def _on_hit(self, tick, eng, a_id, t_id):
        rng = self.rng
        a, t = self.players[a_id], self.players[t_id]
        if not eng["open"] or not a.alive or not t.alive or a.dropped or t.dropped:
            self._end_engagement(eng)
            return
        w = self.weapons[a.weapon]
        kind = self.attack_kind(w["category"])
        ammo = None
        if kind == "SHOT":
            a.ammo = max(0, a.ammo - 1)
            ammo = a.ammo
        self.emit(tick, "attack", netId=a.net_id, weapon=a.weapon, kind=kind, x=a.x, y=a.y, ammo=ammo)
        if rng.random() < 0.66:
            self._damage(tick, t, a, "WEAPON", a.weapon, None, self.weapon_damage(a.weapon), armor=True)
            if t.alive:
                self._maybe_cc(tick, a, t)
        if not t.alive:
            self._end_engagement(eng)
            self._check_end(tick)
            return
        eng["budget"][a_id] -= 1
        if eng["budget"][a_id] > 0:
            nxt = self.weapon_interval(a.weapon) + rng.randint(0, 20)
            if rng.random() < 0.08:
                nxt += rng.randint(GAP_TICKS_HINT + 10, GAP_TICKS_HINT + 70)   # pause longer than gapSec
            self.schedule(tick + nxt, "hit", eng, a_id, t_id)
        elif all(b <= 0 for b in eng["budget"].values()):
            self._end_engagement(eng)

    def _maybe_cc(self, tick, a, t):
        rng = self.rng
        w = self.weapons[a.weapon]
        if a.weapon in STUN_WEAPONS:
            kind, secs = "STUN", w.get("stunSeconds", 1.0)
        elif a.weapon == "ROPE":
            kind, secs = "ROOT", 1.5
        elif a.weapon == "BELL":
            kind, secs = "REVERSE", 2.0
        else:
            return
        if t.cc is not None or rng.random() > 0.6:
            return
        planned = int(round(secs * 1000))
        span = int(round(secs * TICK_HZ))
        if rng.random() < 0.2:
            end, how = tick + max(1, span // 2), "CLEANSED"
        else:
            end, how = tick + span, "EXPIRED"
        t.cc = [kind, a.weapon, a.net_id, tick, end, planned, how]
        self.schedule(end, "cc_end", t.net_id, tick)

    def _on_cc_end(self, tick, net_id, start):
        p = self.players[net_id]
        if p.cc is not None and p.cc[3] == start:
            self._close_cc(tick, p, p.cc[6])

    def _close_cc(self, tick, p, how):
        kind, by, src, start, _, planned, _ = p.cc
        p.cc = None
        self.emit(tick, "cc_span", victim=p.net_id, source=src, by=by, ccKind=kind, startTick=start,
                  endTick=tick, plannedMs=planned, end=how)

    def _damage(self, tick, p, attacker, cause, weapon, hazard, raw, armor):
        absorbed = 0
        if armor:
            if p.helmet:
                part = min(p.helmet_dur, int(round(raw * self.equips[p.helmet]["damageReductionPct"])))
                absorbed += part
                p.helmet_dur -= part
            if p.armor:
                part = min(p.armor_dur, int(round(raw * self.equips[p.armor]["damageReductionPct"])))
                absorbed += part
                p.armor_dur -= part
            absorbed = min(absorbed, int(raw * 0.7))
        applied = min(p.hp, raw - absorbed)
        p.hp -= applied
        p.last_hit_tick = tick
        self.emit(tick, "damage", victim=p.net_id, attacker=attacker.net_id if attacker else None, cause=cause,
                  weapon=weapon, hazard=hazard, raw=raw, applied=applied, absorbed=absorbed if armor else None,
                  hpAfter=p.hp, helmetDur=p.helmet_dur if p.helmet else None,
                  armorDur=p.armor_dur if p.armor else None)
        if p.helmet and p.helmet_dur <= 0:
            self.emit(tick, "equip_change", netId=p.net_id, action="BREAK", slot="HELMET", item=p.helmet, from_=None)
            p.helmet = None
        if p.armor and p.armor_dur <= 0:
            self.emit(tick, "equip_change", netId=p.net_id, action="BREAK", slot="ARMOR", item=p.armor, from_=None)
            p.armor = None
        if p.using is not None and p.hp > 0:
            self._close_use(tick, p, "INTERRUPTED")
        if p.hp <= 0:
            self._eliminate(tick, p, cause, attacker, weapon, hazard)

    def _eliminate(self, tick, p, cause, killer, weapon, hazard):
        if p.using is not None:
            self._close_use(tick, p, "INTERRUPTED")
        if p.cc is not None:
            self._close_cc(tick, p, "DEATH")
        rank = len(self.alive_ids())
        p.alive = False
        p.death_tick = tick
        self.engaged.discard(p.net_id)
        self.emit(tick, "elimination", victim=p.net_id, killer=killer.net_id if killer else None, cause=cause,
                  weapon=weapon, hazard=hazard, rank=rank, survTicks=tick - self.t0, x=p.x, y=p.y,
                  zonePhase=self.zone.phase_at(tick), hidden=False)
        items = []
        if p.weapon != "HAND":
            items.append({"id": p.weapon, "qty": 1})
        for it in (p.helmet, p.armor):
            if it:
                items.append({"id": it, "qty": 1})
        for h in p.heals[:4]:
            items.append({"id": h, "qty": 1})
        if items:
            self.emit(tick, "loot_drop", container="CORPSE", by=None, items=items[:8], x=p.x, y=p.y)
            if killer is not None and killer.alive and self.rng.random() < 0.5:
                self._pickup(tick + 15, killer, items[0]["id"], 1, "CORPSE")

    # sessions -------------------------------------------------------------------------------------------
    def _on_drop(self, tick, net_id):
        p = self.players[net_id]
        if not p.alive:
            return
        p.dropped = True
        self.client_stop[net_id] = tick
        self.emit(tick, "session_state", netId=net_id, kind="DROPPED", transport="UDP", graceMs=GRACE_SECONDS * 1000)
        self.schedule(tick + GRACE_SECONDS * TICK_HZ, "grace", net_id)

    def _on_grace(self, tick, net_id):
        p = self.players[net_id]
        if not p.alive:
            return
        self.emit(tick, "session_state", netId=net_id, kind="GRACE_EXPIRED", transport=None, graceMs=0)
        self._damage(tick, p, None, "DISCONNECT", None, None, p.hp, armor=False)
        self._check_end(tick)

    def _on_leave(self, tick, net_id):
        p = self.players[net_id]
        if not p.alive:
            return
        p.left = True
        self.client_stop[net_id] = tick
        self.emit(tick, "session_state", netId=net_id, kind="LEFT", transport=None, graceMs=None)
        self._damage(tick, p, None, "OTHER", None, None, p.hp, armor=False)
        self._check_end(tick)

    # end ---------------------------------------------------------------------------------------------------
    def _check_end(self, tick):
        if self.end_tick is not None:
            return
        alive = self.alive_ids()
        if len(alive) == 1:
            self._end(tick, "LastAlive", alive[0], 1)
        elif not alive:
            self._end(tick, "Tie", None, 0)

    def _on_timeout(self, tick):
        if self.end_tick is None:
            self._end(tick, "Timeout", None, len(self.alive_ids()))

    def _end(self, tick, reason, winner, alive):
        self.end_tick = tick
        for p in self.players:
            if p.using is not None:
                self._close_use(tick, p, "CANCELLED")
            if p.cc is not None:
                self._close_cc(tick, p, "MATCH_END")
        self.emit(tick, "match_end", reason=reason, winnerNetId=winner, durTicks=tick - self.t0, alive=alive)
        self.result = (reason, winner)

    def _finish(self):
        rng = self.rng
        end = self.end_tick
        # hide spans: decided after the fight so they never change fight outcomes
        for p in self.players:
            stop = p.death_tick if p.death_tick is not None else end
            for _ in range(rng.randint(0, 3)):
                start = self.t0 + rng.randint(120, max(121, stop - self.t0 - 60))
                if start >= stop:
                    continue
                span_end = start + rng.randint(90, 600)
                how = "EXIT"
                if span_end >= stop:
                    span_end = stop
                    how = "DEATH" if p.death_tick is not None else "MATCH_END"
                hit = any(r["type"] == "damage" and r["victim"] == p.net_id and start <= r["_t"] <= span_end
                          for r in self.server)
                self.emit(span_end, "hide_span", netId=p.net_id, startTick=start, endTick=span_end,
                          bush=f"BUSH_{rng.randint(1, 45):02d}", x=p.x, y=p.y, end=how, attackedWhileHidden=hit)
                if how == "DEATH":
                    for r in self.server:
                        if r["type"] == "elimination" and r["victim"] == p.net_id:
                            r["hidden"] = True
                    break
        # replay choices after Ending
        for p in self.players:
            if p.dropped or p.left:
                self.emit(end + ENDING_TICKS, "replay_choice", netId=p.net_id, choice="DISCONNECTED", afterMs=None)
            elif rng.random() < 0.85:
                after = rng.randint(800, 9000)
                self.emit(end + after * TICK_HZ // 1000, "replay_choice", netId=p.net_id, choice="REPLAY", afterMs=after)
            else:
                self.emit(end + ENDING_TICKS, "replay_choice", netId=p.net_id, choice="TIMEOUT", afterMs=None)
        for p in self.players:
            self.client_stop.setdefault(p.net_id, end)
        # perf windows recorded after the end are dropped (client leaves InGame at match_end)
        for nid in list(self.client):
            self.client[nid] = [r for r in self.client[nid] if r["_t"] <= self.client_stop[nid]]


# ----------------------------------------------------------------------------------------------
# Writing
# ----------------------------------------------------------------------------------------------

def _envelope(schema, rec, *, match_id, room_id, src, wid, seq, t0, cfg_hash, bal_hash, skew=0):
    tick = rec["_t"]
    env = {"v": 1, "type": rec["type"], "matchId": match_id, "roomId": room_id, "src": src, "wid": wid, "seq": seq,
           "tick": tick, "mt": tick - t0 if tick >= t0 else None, "utc": utc_of(tick, skew), "build": BUILD,
           "cfg": CFG_VERSION, "cfgHash": cfg_hash, "balHash": bal_hash, "fake": True}
    for k, v in rec.items():
        if k in ("_t", "_o", "type"):
            continue
        env["from" if k == "from_" else k] = v
    return tel_schema.ordered(schema, env)


def _header(src, wid, match_id, opened):
    return {"v": 1, "type": "_header", "schema": "telemetry.v1", "src": src, "wid": wid, "matchId": match_id,
            "build": BUILD, "opened": opened, "fake": True}


def _trailer(src, wid, match_id, written, closed):
    return {"v": 1, "type": "_trailer", "src": src, "wid": wid, "matchId": match_id, "written": written,
            "droppedQueue": 0, "droppedIo": 0, "truncated": False, "closed": closed, "fake": True}


def _write_group(schema, recs, *, match_id, room_id, src, wid, t0, cfg_hash, bal_hash, skew=0):
    recs = sorted(recs, key=lambda r: (r["_t"], r["_o"]))
    lines = []
    out = [_envelope(schema, r, match_id=match_id, room_id=room_id, src=src, wid=wid, seq=i + 1, t0=t0,
                     cfg_hash=cfg_hash, bal_hash=bal_hash, skew=skew) for i, r in enumerate(recs)]
    opened = (out[0]["utc"] if out else utc_of(t0, skew)) - 1000
    closed = (out[-1]["utc"] if out else utc_of(t0, skew)) + 1000
    lines.append(tel_schema.dumps(_header(src, wid, match_id, opened)))
    lines.extend(tel_schema.dumps(r) for r in out)
    lines.append(tel_schema.dumps(_trailer(src, wid, match_id, len(out), closed)))
    return lines


def _perf_server_lines(schema, rng, wid, first_tick, last_tick):
    recs = []
    utc = utc_of(first_tick) - 5000
    stop = utc_of(last_tick) + 10000
    seq = 0
    while utc <= stop:
        seq += 1
        p95 = round(rng.uniform(0.8, 2.6), 3)
        rec = {"v": 1, "type": "perf_server", "matchId": None, "roomId": None, "src": "server", "wid": wid,
               "seq": seq, "tick": None, "mt": None, "utc": utc, "build": BUILD, "cfg": None, "cfgHash": None,
               "balHash": None, "fake": True, "windowSec": 10.0,
               "ticksPerSecond": round(rng.uniform(29.8, 30.05), 3), "tickP95Ms": p95,
               "tickP99Ms": round(p95 + rng.uniform(0.2, 2.0), 3), "loopP95Ms": round(rng.uniform(0.05, 0.4), 3),
               "loopP99Ms": round(rng.uniform(0.4, 1.2), 3), "tickOverruns": 0, "droppedTicks": 0,
               "cpuOneCorePercent": round(rng.uniform(3.0, 9.0), 3), "workingSetMiB": round(rng.uniform(80.0, 96.0), 3),
               "peers": 10, "active": 10, "rooms": 1}
        recs.append(tel_schema.ordered(schema, rec))
        utc += 10000
    lines = [tel_schema.dumps(_header("server", wid, None, recs[0]["utc"] - 1000))]
    lines.extend(tel_schema.dumps(r) for r in recs)
    lines.append(tel_schema.dumps(_trailer("server", wid, None, len(recs), recs[-1]["utc"] + 1000)))
    return lines


def _inject_reconnect(sim: MatchSim, rng: random.Random):
    """Client drops at T and resumes at T+delta (< grace). Same client process: same wid, seq continues."""
    delta = 6 * TICK_HZ
    t = sim.t0 + 200 * TICK_HZ
    excluded = {r["netId"] for r in sim.server if r["type"] == "session_state"}
    cands = [p.net_id for p in sim.players
             if p.net_id not in excluded and (p.death_tick is None or p.death_tick > t + delta + 300)]
    if not cands or sim.end_tick <= t + delta:
        return None
    nid = cands[rng.randrange(len(cands))]
    sim.emit(t, "session_state", netId=nid, kind="DROPPED", transport="UDP", graceMs=GRACE_SECONDS * 1000)
    sim.emit(t + delta, "session_state", netId=nid, kind="RESUMED", transport="UDP", graceMs=delta * 1000 // TICK_HZ)
    before = len(sim.client.get(nid, []))
    sim.client[nid] = [r for r in sim.client.get(nid, []) if not (t < r["_t"] <= t + delta)]
    removed = before - len(sim.client[nid])
    return {"matchId": sim.match_id, "netId": nid, "dropTick": t, "resumeTick": t + delta,
            "addedServer": 2, "removedClient": removed}


def _inject_semantic(sims, n, rng):
    once = ("match_start", "match_end", "player_spawn", "elimination", "zone_phase", "replay_choice")
    cands = [(si, i) for si, s in enumerate(sims) for i, r in enumerate(s.server) if r["type"] in once]
    picks = sorted(rng.sample(cands, min(n, len(cands))))
    for si, i in picks:
        copy = dict(sims[si].server[i])
        sims[si]._order += 1
        copy["_o"] = sims[si].server[i]["_o"]   # same tick, written right after the original
        sims[si].server.append(copy)
    return len(picks)


def generate(out_dir, seed=1, cfg="A", matches=6, players=10, sample_hz=1.0, schema=None,
             dup=0.0, gap=0, reorder=False, conflict=0, semantic_dup=0, reconnect=False) -> dict:
    """Write one fake run into out_dir. Returns the inject manifest (also written as inject_manifest.json)."""
    if cfg not in ("A", "B"):
        raise ValueError("cfg must be A or B")
    if not 2 <= players <= 10:
        raise ValueError("players must be 2..10")
    schema = schema or tel_schema.load_schema()
    weapons, equips = _load_balance(schema)
    zone_sched = _load_zone()
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    run_rng = random.Random(f"tel-fake:{seed}:{cfg}:run")
    server_wid = _hex8(run_rng)
    client_wids = [_hex8(run_rng) for _ in range(players)]
    client_skew = [run_rng.randint(-900, 900) for _ in range(players)]
    perf_wid = _hex8(run_rng)
    perf_rng = random.Random(f"tel-fake:{seed}:{cfg}:perf")
    inj_rng = random.Random(f"tel-fake:{seed}:{cfg}:inject")
    cfg_hash, bal_hash = cfg_hash_for(cfg), bal_hash_for(cfg)

    sims = [MatchSim(seed, cfg, k, players, sample_hz, weapons, equips, zone_sched).run() for k in range(matches)]
    recon = _inject_reconnect(sims[0], inj_rng) if reconnect else None
    sem = _inject_semantic(sims, semantic_dup, inj_rng) if semantic_dup else 0

    files: dict[str, list[str]] = {}
    for s in sims:
        files[f"{s.match_id}.server.{server_wid}.jsonl"] = _write_group(
            schema, s.server, match_id=s.match_id, room_id=ROOM_ID, src="server", wid=server_wid, t0=s.t0,
            cfg_hash=cfg_hash, bal_hash=None)
        for nid in sorted(s.client):
            files[f"{s.match_id}.client.{nid}.jsonl"] = _write_group(
                schema, s.client[nid], match_id=s.match_id, room_id=None, src="client", wid=client_wids[nid],
                t0=s.t0, cfg_hash=cfg_hash, bal_hash=bal_hash, skew=client_skew[nid])
    files[f"perf_server.{perf_wid}.jsonl"] = _perf_server_lines(schema, perf_rng, perf_wid, sims[0].t_lobby,
                                                                sims[-1].end_tick + ENDING_TICKS)
    names = sorted(files)
    records = sum(len(files[n]) - 2 for n in names)

    # line-level faults -------------------------------------------------------------------------------
    slots = [(n, i) for n in names for i in range(1, len(files[n]) - 1)]
    once = set(schema.once_types())
    used: set[tuple[str, int]] = set()
    inserts: dict[str, list[str]] = {n: [] for n in names}
    n_dup = int(round(len(slots) * dup / 100.0)) if dup else 0
    if n_dup:
        picks = sorted(inj_rng.sample(slots, n_dup))
        used.update(picks)
        for n, i in picks:
            inserts[n].append(files[n][i])
    n_conf = 0
    if conflict:
        free = [s for s in slots if s not in used]
        picks = sorted(inj_rng.sample(free, min(conflict, len(free))))
        used.update(picks)
        for n, i in picks:
            rec = json.loads(files[n][i])
            rec["utc"] += 7
            inserts[n].append(tel_schema.dumps(rec))
        n_conf = len(picks)
    removed: set[tuple[str, int]] = set()
    if gap:
        free = [s for s in slots if s not in used and json.loads(files[s[0]][s[1]])["type"] not in once]
        removed = set(inj_rng.sample(free, min(gap, len(free))))
    for n in names:
        body = [ln for i, ln in enumerate(files[n][1:-1], start=1) if (n, i) not in removed]
        for extra in inserts[n]:
            body.insert(inj_rng.randint(0, len(body)), extra)
        if reorder:
            inj_rng.shuffle(body)
        files[n] = [files[n][0]] + body + [files[n][-1]]

    for n in names:
        (out / n).write_bytes(("\n".join(files[n]) + "\n").encode("utf-8"))
    manifest = {
        "generator": "tel_fake", "seed": seed, "cfg": cfg, "matches": matches, "players": players,
        "sampleHz": sample_hz, "cfgHash": cfg_hash, "balHash": bal_hash,
        "matchIds": [s.match_id for s in sims], "files": len(names), "records": records,
        "expectedMerged": records - sem - len(removed),
        "inject": {"dup": n_dup, "conflict": n_conf, "semanticDup": sem, "gap": len(removed),
                   "reorder": bool(reorder), "reconnect": recon},
        "results": [{"matchId": s.match_id, "reason": s.result[0], "winnerNetId": s.result[1]} for s in sims],
    }
    (out / "inject_manifest.json").write_bytes(
        (json.dumps(manifest, indent=2, sort_keys=False) + "\n").encode("utf-8"))
    return manifest
