"""Original UI-03 pixel geometry; writes only the overlay PNG/doc folders.

Run with the approved Python: -I -X utf8 Tools/Art/build_ui03_overlay_assets.py
No Unity, importer, manifest, existing art or .meta writes. No randomness.
"""
import hashlib
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from PIL import Image, ImageDraw, ImageFont
from native32_pipeline import finish_native, palette

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/_JungleMaze/Art/UI/MatchOverlay'
DOC = ROOT / 'Docs/Art/Production_A/MatchOverlay'
NEAREST = Image.Resampling.NEAREST
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)
COLORS, RGB = palette()
P = COLORS['colors']
SLIDERS = [f'MenuPanel/{n}Row/{n}/HandleArea/Handle'
           for n in ('Master', 'Sfx', 'Music', 'Sensitivity')]
TOGGLES = ['MenuPanel/MusicRow/MusicEnabled', 'MenuPanel/ShakeRow/ScreenShake']


def spec(asset_id, name, size, elements, screen, tint, kind='white mask', border=0, note=''):
    return dict(id=asset_id, path=f'Assets/_JungleMaze/Art/UI/MatchOverlay/{name}.png',
                size=list(size), kind=kind, suggested_tint=tint,
                border_LTRB=[border]*4, ppu=100, filter='Point', mipmaps=False,
                compression='None', prefab_elements=elements, on_screen_size=list(screen),
                binding='imported, not bound', note=note)


SPECS = [
    spec('ov_slider_handle', 'SliderHandle', (48,48), SLIDERS, (20,20), P['white'],
         note='Vertical fader cap with one horizontal groove; also previewed at 24px.'),
    spec('ov_toggle_box', 'ToggleBox', (48,48), [p+'/Background' for p in TOGGLES],
         (24,24), P['metal_dark'], border=12, note='Sliced; empty centre.'),
    spec('ov_toggle_check', 'ToggleCheck', (32,32), [p+'/Background/Checkmark' for p in TOGGLES],
         (16,16), P['gold']),
    spec('ov_zone_ring', 'ZoneRing', (256,256), ['Killcam/Board/Zone'], (480,480), P['purple'],
         note='4px ring at source; scales with the existing zone rect. Alpha controlled by Image.'),
    spec('ov_grid_cell', 'KillcamGrid', (48,48), ['Killcam/Board'], (48,48), P['metal_dark'],
         note='Tiled at 48px/cell, 1px top/left lines. Board has no Image slot yet.'),
    spec('ov_player_dot', 'PlayerDot', (32,32), ['Killcam/Board/Player0..9'], (18,18), P['white'],
         note='Filled disc; outline is a separate mask behind this Image.'),
    spec('ov_player_dot_outline', 'PlayerDotOutline', (32,32), ['Killcam/Board/Player0..9'],
         (18,18), P['ink'], note='1px native outer rim; same rect/pivot; requires overlay Image.'),
    spec('ov_killer_mark', 'KillerMark', (32,32), ['Killcam/Board/Player0..9'], (18,18), P['gold'],
         note='Ring/crosshair over the killer dot only; requires overlay Image.'),
]
ICON_SPECS = [
    ('ov_cause_player', 'CausePlayer', ['Eliminated/Frame/Cause'], 'Crossed blades; no gore.'),
    ('ov_cause_zone', 'CauseZone', ['Eliminated/Frame/Cause'], 'Purple ring with four inward chevrons around a gold dot.'),
    ('ov_cause_left', 'CauseLeft', ['Eliminated/Frame/Cause'], 'Doorway with a figure walking out; no arrow, unlike IconExit.'),
    ('ov_cause_disconnect', 'CauseDisconnect', ['Eliminated/Frame/Cause'], 'Signal arcs crossed by a red slash.'),
    ('ov_cause_unknown', 'CauseUnknown', ['Eliminated/Frame/Cause'], 'Question mark.'),
    ('ov_icon_volume', 'IconVolume', ['MenuPanel/MasterRow', 'MenuPanel/SfxRow'], 'Speaker.'),
    ('ov_icon_music', 'IconMusic', ['MenuPanel/MusicRow'], 'Music note.'),
    ('ov_icon_mouse', 'IconMouse', ['MenuPanel/SensitivityRow'], 'Light mouse body with jade left button.'),
    ('ov_icon_shake', 'IconShake', ['MenuPanel/ShakeRow'], 'Screen frame between paired shake ticks.'),
    ('ov_icon_exit', 'IconExit', ['MenuPanel/Leave', 'LeaveConfirm/Box/Confirm'], 'Leave match.'),
    ('ov_icon_eye', 'IconSpectate', ['Spectator/Watching'], 'Eye.'),
]
for asset_id, name, elements, note in ICON_SPECS:
    SPECS.append(spec(asset_id, name, (64,64), elements, (28,28), '#FFFFFF',
                      'colored icon, native32 x2 nearest', note=note +
                      ' Suggested 28px preview; current prefab has no icon Image slot.'))


