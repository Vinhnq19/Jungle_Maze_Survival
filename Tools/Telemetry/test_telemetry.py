"""Unit tests for the TEL-01 offline telemetry tools (stdlib unittest).

    python -I Tools/Telemetry/test_telemetry.py              # full suite
    python -I Tools/Telemetry/test_telemetry.py -v           # verbose
"""
from __future__ import annotations

import ast
import hashlib
import json
import random
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import tel_fake  # noqa: E402
import tel_merge  # noqa: E402
import tel_schema  # noqa: E402

FIXTURES = HERE / "fixtures"
CLI = HERE / "telemetry.py"
SCHEMA = tel_schema.load_schema()
_TMP = tempfile.mkdtemp(prefix="tel01_test_")
_CACHE: dict = {}
M2 = {"matches": 2}     # fault scenarios use 2 matches; coverage, determinism and golden use 6


def tearDownModule():
    shutil.rmtree(_TMP, ignore_errors=True)


def tmpdir(name: str) -> Path:
    p = Path(_TMP) / name
    if p.exists():
        shutil.rmtree(p)
    p.mkdir(parents=True)
    return p


def fake_run(name="clean", **kw) -> tuple[Path, dict]:
    """Generate (once per process) a fake run with the given options."""
    key = (name, tuple(sorted(kw.items())))
    if key not in _CACHE:
        d = tmpdir(f"fake_{len(_CACHE)}_{name}")
        _CACHE[key] = (d, tel_fake.generate(d, schema=SCHEMA, **kw))
    return _CACHE[key]


def records_of(folder: Path, aux=False) -> list[dict]:
    out = []
    for f in tel_schema.expand_inputs([folder]):
        for ln in tel_schema.read_lines(f):
            r = json.loads(ln)
            if aux or not r["type"].startswith("_"):
                out.append(r)
    return out


def run_cli(*args) -> subprocess.CompletedProcess:
    return subprocess.run([sys.executable, "-I", str(CLI), *map(str, args)], capture_output=True, text=True,
                          encoding="utf-8")


def file_hashes(folder: Path) -> dict[str, str]:
    return {f.name: hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(folder.iterdir()) if f.is_file()}


# ----------------------------------------------------------------------------------------------
# Schema (AC1, AC2, AC3, AC4, AC5)
# ----------------------------------------------------------------------------------------------

