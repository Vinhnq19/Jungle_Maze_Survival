"""Rebuild fixtures/valid and fixtures/invalid from the schema (run after a schema change, then review the diff).

    python -I Tools/Telemetry/fixtures/make_fixtures.py

valid/<type>.jsonl   : `_header`, one or two records of that type in real-writer format (no `fake`), `_trailer`.
invalid/<code>__<what>.jsonl : one record per file; the validator must report <code> for every line.
"""
from __future__ import annotations

import copy
import hashlib
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import tel_schema  # noqa: E402

MATCH = "3f2a6c1e-8b4d-4e7a-9c11-0d5e2b7f4a90"
WID = "a1b2c3d4"
CFG_HASH = hashlib.sha256(b"fixture-cfg").hexdigest()
BAL_HASH = hashlib.sha256(b"fixture-bal").hexdigest()
UTC = 1793059260000

FIELDS = {
    "match_start": {"profile": "main", "players": 10, "minPlayers": 6, "seedHash": "9c4e1f0a2b3d4c5e"},
    "match_end": {"reason": "LastAlive", "winnerNetId": 4, "durTicks": 11820, "alive": 1},
    "player_spawn": {"netId": 3, "sess": "0f1e2d3c4b5a6978", "x": 12.5, "y": 40.25, "loadout": []},
    "attack": {"netId": 2, "weapon": "KNIFE", "kind": "MELEE", "x": 20.125, "y": 18.0, "ammo": None},
    "damage": {"victim": 5, "attacker": 2, "cause": "WEAPON", "weapon": "KNIFE", "hazard": None, "raw": 22,
               "applied": 18, "absorbed": 4, "hpAfter": 64, "helmetDur": 26, "armorDur": None},
    "elimination": {"victim": 5, "killer": 2, "cause": "WEAPON", "weapon": "KNIFE", "hazard": None, "rank": 7,
                    "survTicks": 5400, "x": 21.0, "y": 18.5, "zonePhase": 1, "hidden": False},
    "cc_span": {"victim": 6, "source": 1, "by": "MACE", "ccKind": "STUN", "startTick": 9000, "endTick": 9030,
                "plannedMs": 1000, "end": "EXPIRED"},
    "zone_phase": {"phase": 0, "state": "WARNING", "cx": 25.0, "cy": 25.0, "r": 25.0, "targetR": 18.0},
    "loot_drop": {"container": "CHEST_WOODEN", "by": 3, "items": [{"id": "AXE", "qty": 1}, {"id": "AMMO", "qty": 12}],
                  "x": 30.5, "y": 11.0},
    "item_pickup": {"netId": 3, "item": "AXE", "qty": 1, "from": "CONTAINER", "x": 30.5, "y": 11.0},
    "item_use": {"netId": 3, "item": "GOD_LEAF", "startTick": 9100, "endTick": 9115, "plannedMs": 500,
                 "result": "DONE", "hpBefore": 40, "hpAfter": 50},
    "equip_change": {"netId": 3, "action": "EQUIP", "slot": "WEAPON", "item": "AXE", "from": "HAND"},
    "hide_span": {"netId": 7, "startTick": 9200, "endTick": 9500, "bush": "BUSH_12", "x": 8.0, "y": 44.5,
                  "end": "EXIT", "attackedWhileHidden": False},
    "position_sample": {"netId": 1, "x": 24.0, "y": 26.5, "hp": 100, "weapon": "HAND", "inZone": True},
    "session_state": {"netId": 8, "kind": "RESUMED", "transport": "UDP", "graceMs": 6000},
    "replay_choice": {"netId": 0, "choice": "REPLAY", "afterMs": 2400},
    "perf_server": {"windowSec": 10.0, "ticksPerSecond": 30.01, "tickP95Ms": 1.234, "tickP99Ms": 2.5,
                    "loopP95Ms": 0.12, "loopP99Ms": 0.8, "tickOverruns": 0, "droppedTicks": 0,
                    "cpuOneCorePercent": 4.5, "workingSetMiB": 88.25, "peers": 10, "active": 10, "rooms": 1},
    "perf_client": {"netId": 4, "windowSec": 5.0, "frames": 300, "fpsAvg": 60.0, "fps1Low": 44.5,
                    "frameMsP95": 20.5, "frameMsMax": 31.25, "transport": "UDP", "rttP50": 24.0, "rttP95": 41.5,
                    "jitterMs": 2.25, "lossPct": 0.5, "entities": 10},
}
# a second record for types where one value alone would hide a rule branch
EXTRA = {
    "damage": {"victim": 5, "attacker": None, "cause": "ZONE", "weapon": None, "hazard": "ZONE", "raw": 5,
               "applied": 5, "absorbed": None, "hpAfter": 59, "helmetDur": None, "armorDur": None},
    "elimination": {"victim": 6, "killer": None, "cause": "DISCONNECT", "weapon": None, "hazard": None, "rank": 6,
                    "survTicks": 6000, "x": 3.0, "y": 4.0, "zonePhase": 2, "hidden": None},
    "session_state": {"netId": 8, "kind": "DROPPED", "transport": None, "graceMs": 15000},
}


