"""TEL-01 telemetry tools: one CLI for the offline Python side (stdlib only, run with `python -I`).

    python -I Tools/Telemetry/telemetry.py validate [--strict] <file|dir>...
    python -I Tools/Telemetry/telemetry.py fake --out DIR [--seed 1] [--cfg A|B] [fault flags]

Exit codes (TEL-01 plan 4.2): 0 OK (also with partial data), 1 usage error, 2 conflicts with
--strict, 3 fake data with --require-real, 4 schema errors with validate --strict.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import tel_schema  # noqa: E402

EXIT_OK, EXIT_USAGE, EXIT_CONFLICT, EXIT_FAKE, EXIT_SCHEMA = 0, 1, 2, 3, 4


class _Parser(argparse.ArgumentParser):
    def error(self, message):  # argparse would exit 2, which is reserved for conflicts
        self.print_usage(sys.stderr)
        print(f"{self.prog}: error: {message}", file=sys.stderr)
        raise SystemExit(EXIT_USAGE)


def _schema(args):
    return tel_schema.load_schema(args.schema, args.balance_dir)


def cmd_validate(args) -> int:
    schema = _schema(args)
    summary = tel_schema.validate_files(schema, args.paths, pii_samples=args.pii_sample or ())
    if args.json:
        print(json.dumps(summary, indent=2, ensure_ascii=False))
    else:
        print(f"files={summary['files']} lines={summary['lines']} records={summary['records']} "
              f"errors={summary['errors']} fake={summary['fake']}")
        for code, n in summary["byCode"].items():
            print(f"  {code}: {n}")
        for e in summary["examples"]:
            print(f"  {e['file']}:{e['line']} {e['code']} {e['field']} {e['detail']}")
    if args.strict and summary["errors"]:
        return EXIT_SCHEMA
    return EXIT_OK


def cmd_fake(args) -> int:
    import tel_fake
    schema = _schema(args)
    m = tel_fake.generate(args.out, seed=args.seed, cfg=args.cfg, matches=args.matches, players=args.players,
                          sample_hz=args.sample_hz, schema=schema, dup=args.dup or 0.0, gap=args.gap or 0,
                          reorder=args.reorder, conflict=args.conflict or 0, semantic_dup=args.semantic_dup or 0,
                          reconnect=args.reconnect)
    inj = m["inject"]
    print(f"fake seed={m['seed']} cfg={m['cfg']} matches={m['matches']} files={m['files']} records={m['records']} "
          f"expectedMerged={m['expectedMerged']} dup={inj['dup']} conflict={inj['conflict']} "
          f"semanticDup={inj['semanticDup']} gap={inj['gap']} reorder={inj['reorder']} "
          f"reconnect={'yes' if inj['reconnect'] else 'no'}")
    return EXIT_OK


def build_parser() -> argparse.ArgumentParser:
    p = _Parser(prog="telemetry.py", description="TEL-01 telemetry tools (validate, fake).")
    p.add_argument("--schema", help="schema JSON (default Docs/Telemetry/telemetry.schema.v1.json)")
    p.add_argument("--balance-dir", help="folder with weapons.json/equipments.json (default from schema idSets)")
    sub = p.add_subparsers(dest="cmd", required=True, parser_class=_Parser)

    v = sub.add_parser("validate", help="check JSONL files against the schema")
    v.add_argument("paths", nargs="+", help="files or folders (*.jsonl, recursive)")
    v.add_argument("--strict", action="store_true", help="exit 4 when any line has an error")
    v.add_argument("--pii-sample", action="append", metavar="TEXT",
                   help="extra string that must never appear (display name, room code); repeatable")
    v.add_argument("--json", action="store_true", help="print the summary as JSON")
    v.set_defaults(func=cmd_validate)

    f = sub.add_parser("fake", help="write a deterministic fake run (all 18 types, fake=true)")
    f.add_argument("--out", required=True, help="output folder")
    f.add_argument("--seed", type=int, default=1)
    f.add_argument("--cfg", choices=("A", "B"), default="A", help="B: KNIFE/AXE/CROSSBOW damage x1.5")
    f.add_argument("--matches", type=int, default=6)
    f.add_argument("--players", type=int, default=10)
    f.add_argument("--sample-hz", type=float, default=1.0, help="position_sample rate (0.5..2)")
    f.add_argument("--dup", type=float, nargs="?", const=5.0, metavar="PCT", help="duplicate PCT %% of lines (5)")
    f.add_argument("--gap", type=int, metavar="K", help="remove K record lines")
    f.add_argument("--reorder", action="store_true", help="shuffle record lines inside each file")
    f.add_argument("--conflict", type=int, nargs="?", const=3, metavar="N", help="N same-key conflicts (3)")
    f.add_argument("--semantic-dup", type=int, nargs="?", const=6, metavar="N",
                   help="N 'once' records reported twice (6)")
    f.add_argument("--reconnect", action="store_true", help="client drops and resumes within grace in match 0")
    f.set_defaults(func=cmd_fake)
    return p


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    if getattr(args, "sample_hz", 1.0) is not None and not 0.5 <= getattr(args, "sample_hz", 1.0) <= 2.0:
        print("telemetry.py: error: --sample-hz must be within 0.5..2", file=sys.stderr)
        return EXIT_USAGE
    try:
        return args.func(args)
    except (FileNotFoundError, ValueError) as exc:
        print(f"telemetry.py: error: {exc}", file=sys.stderr)
        return EXIT_USAGE


if __name__ == "__main__":
    sys.exit(main())
