"""TEL-01 report: report.md + 9 CSV files from merged events, and `compare` of two cfgKeys.

Output is deterministic (no clock, no absolute paths): the same input gives the same bytes, which is
what golden/seed1 checks. CSV floats use 3 decimals, empty cell = no data.
"""
from __future__ import annotations

import csv
import io
from pathlib import Path

import tel_metrics
import tel_schema

CSV_NAMES = ("ttk_by_weapon.csv", "death_causes.csv", "zone_deaths_by_phase.csv", "loot_by_cfg.csv",
             "cc_by_cfg.csv", "item_use.csv", "hide.csv", "perf.csv", "coverage.csv")
NO_DATA = "không có dữ liệu"
LOW = "mẫu thấp (n<10)"


def f(x, d=2):
    """Report number: fixed decimals, '-' when missing."""
    if x is None:
        return "-"
    if isinstance(x, bool):
        return "có" if x else "không"
    if isinstance(x, int):
        return str(x)
    return f"{x:.{d}f}"


def c(x):
    """CSV cell: 3 decimals for floats, '' when missing, 1/0 for booleans."""
    if x is None:
        return ""
    if isinstance(x, bool):
        return "1" if x else "0"
    if isinstance(x, float):
        return f"{x:.3f}"
    return str(x)


def _csv(header, rows) -> str:
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    for r in rows:
        w.writerow([c(v) for v in r])
    return buf.getvalue()


def _table(header, rows) -> list[str]:
    out = ["| " + " | ".join(header) + " |", "|" + "|".join(" --- " for _ in header) + "|"]
    out += ["| " + " | ".join(str(v) for v in r) + " |" for r in rows]
    return out


# ----------------------------------------------------------------------------------------------
# Report
# ----------------------------------------------------------------------------------------------

def integrity_line(s: dict) -> str:
    fake_pct = tel_metrics.pct(s["fake"], s["events"])
    return (f"Kiểm: events={s['events']} dup={s['dup']} semantic_dup={s['semanticDup']} conflicts={s['conflicts']} "
            f"gaps={s['gaps']} trailer_mismatch={len(s['trailerMismatch'])} "
            f"unterminated={len(s['unterminatedGroups'])} invalid={s['invalid']} "
            f"dropped(trailer)={s['droppedQueue'] + s['droppedIo']} truncated={len(s['truncated'])} "
            f"fake={f(fake_pct, 1)}%")