class SchemaTests(unittest.TestCase):
    def test_schema_defines_exactly_18_types_numbered_1_to_18(self):
        names = SCHEMA.type_names()
        self.assertEqual(18, len(names))
        self.assertEqual(list(range(1, 19)), [SCHEMA.types[t]["n"] for t in names])
        for t in names:
            spec = SCHEMA.types[t]
            for key in ("src", "source", "once", "dedupe", "fields"):
                self.assertIn(key, spec, t)
            for f in spec["dedupe"]:
                self.assertTrue(f in spec["fields"] or f in SCHEMA.envelope, f"{t}.{f}")

    def test_schema_doc_table_matches_json(self):
        text = (tel_schema.REPO_ROOT / "Docs" / "Telemetry" / "Schema.vi.md").read_text(encoding="utf-8")
        section = text.split("## 3. 18 loại bản ghi", 1)[1].split("### 3.1", 1)[0]
        rows = re.findall(r"^\| (\d+) \| `(\w+)` \| ([^|]+)\| ([^|]+)\| ([^|]+)\| ([^|]+)\|$", section, re.M)
        self.assertEqual(18, len(rows))
        for n, t, src, _source, once, key in rows:
            self.assertIn(t, SCHEMA.types)
            spec = SCHEMA.types[t]
            self.assertEqual(int(n), spec["n"], t)
            self.assertTrue(src.strip().startswith(spec["src"]), t)
            self.assertEqual(once.strip() == "có", spec["once"], t)
            self.assertEqual([k.strip() for k in key.split(",")], spec["dedupe"], t)

    def test_code_ids_are_read_from_balance_files(self):
        weapons = json.loads((tel_schema.REPO_ROOT / SCHEMA.raw["idSets"]["weapon"]["source"]).read_text("utf-8"))
        self.assertEqual({w["id"] for w in weapons}, set(SCHEMA.id_sets["weapon"]))
        self.assertIn("HAND", SCHEMA.id_sets["weapon"])
        self.assertIn("COMPASS", SCHEMA.id_sets["equipment"])
        for g6 in SCHEMA.raw["extraIds"]:
            self.assertIn(g6, SCHEMA.all_ids)

    def test_balance_dir_override_changes_valid_ids(self):
        d = tmpdir("balance_override")
        (d / "weapons.json").write_text(json.dumps([{"id": "HAND"}]), encoding="utf-8")
        (d / "equipments.json").write_text(json.dumps([{"id": "GOD_LEAF"}]), encoding="utf-8")
        schema = tel_schema.load_schema(balance_dir=d)
        lines = tel_schema.read_lines(FIXTURES / "valid" / "attack.jsonl")
        _, errs = tel_schema.validate_line(schema, lines[1], 2)
        self.assertEqual(["invalid_id"], [e["code"] for e in errs])

    def test_every_type_has_a_valid_fixture_that_passes(self):
        for t in SCHEMA.type_names():
            path = FIXTURES / "valid" / f"{t}.jsonl"
            self.assertTrue(path.exists(), t)
            lines = tel_schema.read_lines(path)
            types = [json.loads(ln)["type"] for ln in lines]
            self.assertEqual("_header", types[0])
            self.assertEqual("_trailer", types[-1])
            self.assertIn(t, types)
            for i, ln in enumerate(lines, 1):
                _, errs = tel_schema.validate_line(SCHEMA, ln, i)
                self.assertEqual([], errs, f"{t}:{i}")

    def test_valid_fixtures_carry_full_envelope(self):
        required = [f for f, s in SCHEMA.envelope.items() if not s.get("optional")]
        self.assertGreaterEqual(len(required), 12)
        for t in SCHEMA.type_names():
            for ln in tel_schema.read_lines(FIXTURES / "valid" / f"{t}.jsonl"):
                r = json.loads(ln)
                if r["type"] == t:
                    self.assertEqual(required, [k for k in r if k in SCHEMA.envelope][:len(required)], t)
                    self.assertNotIn("fake", r)

    def test_invalid_fixtures_report_their_error_code(self):
        files = sorted((FIXTURES / "invalid").glob("*.jsonl"))
        self.assertGreater(len(files), 40)
        for f in files:
            code = f.stem.split("__")[0]
            self.assertIn(code, SCHEMA.error_codes, f.name)
            for i, ln in enumerate(tel_schema.read_lines(f), 1):
                _, errs = tel_schema.validate_line(SCHEMA, ln, i)
                self.assertIn(code, [e["code"] for e in errs], f.name)

    def test_every_error_code_has_an_invalid_fixture(self):
        used = {f.stem.split("__")[0] for f in (FIXTURES / "invalid").glob("*.jsonl")}
        self.assertEqual(set(SCHEMA.error_codes), used)

    def test_each_missing_envelope_field_is_rejected(self):
        required = [f for f, s in SCHEMA.envelope.items() if not s.get("optional")]
        for f in required:
            path = FIXTURES / "invalid" / f"missing_field__{f}.jsonl"
            self.assertTrue(path.exists(), f)
            _, errs = tel_schema.validate_line(SCHEMA, tel_schema.read_lines(path)[0])
            self.assertIn(("missing_field", f), [(e["code"], e["field"]) for e in errs])

    def test_every_enum_field_has_an_out_of_domain_fixture(self):
        for t, f in SCHEMA.enum_fields():
            name = f"envelope.{f}" if t == "*envelope*" else f"{t}.{f}"
            path = FIXTURES / "invalid" / f"bad_enum__{name}.jsonl"
            self.assertTrue(path.exists(), name)
            _, errs = tel_schema.validate_line(SCHEMA, tel_schema.read_lines(path)[0])
            self.assertIn(("bad_enum", f), [(e["code"], e["field"]) for e in errs], name)

    def test_bad_id_is_flagged_invalid_id(self):
        _, errs = tel_schema.validate_line(SCHEMA, tel_schema.read_lines(FIXTURES / "invalid" / "invalid_id__attack.weapon.jsonl")[0])
        self.assertEqual([("invalid_id", "weapon")], [(e["code"], e["field"]) for e in errs])

    def test_nullfor_allows_null_only_for_listed_types(self):
        rec = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "damage.jsonl")[1])
        rec["matchId"] = None
        self.assertIn("null_not_allowed", [e["code"] for e in tel_schema.validate_record(SCHEMA, rec)])
        perf = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "perf_server.jsonl")[1])
        self.assertIsNone(perf["matchId"])
        self.assertEqual([], tel_schema.validate_record(SCHEMA, perf))
        perf["cfg"], perf["cfgHash"] = "1.0.0", "a" * 64   # perflog may fill cfg when given
        self.assertEqual([], tel_schema.validate_record(SCHEMA, perf))

    def test_pii_scanner_catches_keys_patterns_and_samples(self):
        hide = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "hide_span.jsonl")[1])
        self.assertEqual([], tel_schema.validate_record(SCHEMA, hide, pii_samples=["Tester Alpha"]))
        hide["bush"] = "Tester Alpha"
        errs = tel_schema.validate_record(SCHEMA, hide, pii_samples=["Tester Alpha"])
        self.assertEqual(["pii"], [e["code"] for e in errs])
        for key in ("DisplayName", "roomcode", "JWT", "Ip"):
            r = dict(json.loads(tel_schema.read_lines(FIXTURES / "valid" / "damage.jsonl")[1]), **{key: "x"})
            self.assertIn("pii", [e["code"] for e in tel_schema.validate_record(SCHEMA, r)], key)
        nested = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "loot_drop.jsonl")[1])
        nested["items"][0]["token"] = "x"
        self.assertIn("pii", [e["code"] for e in tel_schema.validate_record(SCHEMA, nested)])
        for name in ("pii__jwt_value", "pii__ipv4_value", "pii__ipv6_value", "pii__displayName", "pii__roomCode"):
            _, errs = tel_schema.validate_line(SCHEMA, tel_schema.read_lines(FIXTURES / "invalid" / f"{name}.jsonl")[0])
            self.assertIn("pii", [e["code"] for e in errs], name)

    def test_version_strings_are_not_mistaken_for_ip(self):
        rec = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "damage.jsonl")[1])
        rec["build"] = "0.4.12-rc1"
        rec["cfg"] = "1.0.0"
        self.assertEqual([], tel_schema.validate_record(SCHEMA, rec))

    def test_validator_never_raises_on_garbage(self):
        rng = random.Random(7)
        samples = ["", "null", "[]", "42", '"x"', "{", '{"type":5}', '{"type":"damage","items":{}}',
                   '{"type":"loot_drop","items":[1,2,{"id":null}]}', '{"type":"_header","matchId":[]}',
                   "{" * 2000 + "}" * 2000]
        base = tel_schema.read_lines(FIXTURES / "valid" / "elimination.jsonl")[1]
        for _ in range(200):
            chars = list(base)
            for _ in range(rng.randint(1, 6)):
                chars[rng.randrange(len(chars))] = rng.choice('{}[]",:0aZ-.')
            samples.append("".join(chars))
        for s in samples:
            rec, errs = tel_schema.validate_line(SCHEMA, s, 1)
            self.assertIsInstance(errs, list)
            if rec is None or not isinstance(rec, dict):
                self.assertTrue(errs)

    def test_ordered_puts_envelope_first(self):
        rec = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "damage.jsonl")[1])
        shuffled = dict(reversed(list(rec.items())))
        self.assertEqual(list(rec), list(tel_schema.ordered(SCHEMA, shuffled)))
        self.assertEqual(SCHEMA.field_order("damage"), list(tel_schema.ordered(SCHEMA, dict(shuffled, fake=True))))


