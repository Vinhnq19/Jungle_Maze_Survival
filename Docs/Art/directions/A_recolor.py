"""Recolor the exact Figma A export without changing its scene geometry."""
from pathlib import Path
import base64, re, xml.etree.ElementTree as ET

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
SOURCE=OUT/'A_figma_original.svg'
source=SOURCE.read_text(encoding='utf-8')
svg=source
palette={
 '#0b0f0d':'#0B1114', '#3b2a1a':'#202A2D', '#1e140c':'#0C1417',
 '#6b4a2b':'#526B69', '#f7ebd0':'#EDF2EE', '#c9b48e':'#A6BBB7',
 '#8bc34a':'#63BAA5', '#a7b5a3':'#A4B9B1', '#6e8070':'#77908A',
}
# Exact export coordinates are selectors, not geometry mutations.
targeted={
 '<rect x="494" y="870" width="60" height="60" fill="#8BC34A" fill-opacity="0.25"/>':
 '<rect x="494" y="870" width="60" height="60" fill="#28534F" fill-opacity="0.25"/>',
 '<rect x="494" y="870" width="60" height="60" stroke="#8BC34A" stroke-width="4"/>':
 '<rect x="494" y="870" width="60" height="60" stroke="#C6AE78" stroke-width="4"/>',
 '<rect x="1481.5" y="537.5" width="297" height="49" fill="#8BC34A"/>':
 '<rect x="1481.5" y="537.5" width="297" height="49" fill="#3B8D7B"/>',
 '<rect x="1481.5" y="537.5" width="297" height="49" stroke="#8BC34A" stroke-width="3"/>':
 '<rect x="1481.5" y="537.5" width="297" height="49" stroke="#C6AE78" stroke-width="3"/>',
}
for before,after in targeted.items():
 if svg.count(before)!=1:raise ValueError('Target selector is not unique: '+before)
 svg=svg.replace(before,after)
logo=re.compile(r'<text fill="#8BC34A"[^>]*><tspan[^>]*>LASTAWAY</tspan></text>')
if len(logo.findall(svg))!=1:raise ValueError('LASTAWAY logo selector is not unique')
svg=logo.sub(lambda m:m.group(0).replace('fill="#8BC34A"','fill="#C6AE78"',1),svg)
svg=re.sub(r'#[0-9a-fA-F]{6}',lambda m:palette.get(m.group(0).lower(),m.group(0)),svg)

# Compare the scene tree before embedding fonts. Only fill/stroke and hex swatch
# labels may differ. Opacity, dimensions, paths, text and font attributes stay exact.
def normalized(text):
 root=ET.fromstring(text)
 def walk(n):
  attrs=tuple(sorted((k,v)for k,v in n.attrib.items()if k not in ('fill','stroke')))
  body=re.sub(r'#[0-9a-fA-F]{6}','HEX',n.text or '')
  return n.tag,attrs,body,tuple(walk(c)for c in n)
 return walk(root)
if normalized(source)!=normalized(svg):raise ValueError('Recolor changed scene structure or geometry')

fonts=ROOT/'Assets/_JungleMaze/Art/Fonts'
rules=[]
for family,weight,file in [('Be Vietnam Pro',500,'BeVietnamPro-Medium.ttf'),('Be Vietnam Pro',700,'BeVietnamPro-Bold.ttf'),('Pixelify Sans','100 900','PixelifySans-VariableFont_wght.ttf')]:
 data=base64.b64encode((fonts/file).read_bytes()).decode()
 rules.append(f'@font-face{{font-family:"{family}";font-weight:{weight};src:url(data:font/ttf;base64,{data})}}')
styles='<defs><style>'+''.join(rules)+'</style></defs>'
svg=svg.replace('xmlns="http://www.w3.org/2000/svg">','xmlns="http://www.w3.org/2000/svg">'+styles,1)
(OUT/'A_charcoal_jade.svg').write_text(svg,encoding='utf-8')
print('A recolor: scene geometry/text/font attributes preserved; 5 targeted accents; 9 global color mappings.')
