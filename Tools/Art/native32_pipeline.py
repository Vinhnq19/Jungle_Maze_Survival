"""Deterministic native32 production, validation, and read-only HUD/menu audit.

No Unity API, source-image mutation, or .meta writes. Preflight the whole batch
before overwriting runtime PNGs. --check-only reads the actual exported files.
"""
from collections import Counter
import csv
import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw, ImageFilter, ImageFont

from native32_recipes import RECIPE_IDS, draw_item

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / 'Docs/Art/Production_A/Items'
NATIVE = REPORT / 'Native32Sources'
PALETTE_PATH = ROOT / 'Tools/Art/pixel_palette.json'
ICONS = ROOT / 'Assets/_JungleMaze/Art/UI/Icons'
FONT = ROOT / 'Assets/_JungleMaze/Art/Fonts/BeVietnamPro-Medium.ttf'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_rgba(path):
    with Image.open(path) as source:
        return source.convert('RGBA')


def flattened(im):
    # Pillow >=12.1 exposes the non-deprecated spelling; keep older runtimes.
    return im.get_flattened_data() if hasattr(im,'get_flattened_data') else im.getdata()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def palette():
    data = json.loads(PALETTE_PATH.read_text(encoding='utf-8'))
    rgb = [ImageColor.getrgb(v) for v in data['colors'].values()]
    if len(rgb) != len(set(rgb)) or len(rgb) > 24:
        raise ValueError('Shared palette must have at most 24 unique colors')
    required = {'#0C1417', '#202A2D', '#526B69', '#77908A', '#A6BBB7',
                '#EDF2EE', '#63BAA5', '#28534F', '#C6AE78'}
    if not required <= set(data['colors'].values()):
        raise ValueError('Missing direction A tones')
    for ramp in data['ramps_dark_base_light'].values():
        if len(ramp) != 3 or any(c not in data['colors'] for c in ramp):
            raise ValueError('Invalid material ramp')
    return data, rgb


def quantize(im, rgb):
    """Map visible pixels only, no dithering; reserve one of eight colors for ink."""
    ink = rgb[0]
    mapped = {}
    counts = Counter()
    for pixel in flattened(im):
        if pixel[3] < 128:
            continue
        if pixel not in mapped:
            mapped[pixel] = min(rgb, key=lambda c: sum((a-b)**2 for a,b in zip(pixel[:3],c)))
        counts[mapped[pixel]] += 1
    selected = [ink] + [c for c,_ in sorted(counts.items(), key=lambda t: (-t[1],t[0])) if c != ink][:7]
    reduced = {c: min(selected, key=lambda s: sum((a-b)**2 for a,b in zip(c,s))) for c in counts}
    out = Image.new('RGBA', im.size)
    out.putdata([(*reduced[mapped[p]],255) if p[3] >= 128 else (0,0,0,0) for p in flattened(im)])
    return out


def repair_material(content, item_id, colors):
    """Native-grid material edits for legacy silhouettes lost on charcoal.

    Keep source alpha and geometry; replace old nearly-black material/green
    axe metal with three broad lit planes before adding the common ink rim.
    """
    if item_id not in {'TREE_BRANCH','SHOTGUN','AXE','MACE','RUBBER_GUN'}:
        return content
    out = content.copy()
    rgb = {k:ImageColor.getrgb(v) for k,v in colors.items()}
    for y in range(content.height):
        for x in range(content.width):
            r,g,b,a = content.getpixel((x,y))
            if not a:
                continue
            lum = (r*3+g*6+b)/10
            if item_id in {'TREE_BRANCH','RUBBER_GUN'}:
                color = 'wood_light' if lum>100 else 'wood' if lum>35 else 'wood_dark'
                # Upper-facing exposed edges catch the key light.
                if y == 0 or content.getpixel((x,y-1))[3]==0:
                    color = 'wood_light'
            elif item_id == 'SHOTGUN':
                if x < content.width*.38:
                    color = 'wood_light' if lum>60 else 'wood'
                else:
                    color = 'metal_light' if y==0 or content.getpixel((x,y-1))[3]==0 else 'metal'
            elif item_id == 'AXE':
                if y < content.height*.45 and x < content.width*.55:
                    color = 'metal_light' if lum>95 else 'metal'
                else:
                    color = 'wood_light' if lum>80 else 'wood'
            else:
                if y > content.height*.45:
                    color = 'wood_light' if lum>80 else 'wood'
                else:
                    color = 'metal_light' if lum>110 else 'metal' if lum>45 else 'metal_dark'
            out.putpixel((x,y),(*rgb[color],255))
    return out


