"""TEL-01 merge: combine telemetry files from many sources, drop duplicates, find conflicts and gaps.

Rules (ticket 2.8, plan 4.3):
  1. Every line is validated; invalid lines are counted and left out.
  2. Technical key (matchId, src, wid, seq): same content -> keep one, count `dup`;
     different content -> `conflict` (all variants go to conflicts.jsonl, the variant with the smallest
     canonical JSON stays in events.jsonl so the result does not depend on input order).
  3. Semantic key (`dedupe` of the type) for types with once=true: first by (tick, src, seq, wid) is kept,
     later ones are counted as `semantic_dup` and left out.
  4. seq holes per (matchId, src, wid): expected 1..max(max seq, _trailer.written).
  5. `_trailer.written` is compared with the distinct seq count; files without `_trailer` are `unterminated`.
  6. Output sorted by (tick, src, seq, wid); perf_server (tick null) follows, ordered by utc.
The result is independent of file order and line order (AC11).
"""
from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path

import tel_schema

MAX_EXAMPLES = 20


def _group(rec):
    return (rec.get("matchId"), rec.get("src"), rec.get("wid"))


def sort_key(rec):
    """(tick, src, seq, wid); records without tick (perf_server) go last, by utc. Records are validated."""
    tick = rec["tick"]
    return (tick is None, tick if tick is not None else rec["utc"], rec["src"], rec["seq"], rec["wid"],
            rec["matchId"] or "", rec["type"])


def _ranges(missing: list[int], limit=10) -> list[str]:
    out, start, prev = [], None, None
    for s in missing:
        if start is None:
            start = prev = s
        elif s == prev + 1:
            prev = s
        else:
            out.append(f"{start}" if start == prev else f"{start}-{prev}")
            start = prev = s
    if start is not None:
        out.append(f"{start}" if start == prev else f"{start}-{prev}")
    return out[:limit]