def draw_masks():
    out = {}
    def canvas(name, size):
        im = Image.new('RGBA', (size,size)); out[name] = im
        return ImageDraw.Draw(im)
    d = canvas('SliderHandle', 48)
    # Fader cap, not a square with bars: that read as a pause button.
    d.polygon([(12,4),(35,4),(37,6),(37,41),(35,43),(12,43),(10,41),(10,6)], fill=WHITE)
    d.rectangle((14,22,33,25), fill=CLEAR)
    d = canvas('ToggleBox',48)
    d.rectangle((4,4,43,43), outline=WHITE, width=4)
    d = canvas('ToggleCheck',32)
    d.polygon([(4,15),(8,11),(13,16),(24,5),(28,9),(13,25)], fill=WHITE)
    d = canvas('ZoneRing',256)
    d.ellipse((4,4,251,251), outline=WHITE, width=4)
    d = canvas('KillcamGrid',48)
    d.line((0,0,47,0), fill=WHITE); d.line((0,0,0,47), fill=WHITE)
    d = canvas('PlayerDot',32); d.ellipse((5,5,26,26), fill=WHITE)
    d = canvas('PlayerDotOutline',32)
    d.ellipse((4,4,27,27), fill=WHITE); d.ellipse((5,5,26,26), fill=CLEAR)
    d = canvas('KillerMark',32)
    d.ellipse((3,3,28,28), outline=WHITE, width=2)
    for xy in ((15,1,16,7),(15,24,16,30),(1,15,7,16),(24,15,30,16)):
        d.rectangle(xy, fill=WHITE)
    return out