def finish_native(im, rgb, allow_resize, item_id=None, colors=None):
    """Center the material silhouette, add exactly one native pixel outside it."""
    im = im.convert('RGBA')
    im.putalpha(im.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    bbox = im.getbbox()
    if not bbox:
        raise ValueError('Empty source')
    content = im.crop(bbox)
    transform = {'source_alpha_bbox': list(bbox), 'resize_from': list(content.size)}
    if max(content.size) > 26:
        if not allow_resize:
            raise ValueError(f'Hand recipe exceeds 26px: {content.size}')
        scale = 26 / max(content.size)
        content = content.resize(tuple(max(1,round(v*scale)) for v in content.size), Image.Resampling.NEAREST)
    if colors is not None:
        content = repair_material(content,item_id,colors)
    content = quantize(content, rgb)
    canvas = Image.new('RGBA', (32,32))
    canvas.paste(content, ((32-content.width)//2,(32-content.height)//2))
    rim = canvas.getchannel('A').filter(ImageFilter.MaxFilter(3))
    result = Image.new('RGBA', (32,32), (*rgb[0],255))
    result.putalpha(rim)
    result.alpha_composite(canvas)
    # Recenter after rasterization; canonicalize RGB under zero alpha.
    bounds = result.getbbox()
    trimmed = result.crop(bounds)
    result = Image.new('RGBA',(32,32))
    result.paste(trimmed,((32-trimmed.width)//2,(32-trimmed.height)//2))
    result.putdata([p if p[3] else (0,0,0,0) for p in flattened(result)])
    transform.update({'resize_to': list(content.size), 'resize_method': 'nearest' if content.size != tuple(transform['resize_from']) else 'none; authored pixels preserved',
                      'native_size':32,'scale':2,'method':'nearest','outline_native_px':1})
    return result, transform


def validate(im, rgb):
    """Check each contract independently, including 8-neighbor silhouette rims.

    f checks every visible boundary pixel (outer boundary AND holes) is ink,
    and that visible non-ink material remains. e includes >=2px edge padding.
    Transparent RGB is exempt from the visible palette, alpha never is.
    """
    rgba = im.convert('RGBA')
    pixels = list(flattened(rgba))
    colors = {p[:3] for p in pixels if p[3]}
    checks = {'a': im.mode == 'RGBA' and im.size == (64,64)}
    checks['b'] = im.size == (64,64) and all(
        len({rgba.getpixel((x+dx,y+dy)) for dx in range(2) for dy in range(2)}) == 1
        for y in range(0,64,2) for x in range(0,64,2))
    checks['c'] = {p[3] for p in pixels} <= {0,255}
    checks['d'] = bool(colors) and colors <= set(rgb) and len(colors) <= 8
    native = rgba.resize((32,32),Image.Resampling.NEAREST)
    bbox = native.getbbox()
    long_edge, center, padding = 0, [99,99], 0
    if bbox:
        x0,y0,x1,y1 = bbox
        long_edge = max(x1-x0,y1-y0)
        center = [(x0+x1)/2-16,(y0+y1)/2-16]
        padding = min(x0,y0,32-x1,32-y1)
    checks['e'] = 26 <= long_edge <= 28 and max(map(abs,center)) <= 1 and padding >= 2
    boundary, bad = 0, 0
    for y in range(32):
        for x in range(32):
            p = native.getpixel((x,y))
            if not p[3]:
                continue
            if any(not (0<=x+dx<32 and 0<=y+dy<32) or native.getpixel((x+dx,y+dy))[3] == 0
                   for dx in (-1,0,1) for dy in (-1,0,1) if dx or dy):
                boundary += 1
                bad += p[:3] != rgb[0]
    checks['f'] = boundary > 0 and bad == 0 and bool(colors - {rgb[0]})
    return {'checks':checks,'passed':all(checks.values()),'size':list(im.size),'mode':im.mode,
            'native_bbox':list(bbox) if bbox else None,'native_long_edge':long_edge,
            'native_center_offset':center,'native_padding':padding,'color_count':len(colors),
            'colors':sorted('#%02X%02X%02X'%c for c in colors),
            'outside_palette':sorted('#%02X%02X%02X'%c for c in colors-set(rgb)),
            'boundary_pixels':boundary,'incorrect_boundary_pixels':bad,
            'alpha_values':sorted({p[3] for p in pixels})}


def print_check(item_id, metrics):
    print(item_id + ' ' + ' '.join(f'({k})={"PASS" if v else "FAIL"}' for k,v in metrics['checks'].items())
          + f' colors={metrics["color_count"]} edge={metrics["native_long_edge"]} center={metrics["native_center_offset"]}')


def native_sheet(rows, filename):
    font = ImageFont.truetype(str(FONT),10)
    cols, cellw, cellh = 6,150,102
    sheet = Image.new('RGB',(cols*cellw,math.ceil(len(rows)/cols)*cellh),'#202A2D')
    d = ImageDraw.Draw(sheet)
    for i,row in enumerate(rows):
        x,y = i%cols*cellw,i//cols*cellh
        path = ROOT / row['target']
        icon = load_rgba(path).resize((32,32),Image.Resampling.NEAREST)
        sheet.paste(icon,(x+8,y+8),icon)
        # Integer x2 inspection with a grid at native pixel boundaries.
        zoom = icon.resize((64,64),Image.Resampling.NEAREST)
        sheet.paste(zoom,(x+72,y+5),zoom)
        for v in range(0,65,2):
            d.line((x+72+v,y+5,x+72+v,y+69), fill='#3B4949')
            d.line((x+72,y+5+v,x+136,y+5+v), fill='#3B4949')
        d.text((x+4,y+76),row['id'],font=font,fill='#EDF2EE')
    sheet.save(REPORT/filename)


def effects_sheet(rows):
    font = ImageFont.truetype(str(FONT),12)
    sheet = Image.new('RGB',(900,240),'#202A2D')
    d = ImageDraw.Draw(sheet)
    for i,row in enumerate(rows):
        x = i*128+8
        d.text((x,8),row['id'],font=font,fill='#EDF2EE')
        im = load_rgba(ROOT/row['target'])
        for y,size in ((40,18),(80,28),(130,40)):
            thumb=im.resize((size,size),Image.Resampling.NEAREST)
            sheet.paste(thumb,(x,y),thumb)
    sheet.save(REPORT/'Effects_18_28_40px.png')


def audit_ui(rgb):
    """Analyze RGB assets and neutral tint masks without changing any of them."""
    records = []
    # Read-back gives color values as floats. Add documented preserved semantic
    # colors and recolor export values, independent of the smaller icon palette.
    direction = set(rgb) | {ImageColor.getrgb(h) for h in (
        '#0B1114','#3B8D7B','#A4B9B1','#E5484D','#5BB8E8','#F5C542','#A274FF')}
    for folder in ('Assets/_JungleMaze/Art/Hud','Assets/_JungleMaze/Art/UI/Menu'):
        for path in sorted((ROOT/folder).glob('*.png')):
            with Image.open(path) as source:
                source_size, source_mode = source.size, source.mode
                im = source.convert('RGBA')
            counts = Counter(p[:3] for p in flattened(im) if p[3])
            neutral = bool(counts) and all(r==g==b for r,g,b in counts)
            outside = counts.keys()-direction
            records.append({'path':relative(path),'sha256':digest(path),'size':list(source_size),
                            'source_mode':source_mode,'alpha_extrema':list(im.getchannel('A').getextrema()),
                            'neutral_tint_mask':neutral,'visible_color_count':len(counts),
                            'outside_direction_a':[{'hex':'#%02X%02X%02X'%c,'pixels':counts[c]} for c in sorted(outside)],
                            'assessment':'neutral mask; final palette/contrast depends on existing runtime tint' if neutral else
                                         'uses colors beyond direction A; illustration/semantic review required' if outside else 'direction A colors',
                            'modified':False})
    return records


def run(args):
    # Import helpers lazily so importing the checker never produces assets.
    from normalize_item_icons import ROSTER, sprite_rect, contact_sheet
    data, rgb = palette()
    rows = list(csv.DictReader(ROSTER.open(encoding='utf-8-sig')))
    if len(rows)!=34 or len({r['id'] for r in rows})!=34:
        raise ValueError('Expected exactly 34 distinct item IDs')
    for kind,file in [('Weapon','weapons'),('Equipment','equipments')]:
        roster = json.loads((ROOT/f'Assets/StreamingAssets/JungleMaze/balance/{file}.json').read_text(encoding='utf-8'))
        if {r['id'] for r in rows if r['loai']==kind} != {r['id'] for r in roster}:
            raise ValueError(f'{kind}: canonical roster mismatch')
    effect_paths = sorted((ICONS/'Effects').glob('*.png'))
    if {p.stem for p in effect_paths} != {'Poison','Slow','Invisible','Stun','Root','Reverse','CcImmune'}:
        raise ValueError('Expected seven canonical effect icons')
    if args.check_only:
        results = []
        for item_id,path in [(r['id'],ROOT/r['dich_chuan_hoa']) for r in rows] + [(p.stem,p) for p in effect_paths]:
            with Image.open(path) as encoded:
                m = validate(encoded,rgb)
            print_check(item_id,m)
            results.append(m['passed'])
        if not all(results):
            raise SystemExit('Native32 validation failed')
        print(f'PASS {len(results)}/{len(results)} icons; all (a)-(f)')
        return
    if args.allow_missing or args.hand or args.rope:
        raise ValueError('--native32 is complete-only and does not accept generated HAND/ROPE sources')
    authored_colors = dict(data['colors'], transparent=(0,0,0,0))
    batch = []
    for row in rows:
        item_id = row['id']
        if item_id in RECIPE_IDS:
            im = draw_item(item_id,authored_colors)
            source_path = Path(__file__).with_name('native32_recipes.py')
            provenance = {'source':relative(source_path),'source_kind':'Hand-authored native32 recipe',
                          'slice':f'draw_item({item_id})','source_sha256':digest(source_path),
                          'reference_source':row['sprite_de_xuat'],
                          'replacement_reason':'Direct 32px redraw: generated/undersized/misleading legacy art; no source resampling'}
            allow_resize = False
        else:
            source_path = ROOT/row['sprite_de_xuat']
            im = load_rgba(source_path)
            named = {'BELL':'bell_2','METEORITE_FRAGMENT':'meteorite_throwing-Sheet_2','SHOTGUN':'shotgun 1_0'}
            frame = named.get(item_id,'full image')
            rect = sprite_rect(source_path,frame) if item_id in named else (0,0,*im.size)
            im = im.crop(rect)
            provenance = {'source':relative(source_path),'source_kind':'JSurviv nearest + shared palette',
                          'slice':frame,'slice_rect_png':list(rect),'source_sha256':digest(source_path)}
            allow_resize = max(im.size)>32
            # Preserve the identifiable long blade/barrel with broad 32px planes.
            if item_id in {'KNIFE','SHOTGUN'}:
                im = im.crop(im.getbbox())
                im = im.resize((im.width,round(im.height*{'KNIFE':1.5,'SHOTGUN':1.8}[item_id])),Image.Resampling.NEAREST)
                im = im.rotate({'KNIFE':35,'SHOTGUN':25}[item_id],resample=Image.Resampling.NEAREST,expand=True)
                provenance['readability_adjustment']='nearest local thickness and diagonal orientation, preserving source silhouette'
        if item_id in {'TREE_BRANCH','SHOTGUN','AXE','MACE','RUBBER_GUN'}:
            provenance['native_material_repair']='Native32 material planes relit for charcoal; same source alpha and silhouette'
            provenance['material_repair_recipe']='Tools/Art/native32_pipeline.py:repair_material'
        native, transform = finish_native(im,rgb,allow_resize,item_id,data['colors'])
        target = ROOT/row['dich_chuan_hoa']
        batch.append((native,target,{'id':item_id,'kind':row['loai'],'target':relative(target),**provenance,**transform}))
    for target in effect_paths:
        source_path = ROOT/f'Docs/Art/Production_A/UI/{target.stem}_native32.png'
        im = load_rgba(source_path)
        if im.size != (32,32):
            raise ValueError(f'Expected authored 32px effect source: {source_path}')
        native, transform = finish_native(im,rgb,True)
        batch.append((native,target,{'id':target.stem,'kind':'Effect','target':relative(target),
                      'source':relative(source_path),'source_kind':'Existing authored32 effect; palette + outline + fit',
                      'source_sha256':digest(source_path),'slice':'full native32 image',**transform}))
    failures = []
    for native,target,record in batch:
        if not target.is_file():
            raise ValueError(f'Runtime target missing: {target}')
        result = native.resize((64,64),Image.Resampling.NEAREST)
        metrics = validate(result,rgb)
        record.update(metrics)
        print_check(record['id'],metrics)
        if not metrics['passed']:
            failures.append(record['id'])
    if failures:
        raise SystemExit('Preflight failed; no runtime PNGs written: '+', '.join(failures))
    raw_hashes = [hashlib.sha256(native.tobytes()).hexdigest() for native,_,_ in batch]
    if len(set(raw_hashes)) != len(raw_hashes):
        raise ValueError('Duplicate icon artwork')
    REPORT.mkdir(exist_ok=True,parents=True)
    NATIVE.mkdir(exist_ok=True)
    for native,target,record in batch:
        # Runtime targets must already exist; never rename or create an asset ID.
        native.save(NATIVE/f'{record["id"]}_32.png')
        record['native_source'] = relative(NATIVE/f'{record["id"]}_32.png')
        native.resize((64,64),Image.Resampling.NEAREST).save(target)
        record['output_sha256'] = digest(target)
        # Check the encoded PNG, not just the pre-encoding image.
        with Image.open(target) as image:
            encoded = validate(image,rgb)
        if not encoded['passed']:
            raise ValueError(f'Encoded output failed: {record["id"]}')
    items = [record for _,_,record in batch if record['kind']!='Effect']
    effects = [record for _,_,record in batch if record['kind']=='Effect']
    output = {'canvas':[64,64],'native_size':32,'scale':2,'method':'nearest',
              'palette':relative(PALETTE_PATH),'palette_sha256':digest(PALETTE_PATH),'palette_color_count':len(rgb),
              'produced':len(items),'expected':34,'missing':[], 'effects_produced':len(effects),
              'runtime_rgba_bytes_without_mips':len(items)*64*64*4,
              'unity_import_and_binding':'Not run in this asset-only session; existing bindings retained by filename and unchanged .meta',
              'icons':items,'effects':effects}
    (REPORT/'Normalization.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    fields = ['id','kind','source_kind','source','slice','target','source_sha256','native_source','output_sha256','reference_source','replacement_reason']
    with (REPORT/'Provenance.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer = csv.DictWriter(f,fieldnames=fields,extrasaction='ignore')
        writer.writeheader()
        writer.writerows(items+effects)
    produced = {r['id']:r for r in items}
    for pixels in (18,28,40):
        for background,suffix in [('#202A2D','slot'),('#FFFFFF','white'),('#000000','black'),('#28534F','selected')]:
            contact_sheet(rows,produced,pixels,background,suffix)
    native_sheet(items,'Contact_32px_grid.png')
    effects_sheet(effects)
    native_sheet(effects,'Effects_32px_grid.png')
    (REPORT/'HudMenu_ReadOnly_Audit.json').write_text(json.dumps(audit_ui(rgb),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('PASS 34 items + 7 effects; no Unity/HUD/menu writes')
