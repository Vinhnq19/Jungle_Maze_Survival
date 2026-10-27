"""TEL-01 telemetry schema: load Docs/Telemetry/telemetry.schema.v1.json and validate records.

The schema JSON is the single source of truth. Weapon and equipment Code IDs are read at run time
from the balance files named in `idSets`, so this module never keeps a second copy of the config.

`validate_record()` / `validate_line()` return a list of errors and never raise.
Each error is a dict {line, code, field, detail}; `code` is one of the schema's `errorCodes`.
"""
from __future__ import annotations

import json
import math
import re
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
REPO_ROOT = TOOLS_DIR.parents[1]
DEFAULT_SCHEMA = REPO_ROOT / "Docs" / "Telemetry" / "telemetry.schema.v1.json"

AUX_TYPES = ("_header", "_trailer")
OUTPUT_NAMES = frozenset({"events.jsonl", "conflicts.jsonl"})

_HEX = {
    "guid": re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$"),
    "hex8": re.compile(r"^[0-9a-f]{8}$"),
    "hex16": re.compile(r"^[0-9a-f]{16}$"),
    "hex64": re.compile(r"^[0-9a-f]{64}$"),
}


class Schema:
    """Parsed schema plus the resolved Code ID sets."""

    def __init__(self, raw: dict, id_sets: dict[str, frozenset[str]], path: Path):
        self.raw = raw
        self.path = path
        self.version = raw["v"]
        self.name = raw["schema"]
        self.envelope: dict = raw["envelope"]
        self.enums: dict[str, list[str]] = raw["enums"]
        self.types: dict[str, dict] = raw["types"]
        self.aux: dict[str, dict] = raw["aux"]
        self.rules: list[dict] = raw["rules"]
        self.error_codes: list[str] = raw["errorCodes"]
        self.id_sets = id_sets
        pii = raw["pii"]
        self.pii_keys = frozenset(k.lower() for k in pii["forbiddenKeys"])
        self.pii_patterns = {k: re.compile(v) for k, v in pii["patterns"].items()}
        self.all_ids = frozenset().union(*id_sets.values()) if id_sets else frozenset()

    # -- helpers -----------------------------------------------------------------------------
    def type_names(self) -> list[str]:
        """The 18 record types in schema order (by 'n')."""
        return sorted(self.types, key=lambda t: self.types[t]["n"])

    def field_order(self, rtype: str) -> list[str]:
        if rtype in self.aux:
            return list(self.aux[rtype]["fields"])
        return list(self.envelope) + list(self.types[rtype]["fields"])

    def once_types(self) -> list[str]:
        return [t for t in self.type_names() if self.types[t].get("once")]

    def enum_fields(self) -> list[tuple[str, str]]:
        """(type or '*envelope*', field) for every enum-typed field."""
        out = [("*envelope*", f) for f, s in self.envelope.items() if s["type"] == "enum"]
        for t in self.type_names():
            out += [(t, f) for f, s in self.types[t]["fields"].items() if s["type"] == "enum"]
        return out


def _load_id_set(spec: dict, repo_root: Path, balance_dir: Path | None) -> frozenset[str]:
    if "values" in spec:
        return frozenset(spec["values"])
    src = Path(spec["source"])
    path = (balance_dir / src.name) if balance_dir else (repo_root / src)
    data = json.loads(path.read_text(encoding="utf-8"))
    key = spec.get("key", "id")
    return frozenset(str(item[key]) for item in data)


def load_schema(path: str | Path | None = None, balance_dir: str | Path | None = None) -> Schema:
    """Load the schema and resolve idSets. `balance_dir` overrides the folder of weapons/equipments."""
    path = Path(path) if path else DEFAULT_SCHEMA
    raw = json.loads(path.read_text(encoding="utf-8"))
    bal = Path(balance_dir) if balance_dir else None
    id_sets = {name: _load_id_set(spec, REPO_ROOT, bal) for name, spec in raw["idSets"].items()}
    return Schema(raw, id_sets, path)


# -- serialisation ---------------------------------------------------------------------------

