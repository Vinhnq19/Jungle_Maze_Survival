"""Create 34 standalone UI icon copies without modifying Imported/JSurviv.

Use --native32 for the current shared-palette, binary-alpha 32px pipeline.
The legacy mode accepts independent HAND/ROPE sources for historical replay.
This tool deliberately does not write Unity .meta files or bind prefabs.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / "Docs/Art/Production_A/Items"
ROSTER = ROOT / "Docs/Tickets/ART-04_icon_inventory.csv"
NATIVE = REPORT / "OriginalPixelSources"
SIZE = 64
TARGET = 56
OUTLINE = "#D6DFC7"
FONT = ROOT / "Assets/_JungleMaze/Art/Fonts/BeVietnamPro-Medium.ttf"


def original_sprite(item_id: str) -> Image.Image:
    """Original 32px recipes for four misleading or branded legacy sprites."""
    im = Image.new("RGBA", (32, 32))
    d = ImageDraw.Draw(im)
    ink, metal, bright = "#17262C", "#6B9291", "#C7D9CB"
    gold, wood, wood_lit = "#C6AE78", "#825737", "#B58A56"
    if item_id == "SPIKE_HELMET":
        d.polygon([(3, 14), (5, 9), (10, 6), (21, 6), (26, 10), (28, 19), (26, 26), (5, 26)], fill=ink)
        d.polygon([(5, 14), (8, 10), (12, 8), (20, 8), (24, 12), (26, 20), (24, 24), (7, 24)], fill=metal)
        d.polygon([(4, 14), (1, 8), (7, 11), (9, 17)], fill=ink)
        d.polygon([(27, 14), (31, 8), (25, 11), (23, 17)], fill=ink)
        d.polygon([(13, 9), (15, 1), (18, 9)], fill=ink)
        d.polygon([(14, 8), (15, 3), (17, 8)], fill=bright)
        d.polygon([(5, 13), (3, 10), (7, 13)], fill=bright)
        d.polygon([(26, 13), (29, 10), (25, 14)], fill=bright)
        d.rectangle((8, 14, 23, 16), fill="#A1BCB0")
        d.rectangle((14, 8, 16, 24), fill=bright)
        d.rectangle((4, 20, 27, 23), fill=ink)
        d.rectangle((5, 20, 26, 21), fill=gold)
        d.rectangle((8, 25, 10, 28), fill=metal)
        d.rectangle((21, 25, 23, 28), fill=metal)
    elif item_id == "WOODEN_ARMOR":
        d.polygon([(8, 3), (12, 3), (12, 8), (19, 8), (19, 3), (24, 3), (30, 10), (27, 15), (25, 14), (25, 29), (6, 29), (6, 14), (3, 15), (1, 10)], fill=ink)
        d.polygon([(8, 5), (10, 5), (10, 10), (21, 10), (21, 5), (23, 5), (28, 11), (26, 13), (23, 12), (23, 27), (8, 27), (8, 12), (5, 13), (3, 11)], fill=wood)
        for y in (11, 16, 21):
            d.rectangle((8, y, 23, y + 3), fill=wood_lit)
            d.line((10, y + 2, 22, y + 2), fill=wood)
        d.rectangle((11, 8, 12, 27), fill="#D4B885")
        d.rectangle((19, 8, 20, 27), fill="#D4B885")
        d.rectangle((9, 24, 22, 26), fill="#4A3F2F")
        d.rectangle((14, 24, 17, 26), fill=gold)
        d.point((10, 13), fill=ink)
        d.point((21, 18), fill=ink)
    elif item_id == "ADRENALINE":
        d.rectangle((10, 2, 21, 29), fill=ink)
        d.rectangle((8, 5, 23, 26), fill=ink)
        d.rectangle((10, 4, 21, 27), fill="#966F3E")
        d.rectangle((9, 6, 22, 25), fill="#C6AE78")
        d.rectangle((11, 6, 13, 25), fill="#E5D3A1")
        d.rectangle((20, 6, 22, 25), fill="#9C7547")
        d.rectangle((10, 3, 21, 4), fill=bright)
        d.rectangle((10, 27, 21, 28), fill=metal)
        d.rectangle((13, 3, 17, 3), fill=metal)
        d.polygon([(17, 9), (12, 17), (16, 17), (14, 23), (21, 14), (17, 14)], fill="#346F67")
    elif item_id == "SEED":
        d.polygon([(14, 3), (20, 5), (25, 10), (26, 19), (22, 26), (15, 29), (8, 26), (5, 19), (7, 11)], fill=ink)
        d.polygon([(14, 5), (19, 7), (23, 11), (24, 19), (20, 24), (15, 27), (10, 24), (7, 19), (9, 12)], fill=wood)
        d.polygon([(14, 6), (17, 8), (11, 14), (10, 21), (8, 18), (10, 11)], fill=wood_lit)
        d.line([(17, 9), (14, 14), (13, 22), (15, 25)], fill="#D0AA72", width=2)
        d.rectangle((13, 1, 16, 4), fill="#547C52")
    else:
        raise ValueError(item_id)
    return im


def sprite_rect(path: Path, name: str) -> tuple[int, int, int, int]:
    """Use the named Unity slice, converting its bottom-left y to PNG top-left."""
    meta = path.with_suffix(path.suffix + ".meta").read_text(encoding="utf-8")
    pattern = re.compile(r"^\s+name: (.*?)\n\s+rect:\n\s+serializedVersion: \d+\n\s+x: ([\d.]+)\n\s+y: ([\d.]+)\n\s+width: ([\d.]+)\n\s+height: ([\d.]+)", re.M)
    choices = {m[1]: tuple(int(float(m[i])) for i in range(2, 6)) for m in pattern.finditer(meta)}
    if name not in choices:
        raise ValueError(f"Missing explicit slice {name!r}: {path}")
    x, y, w, h = choices[name]
    height = Image.open(path).height
    return x, height - y - h, x + w, height - y


def source_for(row: dict, args) -> tuple[Image.Image, dict]:
    item_id = row["id"]
    if item_id in {"HAND", "ROPE"}:
        source = getattr(args, item_id.lower())
        if not source:
            raise FileNotFoundError(f"{item_id}: generated source not supplied")
        path = Path(source).resolve()
        return Image.open(path).convert("RGBA"), {"source": str(path.relative_to(ROOT)), "source_kind": "ImageGen", "slice": "full image"}
    if item_id in {"SPIKE_HELMET", "WOODEN_ARMOR", "ADRENALINE", "SEED"}:
        path = NATIVE / f"{item_id}_32.png"
        im = original_sprite(item_id)
        im.save(path)
        return im, {"source": str(path.relative_to(ROOT)), "source_kind": "Original pixel recipe", "slice": "32x32 native", "replacement_reason": {"SPIKE_HELMET": "Legacy candidate is a non-spiked pith helmet", "WOODEN_ARMOR": "Legacy candidate is a green tactical vest", "ADRENALINE": "Legacy candidate contains beverage branding", "SEED": "Legacy pink sprite resembles mermaid scale at 28px"}[item_id]}
    path = ROOT / row["sprite_de_xuat"]
    im = Image.open(path).convert("RGBA")
    named = {"BELL": "bell_2", "METEORITE_FRAGMENT": "meteorite_throwing-Sheet_2", "SHOTGUN": "shotgun 1_0"}
    rect = (0, 0, im.width, im.height)
    frame = "full image"
    if item_id in named:
        frame = named[item_id]
        rect = sprite_rect(path, frame)
        im = im.crop(rect)
    return im, {"source": str(path.relative_to(ROOT)), "source_kind": "JSurviv copy", "slice": frame, "slice_rect_png": list(rect)}


def opaque_pixels(im: Image.Image) -> Image.Image:
    """Remove antialias halo without adding pixels outside the actual silhouette."""
    im = im.copy()
    a = im.getchannel("A").point(lambda v: 255 if v >= 128 else 0)
    im.putalpha(a)
    return im


def outline(im: Image.Image) -> Image.Image:
    alpha = im.getchannel("A")
    rim = alpha.filter(ImageFilter.MaxFilter(3))
    layer = Image.new("RGBA", im.size, OUTLINE)
    layer.putalpha(rim)
    layer.alpha_composite(im)
    return layer


def normalize(im: Image.Image, item_id: str) -> tuple[Image.Image, dict]:
    im = opaque_pixels(im)
    bbox = im.getbbox()
    if not bbox:
        raise ValueError(f"Empty source: {item_id}")
    im = im.crop(bbox)
    # The two source silhouettes are very thin (knife 62x16, shotgun 58x10).
    # Thicken their local y before rotation so their blade/barrel still reads
    # at a 28px slot; preserve the original drawn stock/blade rather than
    # replacing it with a generic gun or sword.
    readability_y_scale = {"KNIFE": 1.5, "SHOTGUN": 1.8}.get(item_id, 1.0)
    if readability_y_scale != 1.0:
        im = im.resize((im.width, round(im.height * readability_y_scale)), Image.Resampling.NEAREST)
    rotation = {"KNIFE": 35, "SHOTGUN": 25, "SCEPTER": 30}.get(item_id, 0)
    if rotation:
        im = im.rotate(rotation, resample=Image.Resampling.NEAREST, expand=True)
        im = im.crop(im.getbbox())
    resize_from = list(im.size)
    factor = TARGET / max(im.size)
    size = tuple(max(1, round(v * factor)) for v in im.size)
    method = Image.Resampling.NEAREST if factor >= 1 else Image.Resampling.LANCZOS
    im = opaque_pixels(im.resize(size, method))
    canvas = Image.new("RGBA", (SIZE, SIZE))
    canvas.alpha_composite(im, ((SIZE - im.width) // 2, (SIZE - im.height) // 2))
    # Only dark metal weapon/armor needs a light contour on charcoal slots.
    rim = item_id in {"SHOTGUN", "ARMOR", "MAGIC_ARMOR", "TREE_BRANCH"}
    if rim:
        canvas = outline(canvas)
    return canvas, {"source_alpha_bbox": list(bbox), "readability_y_scale": readability_y_scale, "rotation_degrees": rotation, "resize_from": resize_from, "resize_to": list(im.size), "resize_method": "nearest" if method == Image.Resampling.NEAREST else "Lanczos then binary alpha", "light_outline_px": 1 if rim else 0}


def measurements(im: Image.Image) -> dict:
    a = im.getchannel("A")
    bbox = a.point(lambda v: 255 if v > 8 else 0).getbbox()
    if not bbox:
        raise ValueError("Empty output")
    x0, y0, x1, y1 = bbox
    histogram = a.histogram()
    visible = sum(histogram[1:])
    semi = sum(histogram[1:255])
    return {"size": list(im.size), "mode": im.mode, "bbox_alpha_gt8": list(bbox), "content_long_edge": max(x1 - x0, y1 - y0), "center_offset": [(x0 + x1) / 2 - 32, (y0 + y1) / 2 - 32], "semi_transparent_ratio": semi / visible if visible else 0, "corner_alpha": [a.getpixel(p) for p in ((0, 0), (63, 0), (0, 63), (63, 63))]}


def contact_sheet(rows: list, produced: dict, pixels: int, background: str, suffix: str) -> None:
    font = ImageFont.truetype(str(FONT), 10)
    cell_w, cell_h, cols = 150, pixels + 52, 6
    sheet = Image.new("RGB", (cols * cell_w, math.ceil(len(rows) / cols) * cell_h), background)
    draw = ImageDraw.Draw(sheet)
    foreground = "#EDF2EE" if suffix != "white" else "#17262C"
    for i, row in enumerate(rows):
        x, y = i % cols * cell_w, i // cols * cell_h
        if row["id"] in produced:
            icon = Image.open(ROOT / row["dich_chuan_hoa"]).convert("RGBA").resize((pixels, pixels), Image.Resampling.NEAREST)
            sheet.paste(icon, (x + (cell_w - pixels) // 2, y + 3), icon)
        else:
            draw.text((x + 25, y + 4), "MISSING", font=font, fill="#E2483D")
        draw.text((x + 4, y + pixels + 7), row["id"], font=font, fill=foreground)
        draw.text((x + 4, y + pixels + 23), row["ten_cu_gdd"], font=font, fill=foreground)
    sheet.save(REPORT / f"Contact_{pixels}px_{suffix}.png")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hand")
    parser.add_argument("--rope")
    parser.add_argument("--allow-missing", action="store_true")
    parser.add_argument("--native32", action="store_true", help="Author/normalize on a shared 32px grid and validate (a)-(f)")
    parser.add_argument("--check-only", action="store_true", help="With --native32: validate existing PNGs without writing")
    args = parser.parse_args()
    if args.native32:
        from native32_pipeline import run
        run(args)
        return
    if args.check_only:
        parser.error("--check-only requires --native32")
    REPORT.mkdir(parents=True, exist_ok=True)
    NATIVE.mkdir(parents=True, exist_ok=True)
    rows = list(csv.DictReader(ROSTER.open(encoding="utf-8-sig")))
    weapon_ids = {r["id"] for r in json.loads((ROOT / "Assets/StreamingAssets/JungleMaze/balance/weapons.json").read_text())}
    equipment_ids = {r["id"] for r in json.loads((ROOT / "Assets/StreamingAssets/JungleMaze/balance/equipments.json").read_text())}
    assert len(rows) == 34 and len({r["id"] for r in rows}) == 34
    assert {r["id"] for r in rows if r["loai"] == "Weapon"} == weapon_ids
    assert {r["id"] for r in rows if r["loai"] == "Equipment"} == equipment_ids
    produced, missing = {}, []
    for row in rows:
        try:
            im, provenance = source_for(row, args)
        except FileNotFoundError as exc:
            if not args.allow_missing:
                raise
            missing.append({"id": row["id"], "reason": str(exc)})
            continue
        source_path = ROOT / provenance["source"]
        provenance["source_sha256"] = hashlib.sha256(source_path.read_bytes()).hexdigest()
        result, transform = normalize(im, row["id"])
        metrics = measurements(result)
        assert metrics["mode"] == "RGBA" and metrics["size"] == [64, 64]
        assert 44 <= metrics["content_long_edge"] <= 60, (row["id"], metrics)
        assert max(abs(v) for v in metrics["center_offset"]) <= 2, (row["id"], metrics)
        assert metrics["corner_alpha"] == [0, 0, 0, 0]
        assert metrics["semi_transparent_ratio"] <= 0.1
        target = ROOT / row["dich_chuan_hoa"]
        target.parent.mkdir(parents=True, exist_ok=True)
        result.save(target)
        produced[row["id"]] = {"id": row["id"], "kind": row["loai"], "target": row["dich_chuan_hoa"], **provenance, **transform, **metrics}
    # Distinct files, not one generic fallback used for multiple Code IDs.
    digests = [hashlib.sha256((ROOT / p["target"]).read_bytes()).hexdigest() for p in produced.values()]
    assert len(digests) == len(set(digests))
    report = {"canvas": [64, 64], "produced": len(produced), "expected": 34, "missing": missing, "runtime_rgba_bytes_without_mips": len(produced) * 64 * 64 * 4, "unity_import_and_binding": "not tested by this asset-only tool", "icons": list(produced.values())}
    (REPORT / "Normalization.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    fields = ["id", "kind", "source_kind", "source", "slice", "target", "source_sha256"]
    with (REPORT / "Provenance.csv").open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(produced.values())
    for pixels in (18, 28, 40):
        contact_sheet(rows, produced, pixels, "#202A2D", "slot")
    contact_sheet(rows, produced, 28, "#FFFFFF", "white")
    contact_sheet(rows, produced, 28, "#000000", "black")
    contact_sheet(rows, produced, 28, "#28534F", "selected")
    print(json.dumps({"produced": len(produced), "missing": missing, "report": str(REPORT.relative_to(ROOT))}, ensure_ascii=False))


if __name__ == "__main__":
    main()