def draw_icon(name):
    im = Image.new('RGBA',(32,32)); d = ImageDraw.Draw(im)
    def box(xy,c): d.rectangle(xy,fill=P[c])
    def poly(xy,c): d.polygon(xy,fill=P[c])
    def line(xy,c,w=1): d.line(xy,fill=P[c],width=w)
    def ellipse(xy,c): d.ellipse(xy,fill=P[c])
    if name == 'CausePlayer':
        for points in (
            [(5,3),(9,4),(25,22),(22,25),(4,9)],
            [(26,3),(22,4),(6,22),(9,25),(27,9)]): poly(points,'metal')
        line([(6,5),(22,23)],'metal_light',2)
        line([(25,5),(9,23)],'white',2)
        line([(4,20),(11,27)],'gold',2); line([(20,27),(27,20)],'gold',2)
        line([(4,27),(8,24)],'wood_light',3); line([(23,24),(27,27)],'wood_light',3)
    elif name == 'CauseZone':
        ellipse((8,8,23,23),'purple_dark'); ellipse((9,9,22,22),'purple')
        d.ellipse((12,12,19,19),fill=CLEAR)
        ellipse((14,14,17,17),'gold')
        # Small inward chevrons clear of the ring: the zone closes in. Ring segments
        # read as a lifebuoy, large chevrons as a star.
        chevron = [(12,3),(15,5),(16,5),(19,3),(19,5),(16,7),(15,7),(12,5)]
        for _ in range(4):
            poly(chevron,'metal_light')
            chevron = [(31-y,x) for x,y in chevron]
    elif name == 'CauseLeft':
        # Figure walking out of a doorway; no arrow, so it differs from IconExit.
        box((3,3,14,28),'jade'); box((5,5,12,28),'jade_dark')
        ellipse((18,3,22,7),'gold_light')
        line([(20,9),(18,18)],'gold',3)
        line([(19,11),(25,15)],'gold',2); line([(19,11),(14,16)],'gold',2)
        line([(18,18),(15,27)],'gold',2); line([(18,18),(23,22),(23,27)],'gold',2)
    elif name == 'IconExit':
        box((4,3,18,28),'metal_dark'); box((6,5,16,26),'gold')
        box((9,7,18,25),'jade_dark'); box((9,7,10,25),'jade')
        poly([(15,13),(22,13),(22,8),(28,16),(22,23),(22,18),(15,18)],'gold_light')
        box((5,27,21,28),'metal_light')
    elif name == 'CauseDisconnect':
        for r in (13,9,5):
            d.arc((15-r,24-r,16+r,25+r),218,322,fill=P['jade'],width=2)
        ellipse((14,23,17,26),'jade')
        line([(5,5),(26,26)],'ink',4); line([(5,5),(26,26)],'red',2)
    elif name == 'CauseUnknown':
        poly([(6,9),(9,4),(22,4),(26,8),(26,14),(18,20),(18,22),
              (13,22),(13,17),(21,12),(21,9),(18,8),(12,8),(10,12),(6,12)],'gold')
        line([(9,8),(11,6),(21,6)],'gold_light',2)
        box((13,25,18,28),'jade')
    elif name == 'IconVolume':
        box((4,12,10,21),'metal')
        poly([(10,12),(18,5),(18,28),(10,21)],'jade')
        line([(11,13),(16,9),(16,24)],'metal_light',2)
        line([(22,11),(25,14),(25,19),(22,22)],'gold',2)
        line([(25,6),(28,11),(28,22),(25,27)],'gold_light',2)
    elif name == 'IconMusic':
        poly([(12,7),(27,3),(27,23),(23,23),(23,10),(16,12),(16,26),(12,26)],'jade')
        poly([(14,7),(25,4),(25,7),(14,10)],'metal_light')
        ellipse((4,22,15,28),'gold'); ellipse((18,19,27,25),'gold')
        line([(7,23),(12,23)],'gold_light',2)
    elif name == 'IconMouse':
        # Light body for contrast at 28 px; left button fills the body's own corner.
        d.rounded_rectangle((6,3,25,28),radius=9,fill=P['metal_light'])
        px = im.load(); jade = tuple(int(P['jade'][i:i+2],16) for i in (1,3,5)) + (255,)
        for y in range(3,14):
            for x in range(6,16):
                if px[x,y][3]: px[x,y] = jade
        line([(16,3),(16,14)],'metal_dark'); line([(6,14),(25,14)],'metal_dark')
        box((15,6,17,10),'ink')
        box((20,18,22,25),'white')
    elif name == 'IconShake':
        # Screen between paired shake ticks; an offset ghost frame read as "copy".
        box((10,5,21,26),'metal_light'); box((12,7,19,24),'jade_dark')
        for xy in ([(7,9),(7,22)],[(4,12),(4,19)],[(24,9),(24,22)],[(27,12),(27,19)]):
            line(xy,'gold',2)
    elif name == 'IconSpectate':
        poly([(3,16),(8,10),(13,7),(18,7),(23,10),(28,16),
              (23,22),(18,25),(13,25),(8,22)],'metal_light')
        poly([(5,16),(10,12),(14,10),(18,10),(23,13),(26,16),
              (22,20),(18,22),(14,22),(9,19)],'jade_dark')
        ellipse((11,10,21,22),'jade'); ellipse((14,13,18,19),'ink')
        box((13,12,15,14),'white')
    else:
        raise ValueError(name)
    # Shared pipeline adds a 1-native-pixel ink outline, centres, quantizes only
    # to the existing palette, and never loads source art from another game.
    native, _ = finish_native(im, RGB, False)
    return native