class ValidateCliTests(unittest.TestCase):
    def test_valid_fixtures_exit_zero_with_strict(self):
        r = run_cli("validate", "--strict", FIXTURES / "valid")
        self.assertEqual(0, r.returncode, r.stdout + r.stderr)
        self.assertIn("errors=0", r.stdout)

    def test_invalid_fixtures_exit_4_with_strict_only(self):
        self.assertEqual(4, run_cli("validate", "--strict", FIXTURES / "invalid").returncode)
        self.assertEqual(0, run_cli("validate", FIXTURES / "invalid").returncode)

    def test_pii_sample_option(self):
        d = tmpdir("cli_pii")
        rec = json.loads(tel_schema.read_lines(FIXTURES / "valid" / "hide_span.jsonl")[1])
        rec["bush"] = "QX7K2M"
        (d / "x.jsonl").write_text(tel_schema.dumps(rec) + "\n", encoding="utf-8")
        self.assertEqual(0, run_cli("validate", "--strict", d).returncode)
        r = run_cli("validate", "--strict", "--pii-sample", "QX7K2M", d)
        self.assertEqual(4, r.returncode)
        self.assertIn("pii", r.stdout)

    def test_usage_errors_exit_1(self):
        self.assertEqual(1, run_cli().returncode)
        self.assertEqual(1, run_cli("validate").returncode)
        self.assertEqual(1, run_cli("validate", Path(_TMP) / "does_not_exist").returncode)
        self.assertEqual(1, run_cli("fake", "--out", Path(_TMP) / "x", "--sample-hz", "5").returncode)