def envelope(schema, rtype, seq=1, tick=9000):
    src = schema.types[rtype]["src"]
    if rtype == "perf_server":
        env = {"matchId": None, "roomId": None, "tick": None, "mt": None, "cfg": None, "cfgHash": None, "balHash": None}
    else:
        env = {"matchId": MATCH, "roomId": None if src == "client" else "room-1", "tick": tick, "mt": tick - 600,
               "cfg": "1.0.0", "cfgHash": CFG_HASH, "balHash": BAL_HASH if src == "client" else None}
    env.update({"v": 1, "type": rtype, "src": src, "wid": WID, "seq": seq, "utc": UTC + seq * 33, "build": "dev"})
    return env


def record(schema, rtype, fields, seq=1):
    rec = envelope(schema, rtype, seq)
    rec.update(fields)
    return tel_schema.ordered(schema, rec)


def write(path: Path, recs):
    path.write_bytes(("\n".join(tel_schema.dumps(r) for r in recs) + "\n").encode("utf-8"))


def build_valid(schema, out: Path):
    out.mkdir(parents=True, exist_ok=True)
    for t in schema.type_names():
        src = schema.types[t]["src"]
        mid = None if t == "perf_server" else MATCH
        recs = [record(schema, t, FIELDS[t])]
        if t in EXTRA:
            recs.append(record(schema, t, EXTRA[t], seq=2))
        head = {"v": 1, "type": "_header", "schema": "telemetry.v1", "src": src, "wid": WID, "matchId": mid,
                "build": "dev", "opened": UTC}
        tail = {"v": 1, "type": "_trailer", "src": src, "wid": WID, "matchId": mid, "written": len(recs),
                "droppedQueue": 0, "droppedIo": 0, "truncated": False, "closed": UTC + 1000}
        write(out / f"{t}.jsonl", [head] + recs + [tail])