def render(merge_result: dict, metrics: dict, schema: tel_schema.Schema, run: str = "run") -> dict[str, str]:
    """Return {filename: text} for report.md and the 9 CSV files."""
    s = merge_result["summary"]
    groups = metrics["cfgKeys"]
    keys = [k for k in groups if k != tel_metrics.UNATTACHED]
    gap = metrics["gapSec"]
    md = [f"# Báo cáo telemetry  run={run}  matches={len(s['matches'])}  cfgKey={', '.join(keys) or '-'}", "",
          integrity_line(s), ""]
    flags = []
    if s["conflicts"]:
        flags.append(f"{s['conflicts']} xung đột khóa kỹ thuật (xem conflicts.jsonl)")
    if s["gaps"]:
        flags.append(f"{s['gaps']} bản ghi thiếu theo seq")
    if s["invalid"]:
        flags.append(f"{s['invalid']} dòng sai schema bị bỏ")
    if s["unterminatedGroups"]:
        flags.append(f"{len(s['unterminatedGroups'])} nguồn không có _trailer")
    if flags:
        md += ["**Cờ lỗi:** " + "; ".join(flags) + ".", ""]
    loose = groups.get(tel_metrics.UNATTACHED)
    if loose:
        md += [f"perf_server ngoài khoảng mọi trận: {loose['perf']['serverWindows']} cửa sổ (không gộp vào cfgKey nào).",
               ""]
    md += [f"Định nghĩa đo theo TEL-01 mục 2.5: gapSec={f(gap, 1)} s, phân vị nearest-rank, nhóm n<10 là mẫu thấp. "
           "Đây là số đo, không phải mục tiêu cân bằng: báo cáo không kết luận đạt/không đạt về TTK.", ""]

    ttk_rows, cause_rows, phase_rows, loot_rows, cc_rows, use_rows, hide_rows, perf_rows = ([] for _ in range(8))
    containers = sorted(schema.id_sets["container"])
    froms = schema.enums["pickupFrom"]
    for key in keys:
        g = groups[key]
        fake_pct = tel_metrics.pct(g["fake"], g["records"])
        md += [f"## cfgKey {key}", "", f"matches={g['matches']} records={g['records']} fake={f(fake_pct, 1)}%", ""]

        md += [f"### TTK theo vũ khí (giây; giao tranh kết thúc bằng hạ gục, gap {f(gap, 0)} s)", ""]
        if g["ttk"]:
            md += _table(["weapon", "kills", "median", "p90", "hitsToKill", "oneShot", "ghi chú"],
                         [[r["weapon"], r["kills"], f(r["median"]), f(r["p90"]), f(r["hitsToKill"]), r["oneShot"],
                           LOW if r["lowSample"] else ""] for r in g["ttk"]])
            md += ["", f"Hạ gục bằng vũ khí không có chuỗi damage của người hạ: {g['killsWithoutChain']}", ""]
        else:
            md += [NO_DATA, ""]
        for r in g["ttk"]:
            ttk_rows.append([key, r["weapon"], r["kills"], r["median"], r["p90"], r["hitsToKill"], r["oneShot"],
                             r["lowSample"]])

        ca = g["causes"]
        md += ["### Nguyên nhân chết", ""]
        if ca["eliminations"]:
            md += _table(["cause", "n", "%"], [[k, v, f(tel_metrics.pct(v, ca["eliminations"]), 1)]
                                               for k, v in ca["byCause"].items()])
            md += ["", f"Tỷ lệ chết vì bo = {f(ca['zoneRate'], 1)}%; người chơi bị ≥1 damage bo = "
                       f"{f(ca['zoneHitRate'], 1)}% ({ca['zoneHitPlayers']}/{ca['spawns']})",
                   "Chết vì bo theo phase (-1/0/1/2/3/4): " + " / ".join(str(ca["zoneByPhase"][p])
                                                                          for p in tel_metrics.PHASES), ""]
        else:
            md += [NO_DATA, ""]
        for k, v in ca["byCause"].items():
            zone = k == "ZONE"
            cause_rows.append([key, k, v, tel_metrics.pct(v, ca["eliminations"]), ca["eliminations"],
                               ca["zoneHitPlayers"] if zone else None, ca["spawns"] if zone else None,
                               ca["zoneHitRate"] if zone else None])
        for p in tel_metrics.PHASES:
            phase_rows.append([key, p, ca["zoneByPhase"][p]])

        lo = g["loot"]
        md += ["### Loot (mỗi người, mỗi trận)", ""]
        if lo["drops"] or lo["pickups"] or lo["firstEquipN"]:
            md += [f"vật chứa vỡ: {lo['drops']} ({', '.join(f'{k} {v}' for k, v in lo['dropsByContainer'].items()) or '-'})"
                   f" | món rơi: {lo['itemsDropped']} | món nhặt: {lo['pickups']} "
                   f"({f(lo['pickupsPerPlayer'])}/người)",
                   f"có trang bị đầu tiên (trung vị): {f(lo['firstEquipMedian'], 1)} s (n={lo['firstEquipN']}, "
                   f"không bao giờ có: {lo['neverEquipped']}) | chết khi chỉ có HAND: "
                   f"{lo['starvedDeaths']}/{lo['eliminations']}", ""]
        else:
            md += [NO_DATA, ""]
        loot_rows.append([key, lo["matches"], lo["players"], lo["drops"]]
                         + [lo["dropsByContainer"].get(k, 0) for k in containers]
                         + [lo["itemsDropped"], lo["pickups"]] + [lo["pickupsByFrom"].get(k, 0) for k in froms]
                         + [lo["pickupsPerPlayer"], lo["firstEquipMedian"], lo["firstEquipN"], lo["neverEquipped"],
                            lo["starvedDeaths"], lo["eliminations"]])

        cc = g["cc"]
        md += ["### Khống chế (CC)", ""]
        if cc["rows"]:
            md += _table(["ccKind", "by", "n", "giây", "lần/người", "giây/người", "CLEANSED %"],
                         [[r["ccKind"], r["by"], r["n"], f(r["seconds"]), f(r["perPlayer"]),
                           f(r["secondsPerPlayer"]), f(r["cleansedPct"], 1)] for r in cc["rows"]])
            md += ["", f"Kết thúc sớm (CLEANSED) trên tổng: {f(cc['cleansedPct'], 1)}% của {cc['spans']} lần", ""]
        else:
            md += [NO_DATA, ""]
        for r in cc["rows"]:
            cc_rows.append([key, r["ccKind"], r["by"], r["n"], r["seconds"], r["perPlayer"], r["secondsPerPlayer"],
                            r["cleansed"], r["cleansedPct"]])

        md += ["### Thời gian dùng vật phẩm (chênh = thực − plannedMs, chỉ lượt DONE)", ""]
        if g["itemUse"]:
            md += _table(["item", "n", "DONE", "chênh TB (ms)", "CANCELLED %", "INTERRUPTED %", "ghi chú"],
                         [[r["item"], r["n"], r["done"], f(r["meanDeltaMs"]), f(r["cancelledPct"], 1),
                           f(r["interruptedPct"], 1), LOW if r["lowSample"] else ""] for r in g["itemUse"]])
            md += [""]
        else:
            md += [NO_DATA, ""]
        for r in g["itemUse"]:
            use_rows.append([key, r["item"], r["n"], r["done"], r["meanDeltaMs"], r["cancelledPct"],
                             r["interruptedPct"], r["lowSample"]])

        h = g["hide"]
        md += ["### Ẩn nấp", ""]
        if h["spans"]:
            md += [f"số đợt: {h['spans']} | dài TB: {f(h['meanSeconds'])} s | người từng ẩn: {h['hidPlayers']}/"
                   f"{h['players']} ({f(h['hidPct'], 1)}%) | bị loại khi đang ẩn: {h['diedHidden']} | "
                   f"bị đánh khi ẩn: {h['attackedWhileHidden']}", ""]
        else:
            md += [NO_DATA, ""]
        hide_rows.append([key, h["spans"], h["meanSeconds"], h["players"], h["hidPlayers"], h["hidPct"],
                          h["diedHidden"], h["attackedWhileHidden"]])

        pf = g["perf"]
        md += ["### Hiệu năng", ""]
        if pf["serverWindows"]:
            md += [f"server ({pf['serverWindows']} cửa sổ): tick p95 trung vị = {f(pf['tickP95Median'], 3)} ms, "
                   f"max = {f(pf['tickP95Max'], 3)} ms (mục tiêu < 33.3) | tick p99 max = {f(pf['tickP99Max'], 3)} ms"
                   f" | droppedTicks = {pf['droppedTicks']} | tickOverruns = {pf['tickOverruns']} | "
                   f"CPU trung vị = {f(pf['cpuMedian'], 1)}% | RAM max = {f(pf['workingSetMax'], 1)} MiB"]
        else:
            md += [f"server: {NO_DATA}"]
        if pf["clientWindows"]:
            md += [f"client ({pf['clientWindows']} cửa sổ): FPS avg = {f(pf['fpsAvg'], 1)} (mục tiêu ≥ 55) | "
                   f"1% low trung vị = {f(pf['fps1LowMedian'], 1)}, min = {f(pf['fps1LowMin'], 1)} (mục tiêu ≥ 40) | "
                   f"frame p95 trung vị = {f(pf['frameMsP95Median'])} ms | RTT p95 trung vị = "
                   f"{f(pf['rttP95Median'], 1)} ms | jitter trung vị = {f(pf['jitterMedian'], 1)} ms | loss TB = "
                   f"{f(pf['lossPctMean'])}% | transport: "
                   + ", ".join(f"{k} {v}" for k, v in pf["transports"].items())]
        else:
            md += [f"client: {NO_DATA}"]
        md += [""]
        for name, val in pf.items():
            if name in ("serverWindows", "clientWindows", "transports"):
                continue
            perf_rows.append([key, "server" if name in _SERVER_PERF else "client", name, val])
        perf_rows.append([key, "server", "windows", pf["serverWindows"]])
        perf_rows.append([key, "client", "windows", pf["clientWindows"]])

    md += ["## Nguồn dữ liệu theo loại (coverage.csv)", ""]
    md += _table(["#", "type", "nguồn", "real", "fake", "trạng thái"],
                 [[r["n"], r["type"], r["source"], r["real"], r["fake"], r["status"]] for r in metrics["coverage"]])
    md += [""]

    files = {"report.md": "\n".join(md)}
    files["ttk_by_weapon.csv"] = _csv(["cfgKey", "weapon", "kills", "median_s", "p90_s", "hitsToKill", "oneShot",
                                       "lowSample"], ttk_rows)
    files["death_causes.csv"] = _csv(["cfgKey", "cause", "n", "pct", "eliminations", "zoneHitPlayers", "spawns",
                                      "zoneHitPct"], cause_rows)
    files["zone_deaths_by_phase.csv"] = _csv(["cfgKey", "zonePhase", "n"], phase_rows)
    files["loot_by_cfg.csv"] = _csv(["cfgKey", "matches", "players", "drops"] + [f"drops_{k}" for k in containers]
                                    + ["itemsDropped", "pickups"] + [f"pickups_{k}" for k in froms]
                                    + ["pickupsPerPlayer", "firstEquipMedian_s", "firstEquipN", "neverEquipped",
                                       "starvedDeaths", "eliminations"], loot_rows)
    files["cc_by_cfg.csv"] = _csv(["cfgKey", "ccKind", "by", "n", "seconds", "perPlayer", "secondsPerPlayer",
                                   "cleansed", "cleansedPct"], cc_rows)
    files["item_use.csv"] = _csv(["cfgKey", "item", "n", "done", "meanDeltaMs", "cancelledPct", "interruptedPct",
                                  "lowSample"], use_rows)
    files["hide.csv"] = _csv(["cfgKey", "spans", "meanSeconds", "players", "hidPlayers", "hidPct", "diedHidden",
                              "attackedWhileHidden"], hide_rows)
    files["perf.csv"] = _csv(["cfgKey", "source", "metric", "value"], perf_rows)
    files["coverage.csv"] = _csv(["n", "type", "source", "real", "fake", "fakePct", "status"],
                                 [[r["n"], r["type"], r["source"], r["real"], r["fake"], r["fakePct"], r["status"]]
                                  for r in metrics["coverage"]])
    return files


