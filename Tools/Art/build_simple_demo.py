"""Compose a local art preview from read-only Figma SVG exports; no Unity changes."""
from pathlib import Path
import base64
import copy
import json
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Docs/Art/directions'
NS = 'http://www.w3.org/2000/svg'
ET.register_namespace('', NS)


def tag(name):
    return f'{{{NS}}}{name}'


def repair(value):
    if not value:
        return value
    try:
        return value.encode('latin1').decode('utf-8')
    except (UnicodeEncodeError, UnicodeDecodeError):
        return value


def load(name):
    node = ET.parse(ART / name).getroot()
    for child in node.iter():
        child.text = repair(child.text)
        child.tail = repair(child.tail)
        for key, value in list(child.attrib.items()):
            child.set(key, repair(value))
    return node


def find(node, name):
    return next(n for n in node.iter() if n.get('id') == name)


def text(parent, x, y, content, size=16, weight='500', color='#F3EEDB'):
    node = ET.SubElement(parent, tag('text'), {
        'x': str(x), 'y': str(y), 'font-family': 'Be Vietnam Pro',
        'font-size': str(size), 'font-weight': weight, 'fill': color,
    })
    node.text = content


hud = load('H_hud_figma_source.svg')
world = find(hud, 'World (placeholder)')
zone = [copy.deepcopy(n) for n in world if 'ZON-01' in n.get('id', '')]
world.clear()
world.set('id', 'World - Simple forest concept')
world.append(load('H_world.svg'))
world.extend(zone)

icons = json.loads((ART / 'H_icons.json').read_text(encoding='utf-8'))
slots = [n for n in hud.iter() if n.get('id', '').startswith('Slot')]
next_icon = 0
for slot in slots:
    placeholders = [n for n in slot if n.get('id', '').startswith('Icon (ART-04)')]
    names = [''.join(n.itertext()).strip() for n in slot if n.get('id', '').startswith('Name')]
    if not placeholders or not names:
        continue
    placeholder = placeholders[0]
    icon = next(n for n in icons if n['key'] == names[0])
    next_icon += 1
    replacement = ET.fromstring(
        f'<svg xmlns="{NS}" x="{placeholder.get("x", "0")}" '
        f'y="{placeholder.get("y", "0")}" width="28" height="28" viewBox="0 0 32 32">'
        + icon['path'] + '</svg>'
    )
    replacement.set('id', f'Art - {icon["key"]}')
    slot.insert(list(slot).index(placeholder), replacement)
    slot.remove(placeholder)

menu = load('H_menu_figma_source.svg')
key_art = find(menu, 'Key art (placeholder)')
key_art.clear()
key_art.set('id', 'Key art - Minimal jungle island')
island = load('H_island.svg')
island.set('x', '664')
island.set('y', '100')
key_art.append(island)

board = ET.Element(tag('svg'), {
    'width': '2696', 'height': '1000', 'viewBox': '0 0 2696 1000',
    'role': 'img', 'aria-label': 'H - Rừng gọn: gameplay và menu dựa trên Figma v2',
})
style = ET.SubElement(board, tag('style'))
font_dir = ROOT / 'Assets/_JungleMaze/Art/Fonts'
rules = []
for family, weight, filename in [
    ('Be Vietnam Pro', '500', 'BeVietnamPro-Medium.ttf'),
    ('Be Vietnam Pro', '700', 'BeVietnamPro-Bold.ttf'),
    ('Pixelify Sans', '100 900', 'PixelifySans-VariableFont_wght.ttf'),
]:
    encoded = base64.b64encode((font_dir / filename).read_bytes()).decode('ascii')
    rules.append(f'@font-face{{font-family:"{family}";font-weight:{weight};src:url(data:font/ttf;base64,{encoded}) format("truetype");}}')
style.text = '\n'.join(rules)
ET.SubElement(board, tag('rect'), {'width': '2696', 'height': '1000', 'fill': '#0E1712'})
text(board, 48, 75, 'H · Rừng gọn', 34, '700')
text(board, 48, 112, 'Mảng màu phẳng · Bóng cứng nhẹ · Khung HUD mỏng · Bám Figma v2', 18, color='#A7B5A3')
text(board, 48, 180, '01 / Gameplay + lớp phản hồi', 16, '700')
text(board, 1368, 180, '02 / Menu chính', 16, '700')
hud.set('x', '48')
hud.set('y', '210')
menu.set('x', '1368')
menu.set('y', '210')
board.extend([hud, menu])
text(board, 48, 970, 'Bản xem trước từ Figma · Chưa ghi vào file Figma (chỉ đọc) · Chưa thay asset Unity', 14, color='#A7B5A3')
output = ART / 'H_simple_forest.svg'
ET.ElementTree(board).write(output, encoding='utf-8', xml_declaration=True)
assert next_icon == 6, f'Expected 6 icons, got {next_icon}'
print(json.dumps({'svg': str(output), 'icons': next_icon, 'canvas': [2696, 1000]}, ensure_ascii=False))
