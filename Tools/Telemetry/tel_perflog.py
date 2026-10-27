"""TEL-01 perflog: turn GameServer `[metrics]` log lines into `perf_server` records (plan 4.3, AC28).

Line format (GameServer/Diagnostics/ServerMetrics.cs), anything may precede `[metrics]`:
  [metrics] peers=10 active=10 pending=0 ticks=1800 ticksPerSecond=30.00 tickP95Ms=1.234 tickP99Ms=2.345
            loopP95Ms=0.120 loopP99Ms=0.800 cpuOneCorePercent=4.50 tickOverruns=0 droppedTicks=0 pings=20
            invalidPackets=0 historyUnavailable=0 rejected=0 rooms=1 workingSetMiB=88.25 roomSlots=room-1:10/10
            utc=2026-10-27T08:15:30.1234567Z
Extra keys are ignored. A `[metrics]` line with a missing or unparsable field is skipped and counted
as malformed; it never stops the run. Other lines are ignored.

`wid` = first 8 hex of SHA-256 of the log bytes, so ingesting the same log again yields technical
duplicates instead of new records. `windowSec` = utc difference to the previous accepted line; the first
line uses --interval (default 10). The record never carries room slots or any other text from the log.
"""
from __future__ import annotations

import calendar
import hashlib
import re
from pathlib import Path

import tel_schema

FLOAT_KEYS = ("ticksPerSecond", "tickP95Ms", "tickP99Ms", "loopP95Ms", "loopP99Ms", "cpuOneCorePercent",
              "workingSetMiB")
INT_KEYS = ("tickOverruns", "droppedTicks", "peers", "active", "rooms")
_UTC = re.compile(r"^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?(Z|[+-]\d{2}:\d{2})?$")
_NUM = re.compile(r"^\d+(?:\.\d+)?$")      # schema: every perf number is >= 0
_INT = re.compile(r"^\d+$")
MARK = "[metrics]"


def parse_utc_ms(text: str) -> int:
    """ISO-8601 round-trip ('O') timestamp -> epoch milliseconds. Raises ValueError."""
    m = _UTC.match(text)
    if not m:
        raise ValueError(f"bad utc {text!r}")
    y, mo, d, h, mi, s, frac, tz = m.groups()
    secs = calendar.timegm((int(y), int(mo), int(d), int(h), int(mi), int(s), 0, 0, 0))
    if tz and tz != "Z":
        sign = 1 if tz[0] == "+" else -1
        secs -= sign * (int(tz[1:3]) * 3600 + int(tz[4:6]) * 60)
    ms = int((frac or "0")[:3].ljust(3, "0"))
    return secs * 1000 + ms


def parse_metrics_line(line: str) -> dict:
    """Return the key=value pairs after `[metrics]`. Raises ValueError when the line is not usable."""
    idx = line.find(MARK)
    if idx < 0:
        raise ValueError("no [metrics] marker")
    pairs = {}
    for tok in line[idx + len(MARK):].split():
        if "=" not in tok:
            raise ValueError(f"bad token {tok!r}")
        k, v = tok.split("=", 1)
        pairs[k] = v
    out = {}
    for k in FLOAT_KEYS:
        if k not in pairs or not _NUM.match(pairs[k]):
            raise ValueError(f"missing or bad {k}")
        out[k] = round(float(pairs[k]), 3)
    for k in INT_KEYS:
        if k not in pairs or not _INT.match(pairs[k]):
            raise ValueError(f"missing or bad {k}")
        out[k] = int(pairs[k])
    out["utc"] = parse_utc_ms(pairs.get("utc", ""))
    return out


def convert(data: bytes, interval: float = 10.0, build: str = "dev", cfg: str | None = None,
            cfg_hash: str | None = None, schema: tel_schema.Schema | None = None) -> dict:
    """Convert raw log bytes. Returns {"wid", "records", "lines", "malformed", "malformedLines"}."""
    schema = schema or tel_schema.load_schema()
    wid = hashlib.sha256(data).hexdigest()[:8]
    text = data.decode("utf-8", errors="replace")
    records, malformed, bad_lines = [], 0, []
    prev = None
    for no, raw in enumerate(text.splitlines(), start=1):
        if MARK not in raw:
            continue
        try:
            v = parse_metrics_line(raw)
        except ValueError:
            malformed += 1
            bad_lines.append(no)
            continue
        window = interval if prev is None or v["utc"] <= prev else (v["utc"] - prev) / 1000.0
        prev = v["utc"] if prev is None else max(prev, v["utc"])
        rec = {"v": 1, "type": "perf_server", "matchId": None, "roomId": None, "src": "server", "wid": wid,
               "seq": len(records) + 1, "tick": None, "mt": None, "utc": v["utc"], "build": build, "cfg": cfg,
               "cfgHash": cfg_hash, "balHash": None, "windowSec": round(float(window), 3)}
        for k in FLOAT_KEYS + INT_KEYS:
            rec[k] = v[k]
        records.append(tel_schema.ordered(schema, rec))
    return {"wid": wid, "records": records, "lines": len(text.splitlines()), "malformed": malformed,
            "malformedLines": bad_lines}


def write(result: dict, out_dir, name: str | None = None) -> Path:
    """Write `_header`, records and `_trailer` (opened/closed from the log's own utc, not the clock)."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    recs, wid = result["records"], result["wid"]
    first = recs[0]["utc"] if recs else 0
    last = recs[-1]["utc"] if recs else 0
    build = recs[0]["build"] if recs else "dev"
    head = {"v": 1, "type": "_header", "schema": "telemetry.v1", "src": "server", "wid": wid, "matchId": None,
            "build": build, "opened": first}
    tail = {"v": 1, "type": "_trailer", "src": "server", "wid": wid, "matchId": None, "written": len(recs),
            "droppedQueue": 0, "droppedIo": 0, "truncated": False, "closed": last}
    path = out / (name or f"perf_server.{wid}.jsonl")
    lines = [tel_schema.dumps(head)] + [tel_schema.dumps(r) for r in recs] + [tel_schema.dumps(tail)]
    path.write_bytes(("\n".join(lines) + "\n").encode("utf-8"))
    return path