def reuse_mapping():
    old = json.loads((ROOT/'Docs/Art/Production_A/UI/asset_mapping.json').read_text(encoding='utf-8'))
    bindings = {
        'HudRound10.png': ['MenuPanel', 'Eliminated/Frame', 'Spectator/Watching',
                           'Spectator/TargetCard', 'LeaveConfirm/Box'],
        'HudRound6.png': ['Spectator/List/Row1..9', 'Spectator/KeyBar'],
        'HudRound6Stroke2.png': ['Spectator/List/Row1..9 (selected overlay)'],
        'HudRound6Stroke1.png': ['Spectator/List/Row1..9 (outline overlay)'],
        'HudRound6Dashed.png': ['Spectator/List/Row1..9 (optional empty style)'],
        'AFrameEdge96.png': ['MenuPanel', 'Eliminated/Frame', 'Spectator/TargetCard', 'LeaveConfirm/Box'],
        'ABevel96.png': ['MenuPanel', 'Eliminated/Frame', 'Spectator/TargetCard', 'LeaveConfirm/Box'],
        'AFrameEdgeSlot128.png': ['Spectator/List/Row1..9'],
        'ABevelSlot128.png': ['Spectator/List/Row1..9'],
        'BarTrack.png': [p.replace('HandleArea/Handle','Background') for p in SLIDERS] +
                        ['Spectator/TargetCard/Hp/Track', 'Killcam/Board/Player0..9/Hp'],
        'BarFill.png': [p.replace('HandleArea/Handle','FillArea/Fill') for p in SLIDERS] +
                       ['Spectator/TargetCard/Hp/Track/Fill', 'Killcam/Board/Player0..9/Hp/Fill'],
        'KeyCap.png': ['Spectator/KeyBar/Keys (optional keycap Images; currently text)'],
        'ToastBg.png': ['Notice'],
        'ButtonPrimary.png': ['MenuPanel/Resume', 'Eliminated/Frame/Spectate',
                              'Spectator/Home', 'LeaveConfirm/Box/Stay'],
        'ButtonSecondary.png': ['MenuPanel/Defaults', 'MenuPanel/Leave', 'Eliminated/Frame/Home',
                                'Spectator/Killer', 'Killcam/Skip', 'LeaveConfirm/Box/Confirm'],
        'ButtonGhost.png': ['Killcam/Skip (optional style)'],
    }
    out = []
    for item in old:
        name = Path(item['path']).name
        if name not in bindings: continue
        tint = '#FFFFFF' if name.startswith('Button') else P['metal_dark'] if name.startswith('ABevel') else P['ink'] if name.startswith('AFrame') else P['gold'] if 'Stroke2' in name else P['panel']
        out.append(dict(path=item['path'], size=item['size'], kind=item['role'],
                        suggested_tint=tint, border_LTRB=item['border_LTRB'], ppu=100,
                        filter='Point', mipmaps=False, compression='None',
                        prefab_elements=bindings[name], sha256=hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest(),
                        note='Reuse in place; do not copy. Base -> edge -> bevel -> selection. '+
                        ('Colored button: keep white tint; existing baked ghost alpha retained.' if name.startswith('Button') else
                         'BarFill uses gold for sliders / red for HP; BarTrack uses panel.')))
    assert len(out) == len(bindings)
    return out


def tinted(im, color):
    out = Image.new('RGBA',im.size,color); out.putalpha(im.getchannel('A'))
    return out