def merge(paths, schema: tel_schema.Schema, pii_samples=()) -> dict:
    """Merge files/folders. Returns {"summary", "events", "conflicts"}; never writes files."""
    files = tel_schema.expand_inputs(paths)
    lines = invalid = 0
    invalid_by_code: dict[str, int] = defaultdict(int)
    invalid_examples = []
    # tech key -> [first text, first record, count of identical texts, {canonical: [record, count]} or None]
    occurrences: dict[tuple, list] = {}
    text_key: dict[str, tuple] = {}     # exact line already accepted -> its tech key (skips re-validation)
    trailers: dict[tuple, list[dict]] = defaultdict(list)
    headers: dict[tuple, int] = defaultdict(int)
    unterminated_files = []
    for f in files:
        has_records = has_trailer = False
        for i, text in enumerate(tel_schema.read_lines(f), start=1):
            lines += 1
            known = text_key.get(text)
            if known is not None:
                has_records = True
                occurrences[known][2] += 1
                continue
            rec, errs = tel_schema.validate_line(schema, text, i, pii_samples)
            if errs:
                invalid += 1
                for e in errs:
                    invalid_by_code[e["code"]] += 1
                invalid_examples.append({"file": f.name, "line": i, "code": errs[0]["code"],
                                         "field": errs[0]["field"]})
                continue
            rtype = rec["type"]
            if rtype == "_header":
                headers[_group(rec)] += 1
                continue
            if rtype == "_trailer":
                has_trailer = True
                trailers[_group(rec)].append(rec)
                continue
            has_records = True
            key = (rec["matchId"], rec["src"], rec["wid"], rec["seq"])
            slot = occurrences.get(key)
            if slot is None:
                occurrences[key] = [text, rec, 1, None]
                text_key[text] = key
                continue
            if slot[3] is None:
                slot[3] = {tel_schema.canonical(slot[1]): [slot[1], slot[2]]}
                slot[2] = 0
            variants = slot[3]
            canon = tel_schema.canonical(rec)
            if canon in variants:
                variants[canon][1] += 1
            else:
                variants[canon] = [rec, 1]
        if has_records and not has_trailer:
            unterminated_files.append(f.name)

    dup = 0
    conflicts = []
    kept = []
    for key in sorted(occurrences, key=lambda k: (k[0] or "", k[1] or "", k[2] or "", k[3])):
        _, first, same, variants = occurrences[key]
        if variants is None:
            dup += same - 1
            kept.append(first)
            continue
        variants[tel_schema.canonical(first)][1] += same     # identical texts seen after the split
        dup += sum(c for _, c in variants.values()) - len(variants)
        canons = sorted(variants)
        kept.append(variants[canons[0]][0])
        if len(canons) > 1:
            conflicts.append({"key": {"matchId": key[0], "src": key[1], "wid": key[2], "seq": key[3]},
                              "variants": [variants[c][0] for c in canons]})

    # seq holes and trailer check, on distinct technical keys (semantic duplicates occupy a seq)
    seqs: dict[tuple, list[int]] = defaultdict(list)
    for rec in kept:
        seqs[_group(rec)].append(rec["seq"])
    gap_groups, trailer_mismatch, unterminated_groups, truncated = [], [], [], []
    dropped_queue = dropped_io = 0
    gaps = 0
    for g in sorted(set(seqs) | set(trailers), key=lambda k: (k[0] or "", k[1] or "", k[2] or "")):
        present = sorted(seqs.get(g, []))
        tr = trailers.get(g, [])
        written = max((t["written"] for t in tr), default=0)
        top = max(present[-1] if present else 0, written)
        have = set(present)
        missing = [s for s in range(1, top + 1) if s not in have]
        label = {"matchId": g[0], "src": g[1], "wid": g[2]}
        if missing:
            gaps += len(missing)
            gap_groups.append(dict(label, missing=len(missing), ranges=_ranges(missing)))
        if tr:
            if written != len(present):
                trailer_mismatch.append(dict(label, written=written, distinct=len(present)))
            # a file read twice gives the same trailer twice: count drops once per distinct trailer
            distinct = {tel_schema.canonical(t): t for t in tr}
            for t in distinct.values():
                dropped_queue += t["droppedQueue"]
                dropped_io += t["droppedIo"]
                if t["truncated"]:
                    truncated.append(label)
        elif present:
            unterminated_groups.append(label)

    kept.sort(key=sort_key)
    events = []
    semantic = 0
    semantic_examples = []
    seen: set[str] = set()
    once = {t for t in schema.once_types()}
    for rec in kept:
        if rec["type"] in once:
            skey = json.dumps([rec["type"]] + [rec.get(f) for f in schema.types[rec["type"]]["dedupe"]])
            if skey in seen:
                semantic += 1
                if len(semantic_examples) < MAX_EXAMPLES:
                    semantic_examples.append({"type": rec["type"], "matchId": rec.get("matchId"),
                                              "tick": rec.get("tick"), "seq": rec["seq"], "wid": rec["wid"]})
                continue
            seen.add(skey)
        events.append(rec)

    by_type = {t: {"real": 0, "fake": 0} for t in schema.type_names()}
    for rec in events:
        by_type[rec["type"]]["fake" if rec.get("fake") is True else "real"] += 1
    fake = sum(v["fake"] for v in by_type.values())
    invalid_examples.sort(key=lambda e: (e["file"], e["line"]))
    summary = {
        "files": len(files), "lines": lines, "invalid": invalid, "invalidByCode": dict(sorted(invalid_by_code.items())),
        "invalidExamples": invalid_examples[:MAX_EXAMPLES],
        "unique": len(kept), "dup": dup, "conflicts": len(conflicts), "semanticDup": semantic,
        "semanticDupExamples": semantic_examples, "gaps": gaps, "gapGroups": gap_groups,
        "trailerMismatch": trailer_mismatch, "unterminatedFiles": sorted(set(unterminated_files)),
        "unterminatedGroups": unterminated_groups, "truncated": truncated,
        "droppedQueue": dropped_queue, "droppedIo": dropped_io,
        "events": len(events), "fake": fake, "real": len(events) - fake, "byType": by_type,
        "matches": sorted({r["matchId"] for r in events if r.get("matchId")}),
    }
    return {"summary": summary, "events": events, "conflicts": conflicts}


def write_outputs(result: dict, out_dir) -> None:
    """events.jsonl, conflicts.jsonl and merge_summary.json (UTF-8, LF, deterministic)."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    _write_jsonl(out / "events.jsonl", result["events"])
    _write_jsonl(out / "conflicts.jsonl", result["conflicts"])
    (out / "merge_summary.json").write_bytes(
        (json.dumps(result["summary"], indent=2, ensure_ascii=False) + "\n").encode("utf-8"))


def _write_jsonl(path: Path, rows) -> None:
    with open(path, "wb") as fh:
        for r in rows:
            fh.write((tel_schema.dumps(r) + "\n").encode("utf-8"))


def summary_line(s: dict) -> str:
    return (f"files={s['files']} lines={s['lines']} invalid={s['invalid']} events={s['events']} dup={s['dup']} "
            f"semantic_dup={s['semanticDup']} conflicts={s['conflicts']} gaps={s['gaps']} "
            f"trailer_mismatch={len(s['trailerMismatch'])} unterminated={len(s['unterminatedGroups'])} "
            f"dropped(trailer)={s['droppedQueue'] + s['droppedIo']} fake={s['fake']}")