_SERVER_PERF = {"tickP95Median", "tickP95Max", "tickP99Max", "ticksPerSecondMin", "droppedTicks", "tickOverruns",
                "cpuMedian", "workingSetMax"}


def write_files(files: dict[str, str], out_dir) -> None:
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for name, text in files.items():
        if not text.endswith("\n"):
            text += "\n"
        (out / name).write_bytes(text.encode("utf-8"))


# ----------------------------------------------------------------------------------------------
# Compare
# ----------------------------------------------------------------------------------------------

def resolve_key(keys: list[str], wanted: str) -> str:
    if wanted in keys:
        return wanted
    hits = [k for k in keys if k.startswith(wanted)]
    if len(hits) != 1:
        raise ValueError(f"cfgKey {wanted!r} matches {len(hits)} of: {', '.join(keys)}")
    return hits[0]


def comparable_keys(metrics: dict) -> list[str]:
    return [k for k, g in metrics["cfgKeys"].items() if g["matches"] > 0]


def compare_rows(metrics: dict, key_a: str, key_b: str) -> list[dict]:
    a, b = metrics["cfgKeys"][key_a], metrics["cfgKeys"][key_b]
    rows = []

    def add(section, item, metric, va, vb, na, nb):
        delta = vb - va if va is not None and vb is not None else None
        rows.append({"section": section, "item": item, "metric": metric, "A": va, "B": vb, "delta": delta,
                     "nA": na, "nB": nb, "note": LOW if min(na, nb) < tel_metrics.LOW_SAMPLE else ""})

    ta = {r["weapon"]: r for r in a["ttk"]}
    tb = {r["weapon"]: r for r in b["ttk"]}
    for w in sorted(set(ta) | set(tb)):
        ra, rb = ta.get(w), tb.get(w)
        na, nb = (ra["kills"] if ra else 0), (rb["kills"] if rb else 0)
        add("ttk", w, "median_s", ra and ra["median"], rb and rb["median"], na, nb)
        add("ttk", w, "hitsToKill", ra and ra["hitsToKill"], rb and rb["hitsToKill"], na, nb)
    ca, cb = a["causes"], b["causes"]
    add("causes", "ZONE", "zoneDeathPct", ca["zoneRate"], cb["zoneRate"], ca["eliminations"], cb["eliminations"])
    for cause in tel_metrics.CAUSES:
        add("causes", cause, "pct", tel_metrics.pct(ca["byCause"][cause], ca["eliminations"]),
            tel_metrics.pct(cb["byCause"][cause], cb["eliminations"]), ca["eliminations"], cb["eliminations"])
    la, lb = a["loot"], b["loot"]
    add("loot", "*", "pickupsPerPlayer", la["pickupsPerPlayer"], lb["pickupsPerPlayer"], la["players"], lb["players"])
    add("loot", "*", "firstEquipMedian_s", la["firstEquipMedian"], lb["firstEquipMedian"], la["firstEquipN"],
        lb["firstEquipN"])
    add("loot", "*", "starvedDeathPct", tel_metrics.pct(la["starvedDeaths"], la["eliminations"]),
        tel_metrics.pct(lb["starvedDeaths"], lb["eliminations"]), la["eliminations"], lb["eliminations"])
    cca = {r["ccKind"]: r for r in a["cc"]["rows"] if r["by"] == "ALL"}
    ccb = {r["ccKind"]: r for r in b["cc"]["rows"] if r["by"] == "ALL"}
    for kind in tel_metrics.CC_KINDS:
        ra, rb = cca.get(kind), ccb.get(kind)
        na, nb = (ra["n"] if ra else 0), (rb["n"] if rb else 0)
        add("cc", kind, "perPlayer", ra and ra["perPlayer"], rb and rb["perPlayer"], na, nb)
        add("cc", kind, "secondsPerPlayer", ra and ra["secondsPerPlayer"], rb and rb["secondsPerPlayer"], na, nb)
    add("cc", "*", "cleansedPct", a["cc"]["cleansedPct"], b["cc"]["cleansedPct"], a["cc"]["spans"], b["cc"]["spans"])
    ua = {r["item"]: r for r in a["itemUse"]}
    ub = {r["item"]: r for r in b["itemUse"]}
    for item in sorted(set(ua) | set(ub)):
        ra, rb = ua.get(item), ub.get(item)
        add("itemUse", item, "meanDeltaMs", ra and ra["meanDeltaMs"], rb and rb["meanDeltaMs"],
            ra["done"] if ra else 0, rb["done"] if rb else 0)
        add("itemUse", item, "cancelledPct", ra and ra["cancelledPct"], rb and rb["cancelledPct"],
            ra["n"] if ra else 0, rb["n"] if rb else 0)
    ha, hb = a["hide"], b["hide"]
    add("hide", "*", "hidPct", ha["hidPct"], hb["hidPct"], ha["players"], hb["players"])
    add("hide", "*", "meanSeconds", ha["meanSeconds"], hb["meanSeconds"], ha["spans"], hb["spans"])
    pa, pb = a["perf"], b["perf"]
    for metric, n_key in (("tickP95Median", "serverWindows"), ("tickP99Max", "serverWindows"),
                          ("fpsAvg", "clientWindows"), ("fps1LowMedian", "clientWindows"),
                          ("rttP95Median", "clientWindows")):
        add("perf", "*", metric, pa.get(metric), pb.get(metric), pa[n_key], pb[n_key])
    return rows


def render_compare(metrics: dict, key_a: str, key_b: str) -> dict[str, str]:
    rows = compare_rows(metrics, key_a, key_b)
    a, b = metrics["cfgKeys"][key_a], metrics["cfgKeys"][key_b]
    md = [f"# So sánh config  A={key_a} (matches={a['matches']})  B={key_b} (matches={b['matches']})", "",
          "delta = B − A. Nhóm có n < 10 ở một bên gắn mẫu thấp: không dùng để kết luận.", ""]
    md += _table(["section", "item", "metric", "A", "B", "delta", "nA", "nB", "ghi chú"],
                 [[r["section"], r["item"], r["metric"], f(r["A"], 3), f(r["B"], 3),
                   ("+" if r["delta"] is not None and r["delta"] > 0 else "") + f(r["delta"], 3), r["nA"], r["nB"],
                   r["note"]] for r in rows])
    md += [""]
    text = _csv(["section", "item", "metric", "A", "B", "delta", "nA", "nB", "note"],
                [[r["section"], r["item"], r["metric"], r["A"], r["B"], r["delta"], r["nA"], r["nB"], r["note"]]
                 for r in rows])
    return {"compare.md": "\n".join(md), "compare.csv": text}