def build_invalid(schema, out: Path):
    out.mkdir(parents=True, exist_ok=True)
    base = record(schema, "damage", FIELDS["damage"])
    cases: dict[str, dict | str] = {}
    for f in schema.envelope:
        if schema.envelope[f].get("optional"):
            continue
        r = copy.deepcopy(base)
        del r[f]
        cases[f"missing_field__{f}"] = r
    for t, f in schema.enum_fields():
        if t == "*envelope*":
            r = copy.deepcopy(base)
            name = f"envelope.{f}"
        else:
            r = record(schema, t, FIELDS[t])
            name = f"{t}.{f}"
        r[f] = "NOT_A_VALUE"
        cases[f"bad_enum__{name}"] = r
    r = record(schema, "attack", FIELDS["attack"]); r["weapon"] = "BAD_ID"; cases["invalid_id__attack.weapon"] = r
    r = record(schema, "loot_drop", copy.deepcopy(FIELDS["loot_drop"])); r["items"][0]["id"] = "BAD_ID"
    cases["invalid_id__loot_drop.items"] = r
    r = record(schema, "cc_span", FIELDS["cc_span"]); r["by"] = "BAD_ID"; cases["invalid_id__cc_span.by"] = r
    r = record(schema, "item_use", FIELDS["item_use"]); r["item"] = "AXE"; cases["invalid_id__item_use.weapon_as_item"] = r
    # PII (AC4): forbidden keys and values that look like a JWT or an IP address
    r = copy.deepcopy(base); r["displayName"] = "Tester Alpha"; cases["pii__displayName"] = r
    r = copy.deepcopy(base); r["roomCode"] = "QX7K2M"; cases["pii__roomCode"] = r
    r = record(schema, "hide_span", FIELDS["hide_span"])
    r["bush"] = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.sig"; cases["pii__jwt_value"] = r
    r = copy.deepcopy(base); r["build"] = "203.0.113.7"; cases["pii__ipv4_value"] = r
    r = copy.deepcopy(base); r["build"] = "2001:db8:0:0:0:0:0:1"; cases["pii__ipv6_value"] = r
    r = copy.deepcopy(base); r["ip"] = "x"; cases["pii__ip_key"] = r
    # every other error code at least once
    r = copy.deepcopy(base); r["victim"] = None; cases["null_not_allowed__damage.victim"] = r
    r = copy.deepcopy(base); r["seq"] = 1.0; cases["bad_type__seq_float"] = r
    r = copy.deepcopy(base); r["matchId"] = MATCH.upper(); cases["bad_type__matchId_uppercase"] = r
    r = copy.deepcopy(base); r["raw"] = True; cases["bad_type__raw_bool"] = r
    r = copy.deepcopy(base); r["v"] = 2; cases["bad_const__v"] = r
    r = copy.deepcopy(base); r["fake"] = False; cases["bad_const__fake_false"] = r
    r = record(schema, "position_sample", FIELDS["position_sample"]); r["hp"] = 300; cases["out_of_range__hp"] = r
    r = copy.deepcopy(base); r["seq"] = 0; cases["out_of_range__seq_zero"] = r
    r = copy.deepcopy(base); r["build"] = "b" * 33; cases["too_long__build"] = r
    r = record(schema, "loot_drop", FIELDS["loot_drop"]); r["items"] = [{"id": "AXE", "qty": 1}] * 9
    cases["too_many_items__loot_drop.items"] = r
    r = copy.deepcopy(base); r["extra"] = 1; cases["unknown_field__extra"] = r
    r = copy.deepcopy(base); r["type"] = "kill"; cases["unknown_type__kill"] = r
    cases["bad_json__truncated"] = tel_schema.dumps(base)[:-5]
    cases["bad_json__nan"] = tel_schema.dumps(base).replace('"raw":22', '"raw":NaN')
    r = copy.deepcopy(base); r["cause"] = "ZONE"; r["hazard"] = "ZONE"; r["weapon"] = None
    cases["rule_violation__damage-zone"] = r
    r = record(schema, "elimination", FIELDS["elimination"]); r["cause"] = "HAZARD"; r["killer"] = None
    r["weapon"] = None; r["hazard"] = "ZONE"; cases["rule_violation__elim-hazard"] = r
    r = record(schema, "cc_span", FIELDS["cc_span"]); r["endTick"] = 8999; cases["rule_violation__span-order"] = r
    r = copy.deepcopy(base); r["applied"] = 30; cases["rule_violation__applied-le-raw"] = r
    r = record(schema, "perf_client", FIELDS["perf_client"]); r["src"] = "server"
    cases["rule_violation__src-matches-type"] = r
    for name, rec in cases.items():
        text = rec if isinstance(rec, str) else tel_schema.dumps(rec)
        (out / f"{name}.jsonl").write_bytes((text + "\n").encode("utf-8"))


def main():
    schema = tel_schema.load_schema()
    build_valid(schema, HERE / "valid")
    build_invalid(schema, HERE / "invalid")
    print("fixtures rebuilt:", len(list((HERE / "valid").glob("*.jsonl"))), "valid,",
          len(list((HERE / "invalid").glob("*.jsonl"))), "invalid")


if __name__ == "__main__":
    main()