def contact(images):
    sheet = Image.new('RGBA',(1380,1720),'#0B1114'); d = ImageDraw.Draw(sheet)
    font_path = ROOT/'Assets/_JungleMaze/Art/Fonts/BeVietnamPro-Medium.ttf'
    title = ImageFont.truetype(str(font_path),22); font = ImageFont.truetype(str(font_path),14)
    d.text((24,14),'UI-03 / A charcoal · jade · soft gold',font=title,fill=P['gold'])
    d.text((24,48),'Export 1× and screen pixels / Point sampling / masks previewed with suggested Image tint',font=font,fill=P['metal_light'])
    zone = images['ZoneRing']
    d.text((24,87),'ZoneRing / 256×256 source 1×',font=font,fill=P['white'])
    sheet.alpha_composite(tinted(zone,P['purple']),(24,120))
    d.text((360,87),'480×480 current Zone rect (dynamic in replay)',font=font,fill=P['white'])
    sheet.alpha_composite(tinted(zone.resize((480,480),NEAREST),P['purple']),(360,120))
    # Repeat the actual grid tile, not a resampled approximation.
    for yy in range(144,384,48):
        for xx in range(960,1248,48):
            sheet.alpha_composite(tinted(images['KillcamGrid'],P['metal_dark']),(xx,yy))
    d.text((940,410),'Grid / 6×5 tiles, 48px each',font=font,fill=P['metal_light'])
    # Actual shared dot rect and tint composition at on-screen 18px.
    for i, color in enumerate((P['white'],P['jade'],P['gold'])):
        xy=(980+i*70,470)
        sheet.alpha_composite(tinted(images['PlayerDotOutline'].resize((18,18),NEAREST),P['ink']),xy)
        sheet.alpha_composite(tinted(images['PlayerDot'].resize((18,18),NEAREST),color),xy)
        if i == 2: sheet.alpha_composite(tinted(images['KillerMark'].resize((18,18),NEAREST),P['gold']),xy)
    d.text((940,514),'Dot + outline / killer overlay / 18px',font=font,fill=P['metal_light'])
    others = [s for s in SPECS if Path(s['path']).stem != 'ZoneRing']
    for i,s in enumerate(others):
        x=24+(i%3)*452; y=632+(i//3)*176
        d.rectangle((x-6,y-4,x+428,y+157),fill=P['panel'])
        name=Path(s['path']).stem; im=images[name]
        d.text((x,y),name,font=font,fill=P['white'])
        view=tinted(im,s['suggested_tint']) if s['kind']=='white mask' else im
        sheet.alpha_composite(view,(x+8,y+37))
        screen=tuple(s['on_screen_size'])
        sheet.alpha_composite(view.resize(screen,NEAREST),(x+153,y+45))
        d.text((x+8,y+110),f'1× {im.width}×{im.height}',font=font,fill=P['metal_light'])
        d.text((x+143,y+110),f'Screen {screen[0]}×{screen[1]}',font=font,fill=P['metal_light'])
        if name == 'SliderHandle':
            sheet.alpha_composite(view.resize((24,24),NEAREST),(x+285,y+45))
            d.text((x+278,y+110),'24×24',font=font,fill=P['metal_light'])
    sheet.save(DOC/'Contact.png')


def main():
    images=draw_masks()
    natives={name:draw_icon(name) for _,name,_,_ in ICON_SPECS}
    images.update({name:im.resize((64,64),NEAREST) for name,im in natives.items()})
    # Validate the full in-memory batch before writing any asset.
    from test_ui03_overlay_validation import validate_image
    for s in SPECS:
        checks=validate_image(images[Path(s['path']).stem],s)
        if not all(checks.values()): raise ValueError((s['path'],checks))
    ART.mkdir(parents=True,exist_ok=True); DOC.mkdir(parents=True,exist_ok=True)
    for name,im in images.items(): im.save(ART/f'{name}.png')
    for name,im in natives.items(): im.save(DOC/f'{name}_native32.png')
    mapping=dict(style='A charcoal / jade / soft gold', assets=SPECS, reuse=reuse_mapping(),
                 import_settings='Suggested only; Unity import/binding not performed. No .meta writes.',
                 border_order='left, top, right, bottom',
                 integration_notes=[
                     'Prefab element names from MatchOverlayPrefabBuild.cs; root MatchOverlay omitted.',
                     'Icons target existing parent/text/button names; no icon Image slots currently exist.',
                     'Additional Images and cause selection must be handled by integration without changing layout.',
                     'PlayerDotOutline is a WHITE mask tinted ink behind PlayerDot, never baked black RGB.',
                     'KillcamGrid is tiled across Board at 48px/cell; do not stretch a single tile.',
                     'KillerMark uses killer state; ZoneRing follows the existing Zone rect.'])
    (DOC/'asset_mapping.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    contact(images)
    print(f'PASS preflight: {len(images)} overlay PNGs, {len(natives)} native32 sources; contact and mapping written.')


if __name__ == '__main__':
    main()