def ordered(schema: Schema, record: dict) -> dict:
    """Return a copy with keys in write order (envelope, then type fields); floats rounded to 3 dp."""
    rtype = record.get("type")
    order = schema.field_order(rtype) if rtype in schema.types or rtype in schema.aux else []
    out = {}
    for k in order:
        if k in record:
            out[k] = _round(record[k])
    for k in record:
        if k not in out:
            out[k] = _round(record[k])
    return out


def _round(value):
    if isinstance(value, float):
        return round(value, 3)
    if isinstance(value, list):
        return [_round(v) for v in value]
    if isinstance(value, dict):
        return {k: _round(v) for k, v in value.items()}
    return value


def dumps(record: dict) -> str:
    """Compact one-line JSON (no trailing newline), non-ASCII kept as UTF-8."""
    return json.dumps(record, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def canonical(record: dict) -> str:
    """Key-sorted JSON used to compare record content (merge 4.3 step 2)."""
    return json.dumps(record, ensure_ascii=False, separators=(",", ":"), sort_keys=True, allow_nan=False)


def _reject_constant(name):
    raise ValueError(f"non-finite number {name}")


def parse_line(text: str):
    """json.loads that rejects NaN/Infinity. Raises ValueError on bad JSON."""
    return json.loads(text, parse_constant=_reject_constant)


# -- file helpers ----------------------------------------------------------------------------

def expand_inputs(paths) -> list[Path]:
    """Files and folders -> sorted list of *.jsonl files (folders searched recursively).

    Merge outputs (events.jsonl, conflicts.jsonl) inside folders are skipped so a report folder
    can sit next to its inputs."""
    out: set[Path] = set()
    for p in paths:
        p = Path(p)
        if p.is_dir():
            out.update(f for f in p.rglob("*.jsonl") if f.name not in OUTPUT_NAMES)
        elif p.exists():
            out.add(p)
        else:
            raise FileNotFoundError(str(p))
    return sorted(out, key=lambda f: f.as_posix())


def read_lines(path: Path) -> list[str]:
    """Non-empty lines of a UTF-8 file without line terminators (LF or CRLF)."""
    text = Path(path).read_text(encoding="utf-8-sig")
    return [ln.rstrip("\r") for ln in text.split("\n") if ln.strip()]


# -- validation ------------------------------------------------------------------------------

def _err(errors, line, code, field, detail):
    errors.append({"line": line, "code": code, "field": field, "detail": detail})


def _is_int(v) -> bool:
    return isinstance(v, int) and not isinstance(v, bool)


def _check_value(schema: Schema, spec: dict, value, field: str, errors: list, line) -> bool:
    """Check one non-null value against a field spec. Returns True when the value is well typed."""
    t = spec["type"]
    ok = True
    if t in ("int", "uint", "int64"):
        if not _is_int(value):
            _err(errors, line, "bad_type", field, f"expected {t}, got {type(value).__name__}")
            return False
        if t == "uint" and value < 0:
            _err(errors, line, "out_of_range", field, f"{value} < 0")
            ok = False
    elif t == "num":
        if not (_is_int(value) or isinstance(value, float)) or (isinstance(value, float) and not math.isfinite(value)):
            _err(errors, line, "bad_type", field, f"expected number, got {type(value).__name__}")
            return False
    elif t == "bool":
        if not isinstance(value, bool):
            _err(errors, line, "bad_type", field, f"expected bool, got {type(value).__name__}")
            return False
    elif t in ("str", "enum", "id") or t in _HEX:
        if not isinstance(value, str):
            _err(errors, line, "bad_type", field, f"expected string, got {type(value).__name__}")
            return False
        if t in _HEX and not _HEX[t].match(value):
            _err(errors, line, "bad_type", field, f"not a lowercase {t}")
            return False
        if t == "enum" and value not in schema.enums[spec["enum"]]:
            _err(errors, line, "bad_enum", field, f"{value!r} not in {spec['enum']}")
            ok = False
        if t == "id":
            allowed = frozenset().union(*(schema.id_sets[s] for s in spec["ids"]))
            if value not in allowed:
                _err(errors, line, "invalid_id", field, f"{value!r} not in idSets {spec['ids']}")
                ok = False
    elif t == "array":
        if not isinstance(value, list):
            _err(errors, line, "bad_type", field, "expected array")
            return False
        if "maxItems" in spec and len(value) > spec["maxItems"]:
            _err(errors, line, "too_many_items", field, f"{len(value)} > {spec['maxItems']}")
            ok = False
        for i, item in enumerate(value):
            ok &= _check_field(schema, spec["items"], item, f"{field}[{i}]", errors, line)
    elif t == "object":
        if not isinstance(value, dict):
            _err(errors, line, "bad_type", field, "expected object")
            return False
        props = spec["props"]
        for k in value:
            if k not in props:
                _err(errors, line, "unknown_field", f"{field}.{k}", "not in schema")
                ok = False
        for k, sub in props.items():
            if k not in value:
                _err(errors, line, "missing_field", f"{field}.{k}", "required")
                ok = False
            else:
                ok &= _check_field(schema, sub, value[k], f"{field}.{k}", errors, line)
    else:  # unknown field type in schema itself
        _err(errors, line, "bad_type", field, f"schema type {t!r} unsupported")
        return False

    if "const" in spec and value != spec["const"]:
        _err(errors, line, "bad_const", field, f"expected {spec['const']!r}")
        ok = False
    if ("min" in spec or "max" in spec) and (_is_int(value) or isinstance(value, float)):
        if "min" in spec and value < spec["min"]:
            _err(errors, line, "out_of_range", field, f"{value} < {spec['min']}")
            ok = False
        if "max" in spec and value > spec["max"]:
            _err(errors, line, "out_of_range", field, f"{value} > {spec['max']}")
            ok = False
    if "maxLen" in spec and isinstance(value, str) and len(value) > spec["maxLen"]:
        _err(errors, line, "too_long", field, f"{len(value)} > {spec['maxLen']}")
        ok = False
    return ok


def _check_field(schema, spec, value, field, errors, line, rtype=None) -> bool:
    if value is None:
        if spec.get("null") or (rtype is not None and rtype in spec.get("nullFor", ())):
            return True
        _err(errors, line, "null_not_allowed", field, "null")
        return False
    return _check_value(schema, spec, value, field, errors, line)


def _rule_applies(rule, rtype):
    return "types" not in rule or rtype in rule["types"]


def _check_rules(schema: Schema, rec: dict, rtype: str, bad: set, errors: list, line):
    tspec = schema.types[rtype]
    for rule in schema.rules:
        if not _rule_applies(rule, rtype):
            continue
        rid = rule["id"]
        if "if" in rule:
            fields = set(rule["if"]) | set(rule["then"])
            if fields & bad or not fields <= rec.keys():
                continue
            if not all(rec[f] in vals for f, vals in rule["if"].items()):
                continue
            for f, cond in rule["then"].items():
                v = rec[f]
                if cond.get("null") and v is not None:
                    _err(errors, line, "rule_violation", f, f"{rid}: must be null")
                elif "in" in cond and v not in cond["in"]:
                    _err(errors, line, "rule_violation", f, f"{rid}: must be one of {cond['in']}")
        elif rid == "src-matches-type":
            if "src" in rec and "src" not in bad and rec["src"] != tspec["src"]:
                _err(errors, line, "rule_violation", "src", f"{rid}: {rtype} is written by {tspec['src']}")
        elif rid == "span-order":
            if {"startTick", "endTick"} <= rec.keys() and not {"startTick", "endTick"} & bad:
                if rec["endTick"] < rec["startTick"]:
                    _err(errors, line, "rule_violation", "endTick", f"{rid}: endTick < startTick")
        elif rid == "applied-le-raw":
            if {"applied", "raw"} <= rec.keys() and not {"applied", "raw"} & bad:
                if rec["applied"] > rec["raw"]:
                    _err(errors, line, "rule_violation", "applied", f"{rid}: applied > raw")
        else:
            _err(errors, line, "rule_violation", "*", f"unknown named rule {rid!r}")


def _scan_pii(schema: Schema, value, path: str, errors: list, line, samples):
    if isinstance(value, dict):
        for k, v in value.items():
            sub = f"{path}.{k}" if path else k
            if k.lower() in schema.pii_keys:
                _err(errors, line, "pii", sub, "forbidden key")
            _scan_pii(schema, v, sub, errors, line, samples)
    elif isinstance(value, list):
        for i, v in enumerate(value):
            _scan_pii(schema, v, f"{path}[{i}]", errors, line, samples)
    elif isinstance(value, str):
        for name, pat in schema.pii_patterns.items():
            if pat.search(value):
                _err(errors, line, "pii", path, f"looks like {name}")
        for s in samples:
            if s and s in value:
                _err(errors, line, "pii", path, "contains a sample secret/name")


def validate_record(schema: Schema, rec, line=None, pii_samples=()) -> list[dict]:
    """Validate one parsed record. Never raises."""
    errors: list[dict] = []
    try:
        if not isinstance(rec, dict):
            _err(errors, line, "bad_json", "*", "line is not a JSON object")
            return errors
        _scan_pii(schema, rec, "", errors, line, tuple(pii_samples))
        if "type" not in rec:
            _err(errors, line, "missing_field", "type", "required")
            return errors
        rtype = rec["type"]
        if not isinstance(rtype, str) or (rtype not in schema.types and rtype not in schema.aux):
            _err(errors, line, "unknown_type", "type", repr(rtype))
            return errors
        if rtype in schema.aux:
            specs = dict(schema.aux[rtype]["fields"])
        else:
            specs = dict(schema.envelope)
            specs.update(schema.types[rtype]["fields"])
        bad: set[str] = set()
        for k in rec:
            if k not in specs and k.lower() not in schema.pii_keys:
                _err(errors, line, "unknown_field", k, "not in schema")
        for k, spec in specs.items():
            if k not in rec:
                if not spec.get("optional"):
                    _err(errors, line, "missing_field", k, "required")
                    bad.add(k)
                continue
            if k == "type" and rtype in schema.types:
                continue
            if not _check_field(schema, spec, rec[k], k, errors, line, rtype):
                bad.add(k)
        if rtype in schema.types:
            _check_rules(schema, rec, rtype, bad, errors, line)
    except Exception as exc:  # validator must never raise
        _err(errors, line, "bad_json", "*", f"validator error: {exc!r}")
    return errors


def validate_line(schema: Schema, text: str, line=None, pii_samples=()):
    """Parse and validate one JSONL line. Returns (record_or_None, errors)."""
    try:
        rec = parse_line(text)
    except (ValueError, RecursionError) as exc:
        return None, [{"line": line, "code": "bad_json", "field": "*", "detail": str(exc)[:120]}]
    return rec, validate_record(schema, rec, line, pii_samples)


def validate_files(schema: Schema, paths, pii_samples=()) -> dict:
    """Validate every line of the given files. Returns a summary dict (deterministic order)."""
    files = expand_inputs(paths)
    summary = {"files": len(files), "lines": 0, "records": 0, "errors": 0, "byCode": {}, "byType": {},
               "fake": 0, "examples": []}
    for f in files:
        for i, text in enumerate(read_lines(f), start=1):
            summary["lines"] += 1
            rec, errs = validate_line(schema, text, i, pii_samples)
            if isinstance(rec, dict) and rec.get("type") in schema.types:
                summary["records"] += 1
                summary["byType"][rec["type"]] = summary["byType"].get(rec["type"], 0) + 1
                if rec.get("fake") is True:
                    summary["fake"] += 1
            for e in errs:
                summary["errors"] += 1
                summary["byCode"][e["code"]] = summary["byCode"].get(e["code"], 0) + 1
                if len(summary["examples"]) < 20:
                    summary["examples"].append(dict(e, file=f.as_posix()))
    summary["byCode"] = dict(sorted(summary["byCode"].items()))
    summary["byType"] = dict(sorted(summary["byType"].items()))
    return summary


def cfg_key(rec: dict) -> str:
    """cfgKey = cfg + '/' + cfgHash[0:8] + '/' + (balHash[0:8] or '-')."""
    cfg = rec.get("cfg") or "-"
    ch = (rec.get("cfgHash") or "-")[:8]
    bh = (rec.get("balHash") or "-")[:8]
    return f"{cfg}/{ch}/{bh}"