# ----------------------------------------------------------------------------------------------
# Fake generator (AC20, AC21 shape, AC22, AC2/AC3/AC4 on fake data)
# ----------------------------------------------------------------------------------------------

class FakeGeneratorTests(unittest.TestCase):
    def test_same_seed_gives_byte_identical_files(self):
        a = tmpdir("det_a")
        b = tmpdir("det_b")
        tel_fake.generate(a, seed=1, schema=SCHEMA)
        tel_fake.generate(b, seed=1, schema=SCHEMA)
        ha, hb = file_hashes(a), file_hashes(b)
        self.assertEqual(ha, hb)
        self.assertGreater(len(ha), 60)
        clean, _ = fake_run()
        self.assertEqual(file_hashes(clean), ha)

    def test_other_seed_or_cfg_differs(self):
        clean, m = fake_run()
        _, m2 = fake_run("seed2", seed=2)
        _, mb = fake_run("cfgB", cfg="B")
        self.assertNotEqual(m["matchIds"], m2["matchIds"])
        self.assertNotEqual(m["matchIds"], mb["matchIds"])
        self.assertNotEqual(m["cfgHash"], mb["cfgHash"])

    def test_every_match_has_all_18_types(self):
        for name, kw in (("clean", {}), ("cfgB", {"cfg": "B"})):
            folder, m = fake_run(name, **kw)
            recs = records_of(folder)
            self.assertEqual(6, len(m["matchIds"]))
            spans = {}
            by_match = defaultdict(set)
            for r in recs:
                if r["matchId"]:
                    by_match[r["matchId"]].add(r["type"])
                if r["type"] == "match_start":
                    spans.setdefault(r["matchId"], [0, 0])[0] = r["utc"]
                if r["type"] == "match_end":
                    spans.setdefault(r["matchId"], [0, 0])[1] = r["utc"]
            for r in recs:
                if r["type"] == "perf_server":
                    for mid, (s, e) in spans.items():
                        if s <= r["utc"] <= e + r["windowSec"] * 1000:
                            by_match[mid].add("perf_server")
            for mid in m["matchIds"]:
                self.assertEqual(set(SCHEMA.type_names()), by_match[mid], f"{name} {mid}")

    def test_fake_output_is_schema_valid_without_invalid_ids(self):
        folder, _ = fake_run()
        s = tel_schema.validate_files(SCHEMA, [folder])
        self.assertEqual(0, s["errors"], s["examples"])
        self.assertNotIn("invalid_id", s["byCode"])
        self.assertEqual(s["records"], s["fake"])

    def test_every_line_is_marked_fake(self):
        folder, _ = fake_run()
        for r in records_of(folder, aux=True):
            self.assertIs(True, r.get("fake"), r["type"])

    def test_no_pii_in_fake_output(self):
        folder, _ = fake_run()
        secrets = list(tel_fake.FAKE_DISPLAY_NAMES) + [tel_fake.FAKE_ROOM_CODE]
        for f in sorted(folder.iterdir()):
            data = f.read_text(encoding="utf-8")
            for s in secrets:
                self.assertNotIn(s, data, f.name)
        s = tel_schema.validate_files(SCHEMA, [folder], pii_samples=secrets)
        self.assertNotIn("pii", s["byCode"])
        for t, spec in SCHEMA.types.items():
            for f in spec["fields"]:
                self.assertNotIn(f.lower(), SCHEMA.pii_keys, f"{t}.{f}")

    def test_clean_files_have_contiguous_seq_and_matching_trailer(self):
        folder, m = fake_run()
        total = 0
        for f in tel_schema.expand_inputs([folder]):
            lines = [json.loads(x) for x in tel_schema.read_lines(f)]
            self.assertEqual("_header", lines[0]["type"])
            self.assertEqual("_trailer", lines[-1]["type"])
            body = lines[1:-1]
            self.assertEqual(list(range(1, len(body) + 1)), [r["seq"] for r in body], f.name)
            self.assertEqual(len(body), lines[-1]["written"])
            self.assertEqual(1, len({(r["matchId"], r["src"], r["wid"]) for r in body}), f.name)
            total += len(body)
        self.assertEqual(m["records"], total)
        self.assertEqual(m["records"], m["expectedMerged"])

    def test_cfg_b_changes_cfg_hash_and_damage(self):
        a, _ = fake_run()
        b, _ = fake_run("cfgB", cfg="B")
        def raws(folder):
            out = defaultdict(set)
            for r in records_of(folder):
                if r["type"] == "damage" and r["cause"] == "WEAPON":
                    out[r["weapon"]].add(r["raw"])
            return out
        ra, rb = raws(a), raws(b)
        base = {"KNIFE": 22, "AXE": 42, "CROSSBOW": 34}
        seen = [w for w in tel_fake.B_BOOSTED if w in ra and w in rb]
        self.assertTrue(seen)
        for w in seen:
            self.assertEqual({base[w]}, ra[w], w)
            self.assertEqual({int(round(base[w] * 1.5))}, rb[w], w)
        for w in set(ra) & set(rb) - set(tel_fake.B_BOOSTED):
            self.assertEqual(ra[w], rb[w], w)
        self.assertNotEqual({r["cfgHash"] for r in records_of(a) if r["cfgHash"]},
                            {r["cfgHash"] for r in records_of(b) if r["cfgHash"]})

    def test_injection_counts_are_recorded(self):
        _, m = fake_run("dup", dup=5.0, **M2)
        self.assertEqual(int(round(m["records"] * 0.05)), m["inject"]["dup"])
        _, m = fake_run("conflict", conflict=3, **M2)
        self.assertEqual(3, m["inject"]["conflict"])
        _, m = fake_run("sem", semantic_dup=6, **M2)
        self.assertEqual(6, m["inject"]["semanticDup"])
        self.assertEqual(m["records"] - 6, m["expectedMerged"])
        _, m = fake_run("gap7", gap=7, **M2)
        self.assertEqual(7, m["inject"]["gap"])

    def test_dup_and_reorder_keep_line_set(self):
        clean, _ = fake_run("clean2", **M2)
        dup, m = fake_run("dup", dup=5.0, **M2)
        reo, _ = fake_run("reorder", reorder=True, **M2)
        def lines(folder):
            return Counter(ln for f in tel_schema.expand_inputs([folder]) for ln in tel_schema.read_lines(f))
        self.assertEqual(lines(clean), lines(reo))
        diff = lines(dup) - lines(clean)
        self.assertEqual(m["inject"]["dup"], sum(diff.values()))
        self.assertFalse(lines(clean) - lines(dup))

    def test_reconnect_scenario_shape(self):
        folder, m = fake_run("reconnect", reconnect=True, **M2)
        rc = m["inject"]["reconnect"]
        self.assertIsNotNone(rc)
        recs = [r for r in records_of(folder) if r["matchId"] == rc["matchId"]]
        kinds = Counter((r["netId"], r["kind"]) for r in recs if r["type"] == "session_state")
        self.assertEqual(1, kinds[(rc["netId"], "RESUMED")])
        self.assertEqual(1, kinds[(rc["netId"], "DROPPED")])
        self.assertEqual(1, sum(1 for r in recs if r["type"] == "match_start"))
        self.assertEqual(10, len({r["netId"] for r in recs if r["type"] == "player_spawn"}))
        self.assertEqual(10, sum(1 for r in recs if r["type"] == "player_spawn"))
        client = sorted(r["seq"] for r in recs if r["src"] == "client" and r["netId"] == rc["netId"])
        self.assertEqual(list(range(1, len(client) + 1)), client)
        self.assertGreater(rc["removedClient"], 0)
        _, clean = fake_run("clean2", **M2)
        self.assertEqual(clean["records"] + 2 - rc["removedClient"], m["expectedMerged"])

    def test_fake_cli(self):
        d = Path(_TMP) / "cli_fake"
        r = run_cli("fake", "--out", d, "--matches", "1", "--seed", "3", "--dup", "--conflict")
        self.assertEqual(0, r.returncode, r.stderr)
        self.assertIn("conflict=3", r.stdout)
        manifest = json.loads((d / "inject_manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(1, manifest["matches"])


# ----------------------------------------------------------------------------------------------
# Merge: duplicates, conflicts, semantic duplicates, reconnect, gaps, order (AC6-AC11, AC21, AC22)
# ----------------------------------------------------------------------------------------------

def merged(name="clean", **kw) -> tuple[dict, dict, Path]:
    """(merge result, inject manifest, output folder) for a cached fake run."""
    key = ("merged", name, tuple(sorted(kw.items())))
    if key not in _CACHE:
        folder, manifest = fake_run(name, **kw)
        out = tmpdir(f"merge_{len(_CACHE)}_{name}")
        result = tel_merge.merge([folder], SCHEMA)
        tel_merge.write_outputs(result, out)
        _CACHE[key] = (result, manifest, out)
    return _CACHE[key]


def copy_lines(src: Path, dst: Path, transform) -> Path:
    """Copy every *.jsonl of src into dst, passing each file's line list through transform(name, lines)."""
    dst.mkdir(parents=True, exist_ok=True)
    for f in tel_schema.expand_inputs([src]):
        lines = transform(f.name, tel_schema.read_lines(f))
        if lines is not None:
            (dst / f.name).write_bytes(("\n".join(lines) + "\n").encode("utf-8"))
    return dst


class MergeTests(unittest.TestCase):
    def test_clean_run_has_no_findings(self):
        r, m, _ = merged("clean2", **M2)
        s = r["summary"]
        self.assertEqual(m["records"], s["events"])
        for k in ("invalid", "dup", "conflicts", "semanticDup", "gaps", "droppedQueue", "droppedIo"):
            self.assertEqual(0, s[k], k)
        for k in ("trailerMismatch", "unterminatedFiles", "unterminatedGroups", "truncated", "gapGroups"):
            self.assertEqual([], s[k], k)
        self.assertEqual(len(m["matchIds"]), len(s["matches"]))

    def test_technical_duplicates_keep_one(self):
        clean, _, clean_out = merged("clean2", **M2)
        r, m, out = merged("dup", dup=5.0, **M2)
        self.assertGreater(m["inject"]["dup"], 0)
        self.assertEqual(m["inject"]["dup"], r["summary"]["dup"])
        self.assertEqual(clean["summary"]["events"], r["summary"]["events"])
        self.assertEqual((clean_out / "events.jsonl").read_bytes(), (out / "events.jsonl").read_bytes())
        self.assertEqual(0, r["summary"]["gaps"])
        self.assertEqual([], r["summary"]["trailerMismatch"])

    def test_conflicts_are_listed_not_merged_silently(self):
        r, m, out = merged("conflict", conflict=3, **M2)
        self.assertEqual(3, r["summary"]["conflicts"])
        rows = [json.loads(x) for x in tel_schema.read_lines(out / "conflicts.jsonl")]
        self.assertEqual(3, len(rows))
        for row in rows:
            self.assertEqual(2, len(row["variants"]))
            self.assertEqual(1, len({(v["matchId"], v["src"], v["wid"], v["seq"]) for v in row["variants"]}))
        self.assertEqual(m["records"], r["summary"]["events"])
        folder, _ = fake_run("conflict", conflict=3, **M2)
        self.assertEqual(2, run_cli("merge", folder, "--out", Path(_TMP) / "cli_conflict", "--strict").returncode)
        self.assertEqual(0, run_cli("merge", folder, "--out", Path(_TMP) / "cli_conflict2").returncode)

    def test_semantic_duplicates_count_and_drop(self):
        clean, _, _ = merged("clean2", **M2)
        r, m, _ = merged("sem", semantic_dup=6, **M2)
        self.assertEqual(6, r["summary"]["semanticDup"])
        self.assertEqual(m["expectedMerged"], r["summary"]["events"])
        self.assertEqual(0, r["summary"]["gaps"])
        for t in SCHEMA.once_types():
            self.assertEqual(clean["summary"]["byType"][t], r["summary"]["byType"][t], t)
        elim = Counter((e["matchId"], e["victim"]) for e in r["events"] if e["type"] == "elimination")
        self.assertEqual({1}, set(elim.values()))

    def test_reconnect_keeps_one_spawn_one_resume_and_contiguous_seq(self):
        r, m, _ = merged("reconnect", reconnect=True, **M2)
        rc = m["inject"]["reconnect"]
        s = r["summary"]
        self.assertEqual(m["expectedMerged"], s["events"])
        self.assertEqual(0, s["gaps"])
        self.assertEqual(0, s["semanticDup"])
        ev = [e for e in r["events"] if e["matchId"] == rc["matchId"]]
        self.assertEqual(1, sum(e["type"] == "match_start" for e in ev))
        self.assertEqual(1, sum(e["type"] == "player_spawn" and e["netId"] == rc["netId"] for e in ev))
        kinds = [e["kind"] for e in ev if e["type"] == "session_state" and e["netId"] == rc["netId"]]
        self.assertEqual(["DROPPED", "RESUMED"], kinds)
        seqs = sorted(e["seq"] for e in ev if e["src"] == "client" and e["netId"] == rc["netId"])
        self.assertEqual(list(range(1, len(seqs) + 1)), seqs)

    def test_gaps_report_exact_missing_count(self):
        for k in (1, 7, 40):
            r, m, _ = merged(f"gap{k}", gap=k, **M2)
            s = r["summary"]
            self.assertEqual(k, m["inject"]["gap"])
            self.assertEqual(k, s["gaps"], k)
            self.assertEqual(k, sum(g["missing"] for g in s["gapGroups"]))
            self.assertEqual(k, sum(t["written"] - t["distinct"] for t in s["trailerMismatch"]))
            self.assertEqual(m["expectedMerged"], s["events"])

    def test_gap_at_the_end_of_a_file_is_found_through_the_trailer(self):
        folder, _ = fake_run("clean2", **M2)

        def drop_last(name, lines):
            return lines[:-2] + lines[-1:] if name.startswith("perf_server") else lines
        r = tel_merge.merge([copy_lines(folder, tmpdir("gap_tail"), drop_last)], SCHEMA)
        self.assertEqual(1, r["summary"]["gaps"])

    def test_trailer_written_equals_lines_on_clean_run(self):
        folder, _ = fake_run("clean2", **M2)
        for f in tel_schema.expand_inputs([folder]):
            lines = tel_schema.read_lines(f)
            self.assertEqual(len(lines) - 2, json.loads(lines[-1])["written"], f.name)

    def test_missing_trailer_is_unterminated_but_merged(self):
        folder, m = fake_run("clean2", **M2)
        victim = sorted(f.name for f in folder.glob("*.server.*.jsonl"))[0]

        def cut(name, lines):
            return lines[:-1] if name == victim else lines
        r = tel_merge.merge([copy_lines(folder, tmpdir("unterminated"), cut)], SCHEMA)
        self.assertEqual([victim], r["summary"]["unterminatedFiles"])
        self.assertEqual(1, len(r["summary"]["unterminatedGroups"]))
        self.assertEqual(m["records"], r["summary"]["events"])

    def test_order_of_lines_and_files_does_not_change_output(self):
        clean, _, clean_out = merged("clean2", **M2)
        _, _, reo_out = merged("reorder", reorder=True, **M2)
        expect_events = (clean_out / "events.jsonl").read_bytes()
        expect_summary = (clean_out / "merge_summary.json").read_bytes()
        self.assertEqual(expect_events, (reo_out / "events.jsonl").read_bytes())
        folder, _ = fake_run("clean2", **M2)
        for i in range(5):
            rng = random.Random(100 + i)

            def shuffle(name, lines):
                lines = list(lines)
                rng.shuffle(lines)          # header and trailer move too
                return lines
            src = copy_lines(folder, tmpdir(f"shuffle_{i}"), shuffle)
            paths = sorted(src.iterdir())
            rng.shuffle(paths)
            out = tmpdir(f"shuffle_out_{i}")
            tel_merge.write_outputs(tel_merge.merge(paths, SCHEMA), out)
            self.assertEqual(expect_events, (out / "events.jsonl").read_bytes(), i)
            self.assertEqual(expect_summary, (out / "merge_summary.json").read_bytes(), i)

    def test_output_sorted_by_tick_src_seq(self):
        r, _, _ = merged("clean2", **M2)
        ticked = [e for e in r["events"] if e["tick"] is not None]
        keys = [(e["tick"], e["src"], e["seq"], e["wid"]) for e in ticked]
        self.assertEqual(sorted(keys), keys)
        tail = r["events"][len(ticked):]
        self.assertTrue(tail and all(e["type"] == "perf_server" for e in tail))
        self.assertEqual(sorted(e["utc"] for e in tail), [e["utc"] for e in tail])

    def test_all_faults_together(self):
        kw = dict(dup=5.0, gap=7, conflict=3, semantic_dup=6, reorder=True, reconnect=True)
        r, m, _ = merged("all", **kw, **M2)
        s = r["summary"]
        inj = m["inject"]
        self.assertEqual((inj["dup"], inj["conflict"], inj["semanticDup"], inj["gap"]),
                         (s["dup"], s["conflicts"], s["semanticDup"], s["gaps"]))
        self.assertEqual(m["expectedMerged"], s["events"])

    def test_invalid_lines_are_counted_and_skipped(self):
        d = tmpdir("merge_invalid")
        shutil.copy(FIXTURES / "valid" / "damage.jsonl", d / "a.jsonl")
        shutil.copy(FIXTURES / "invalid" / "invalid_id__attack.weapon.jsonl", d / "b.jsonl")
        (d / "c.jsonl").write_text("not json\n", encoding="utf-8")
        s = tel_merge.merge([d], SCHEMA)["summary"]
        self.assertEqual(2, s["invalid"])
        self.assertEqual({"bad_json": 1, "invalid_id": 1}, s["invalidByCode"])
        self.assertEqual(2, s["events"])
        self.assertEqual(0, s["fake"])

    def test_same_file_read_twice_is_all_duplicates(self):
        d = tmpdir("twice")
        shutil.copy(FIXTURES / "valid" / "perf_server.jsonl", d / "a.jsonl")
        shutil.copy(FIXTURES / "valid" / "perf_server.jsonl", d / "b.jsonl")
        s = tel_merge.merge([d], SCHEMA)["summary"]
        self.assertEqual((1, 1, 0, 0), (s["events"], s["dup"], s["conflicts"], s["gaps"]))

    def test_key_order_difference_is_a_duplicate_not_a_conflict(self):
        d = tmpdir("keyorder")
        lines = tel_schema.read_lines(FIXTURES / "valid" / "attack.jsonl")
        rec = json.loads(lines[1])
        (d / "a.jsonl").write_text("\n".join(lines) + "\n", encoding="utf-8")
        (d / "b.jsonl").write_text(json.dumps(dict(reversed(list(rec.items())))) + "\n", encoding="utf-8")
        s = tel_merge.merge([d], SCHEMA)["summary"]
        self.assertEqual((1, 0), (s["dup"], s["conflicts"]))

    def test_require_real_exit_codes(self):
        folder, _ = fake_run("clean2", **M2)
        self.assertEqual(3, run_cli("merge", folder, "--out", Path(_TMP) / "rr1", "--require-real").returncode)
        self.assertEqual(0, run_cli("merge", FIXTURES / "valid" / "damage.jsonl", "--out", Path(_TMP) / "rr2",
                                    "--require-real", "--strict").returncode)

    def test_merge_outputs_are_lf_utf8(self):
        _, _, out = merged("clean2", **M2)
        for name in ("events.jsonl", "conflicts.jsonl", "merge_summary.json"):
            data = (out / name).read_bytes()
            self.assertNotIn(b"\r", data, name)


# ----------------------------------------------------------------------------------------------
# Tooling rules (AC30)
# ----------------------------------------------------------------------------------------------

class ToolingTests(unittest.TestCase):
    def test_only_stdlib_imports(self):
        local = {p.stem for p in HERE.glob("*.py")}
        for path in sorted(HERE.rglob("*.py")):
            tree = ast.parse(path.read_text(encoding="utf-8"))
            for node in ast.walk(tree):
                names = []
                if isinstance(node, ast.Import):
                    names = [a.name.split(".")[0] for a in node.names]
                elif isinstance(node, ast.ImportFrom) and node.level == 0:
                    names = [node.module.split(".")[0]]
                for n in names:
                    self.assertTrue(n in sys.stdlib_module_names or n in local, f"{path.name}: {n}")

    def test_scripts_use_lf_and_utf8(self):
        for path in sorted(HERE.rglob("*")):
            if path.is_file() and path.suffix in (".py", ".jsonl", ".md", ".csv"):
                data = path.read_bytes()
                self.assertNotIn(b"\r\n", data, path.name)
                data.decode("utf-8")


if __name__ == "__main__":
    unittest.main()
