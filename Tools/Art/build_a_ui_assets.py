"""Build A charcoal/jade UI masks, effect glyphs and menu art; never edit .meta.

Pillow is used for deterministic authored pixel geometry and RGBA export. UI masks
are grayscale/tintable; menu/effect art is colored and should use white Image tint.
Existing damage arc and danger arrow are reused byte-for-byte, not regenerated.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, random, math, hashlib

ROOT=Path(__file__).resolve().parents[2]
HUD=ROOT/'Assets/_JungleMaze/Art/Hud'
EFFECTS=ROOT/'Assets/_JungleMaze/Art/UI/Icons/Effects'
MENU=ROOT/'Assets/_JungleMaze/Art/UI/Menu'
DOC=ROOT/'Docs/Art/Production_A/UI'
for folder in [HUD,EFFECTS,MENU,DOC]:folder.mkdir(parents=True,exist_ok=True)
N=Image.Resampling.NEAREST
P={'background':'#0b1114','panel':'#202a2d','edge':'#0c1417','bevel':'#526b69','jade':'#63baa5','gold':'#c6ae78','text':'#edf2ee','muted':'#a6bbb7','purple':'#b37bff'}
outputs=[]

def save(im,path,pack,source=None):
 im=im.convert('RGBA');im.save(path,optimize=True)
 outputs.append({'path':str(path.relative_to(ROOT)).replace('\\','/'),'size':list(im.size),'pack':pack,'mode':'RGBA','source':source,'binding':'prepared; Unity binding/import QA pending'})

def mask(size,mode='fill',stroke=3):
 w,h=size;im=Image.new('RGBA',size);d=ImageDraw.Draw(im)
 if mode=='fill':
  # A dark base tint cannot yield a brighter bevel by multiplication. Keep the
  # base neutral white and provide independent white overlay masks below.
  d.rectangle((1,1,w-2,h-2),fill='white')
 elif mode=='outline':d.rectangle((1,1,w-2,h-2),outline='white',width=stroke)
 elif mode=='dashed':
  for x in range(3,w-3,24):d.rectangle((x,2,min(x+12,w-4),2+stroke-1),fill='white');d.rectangle((x,h-3-stroke,min(x+12,w-4),h-4),fill='white')
  for y in range(3,h-3,24):d.rectangle((2,y,2+stroke-1,min(y+12,h-4)),fill='white');d.rectangle((w-3-stroke,y,w-4,min(y+12,h-4)),fill='white')
 return im

mask_specs=[
 ('HudRound10.png',(96,96),'fill',3,'P01'),('HudRound6.png',(128,128),'fill',3,'P01'),
 ('HudRound6Stroke2.png',(128,128),'outline',4,'P01'),('HudRound6Dashed.png',(128,128),'dashed',4,'P01'),
 ('HudRound6Stroke1.png',(96,96),'outline',3,'P01'),('BarFill.png',(64,16),'fill',2,'P01'),
 ('BarTrack.png',(64,16),'fill',2,'P01'),('KeyCap.png',(48,48),'fill',2,'P01'),
 ('MinimapFrame.png',(400,400),'outline',8,'P01'),('EffectFrame.png',(88,88),'outline',3,'P02'),
 ('ToastBg.png',(96,64),'fill',3,'P02'),('DangerBanner.png',(192,80),'fill',3,'P02')]
for name,size,mode,stroke,pack in mask_specs:save(mask(size,mode,stroke),HUD/name,pack)
for name,size,kind in [('ABevel96.png',96,'bevel'),('ABevelSlot128.png',128,'bevel'),('AFrameEdge96.png',96,'edge'),('AFrameEdgeSlot128.png',128,'edge')]:
 im=Image.new('RGBA',(size,size));d=ImageDraw.Draw(im)
 if kind=='bevel':d.rectangle((3,3,size-4,5),fill='white');d.rectangle((3,3,5,size-4),fill='white')
 else:d.rectangle((1,1,size-2,size-2),outline='white',width=2)
 save(im,HUD/name,'P01-overlay')
marker=Image.new('RGBA',(16,16));d=ImageDraw.Draw(marker);d.ellipse((3,4,12,13),fill='white');d.polygon([(7,0),(12,7),(4,7)],fill='white');save(marker.resize((64,64),N),HUD/'HudCircle16.png','P01')
cross=Image.new('RGBA',(32,32));d=ImageDraw.Draw(cross)
for box in [(15,3,16,10),(15,21,16,28),(3,15,10,16),(21,15,28,16)]:d.rectangle(box,fill='white')
save(cross.resize((128,128),N),HUD/'Crosshair.png','P01')
hit=Image.new('RGBA',(32,32));d=ImageDraw.Draw(hit)
for points in [[(5,5),(11,11)],[(20,20),(26,26)],[(5,26),(11,20)],[(20,11),(26,5)]]:d.line(points,fill='white',width=2)
save(hit.resize((128,128),N),HUD/'HitMarker.png','P02')

# Seven native 32px effect glyphs with distinct silhouettes at small sizes.
glyphs={}
def new():
 im=Image.new('RGBA',(32,32));return im,ImageDraw.Draw(im)
im,d=new();d.polygon([(16,2),(25,14),(26,20),(22,27),(10,27),(6,21),(7,15)],fill='#526f43');d.polygon([(16,4),(23,15),(23,21),(19,25),(11,24),(9,20),(10,14)],fill='#a6c56d');d.rectangle((12,15,14,18),fill='#344539');d.rectangle((18,15,20,18),fill='#344539');d.rectangle((14,21,18,22),fill='#76547b');d.rectangle((12,10,13,13),fill='#e1e4ab');glyphs['Poison']=im
im,d=new();d.rectangle((4,24,27,28),fill='#725640');d.polygon([(9,4),(20,4),(20,17),(27,20),(27,24),(7,24),(6,20),(9,17)],fill='#ad8c61');d.rectangle((10,6,17,15),fill='#d2b584');d.rectangle((5,23,28,25),fill='#4e6351');d.rectangle((3,29,8,30),fill='#65794e');glyphs['Slow']=im
im,d=new();d.rectangle((13,4,20,6),fill='#9fbbb7');d.rectangle((10,7,22,12),fill='#81a6a2');d.polygon([(10,14),(22,14),(26,25),(19,25),(19,29),(14,29),(14,24),(7,24)],fill='#607f7e');d.rectangle((14,8,15,9),fill='#d2ede6');d.rectangle((19,8,20,9),fill='#d2ede6');d.rectangle((7,18,11,20),fill='#c0d4ca');d.rectangle((3,22,7,24),fill='#657c7a');d.rectangle((23,26,26,28),fill='#a7c3b8');glyphs['Invisible']=im
im,d=new()
for x,y,scale in [(9,10,1),(23,7,.7),(21,23,.9)]:
 k=scale;d.polygon([(x,y-7*k),(x+2*k,y-2*k),(x+7*k,y),(x+2*k,y+2*k),(x,y+7*k),(x-2*k,y+2*k),(x-7*k,y),(x-2*k,y-2*k)],fill='#e0c27b');d.rectangle((x-1,y-1,x+1,y+1),fill='#fff0c0')
d.rectangle((6,24,11,25),fill='#8d896e');d.rectangle((5,20,6,23),fill='#8d896e');glyphs['Stun']=im
im,d=new();d.rectangle((10,3,14,18),fill='#aa9f77');d.rectangle((19,3,23,18),fill='#c3b384');d.rectangle((7,17,14,22),fill='#7b6b4d');d.rectangle((18,17,26,22),fill='#7b6b4d');d.line([(3,28),(10,22),(7,16),(18,12),(24,7),(28,10)],fill='#69a46d',width=3);d.line([(7,30),(15,25),(22,25),(27,20)],fill='#a2c780',width=3);d.rectangle((3,18,6,20),fill='#b8cc8d');d.rectangle((25,13,29,15),fill='#84b575');glyphs['Root']=im
im,d=new();d.polygon([(2,8),(19,8),(19,3),(29,11),(19,19),(19,14),(2,14)],fill='#72b7a7');d.polygon([(30,20),(13,20),(13,15),(3,23),(13,31),(13,26),(30,26)],fill='#ae92c5');d.rectangle((5,9,17,10),fill='#c0e3d2');d.rectangle((15,21,27,22),fill='#d8c6df');glyphs['Reverse']=im
im,d=new();d.polygon([(16,2),(28,7),(26,21),(21,27),(16,31),(10,27),(5,21),(4,7)],fill='#a18a58');d.polygon([(16,5),(24,9),(23,19),(19,24),(16,27),(12,24),(8,19),(7,9)],fill='#466d68');d.polygon([(11,14),(15,18),(22,10),(24,13),(15,23),(8,17)],fill='#dce3b0');d.rectangle((10,8,12,12),fill='#75aa93');glyphs['CcImmune']=im
for name,im in glyphs.items():
 im.save(DOC/f'{name}_native32.png');save(im.resize((256,256),N),EFFECTS/f'{name}.png','P06',f'Docs/Art/Production_A/UI/{name}_native32.png')

# Empty-face menu frames are colored art; Image tint should stay white.
def button(fill,edge,ghost=False):
 im=Image.new('RGBA',(192,96));d=ImageDraw.Draw(im)
 d.rectangle((1,1,190,94),fill=fill,outline=edge,width=3)
 d.rectangle((4,4,187,6),fill=P['bevel']);d.rectangle((4,4,6,91),fill=P['bevel']);d.rectangle((7,89,187,91),fill=P['edge']);d.rectangle((185,7,187,91),fill=P['edge'])
 if ghost:
  im.putalpha(im.getchannel('A').point(lambda a:round(a*.68)))
 return im
save(button('#3b8d7b',P['gold']),MENU/'ButtonPrimary.png','P07')
save(button(P['panel'],P['edge']),MENU/'ButtonSecondary.png','P07')
save(button(P['panel'],P['bevel'],True),MENU/'ButtonGhost.png','P07')

# Pixelify wordmark: actual font render in native256x64 then integer4x export.
logo=Image.new('RGBA',(256,64));d=ImageDraw.Draw(logo);font=ImageFont.truetype(str(ROOT/'Assets/_JungleMaze/Art/Fonts/PixelifySans-VariableFont_wght.ttf'),45)
try:font.set_variation_by_axes([700])
except (AttributeError,OSError):pass
bounds=d.textbbox((0,0),'LASTAWAY',font=font);x=(256-(bounds[2]-bounds[0]))//2-bounds[0];y=(64-(bounds[3]-bounds[1]))//2-bounds[1]
d.text((x,y),'LASTAWAY',font=font,fill=P['gold'])
# Binary glyph alpha preserves hard native pixel edges, including font rasterizer edges.
logo.putalpha(logo.getchannel('A').point(lambda a:255 if a>=128 else 0));logo.save(DOC/'Logo_native256x64.png');save(logo.resize((1024,256),N),MENU/'Logo.png','P07')

# New original top-down island illustration replaces only the missing key-art
# placeholder; it does not change Figma A layout or gameplay world assets.
def island():
 im=Image.new('RGBA',(208,208),P['background']);d=ImageDraw.Draw(im)
 random.seed(410)
 for _ in range(160):
  x=random.randrange(208);y=random.randrange(208);d.rectangle((x,y,x+random.randrange(2,7),y),fill='#16272d')
 shore=[(48,48),(75,29),(112,23),(141,39),(164,62),(179,91),(174,126),(153,153),(131,172),(91,180),(58,159),(37,131),(28,96),(35,69)]
 d.polygon([(x+2,y+4)for x,y in shore],fill='#1c3b3d');d.polygon(shore,fill='#92946c')
 land=[(52,53),(78,36),(110,30),(137,45),(158,66),(171,94),(166,123),(148,147),(127,164),(93,172),(64,153),(45,127),(36,96),(43,72)]
 d.polygon(land,fill='#34532f');d.polygon([(72,54),(99,43),(127,49),(148,76),(153,103),(136,128),(110,145),(79,138),(56,111),(57,80)],fill='#42623b')
 d.line([(106,40),(112,63),(98,85),(106,105),(93,127),(103,160)],fill='#274d4c',width=6);d.line([(108,41),(114,63),(100,85),(108,105),(95,127),(105,160)],fill='#487364',width=2)
 def tree(x,y,k=1):
  d.rectangle((x-4,y+5,x+5,y+10),fill='#203d29');d.polygon([(x-7,y-3),(x-4,y-7),(x+3,y-8),(x+7,y-4),(x+8,y+3),(x+4,y+7),(x-4,y+6),(x-8,y+2)],fill='#25462c');d.rectangle((x-4,y-5,x+2,y-3),fill='#5d7d43');d.rectangle((x-6,y-2,x-1,y+1),fill='#486c38')
 for x,y in [(58,75),(74,57),(93,51),(127,60),(149,81),(151,111),(140,138),(120,151),(79,148),(61,128),(54,104),(80,88),(130,98)]:tree(x,y)
 d.rectangle((116,75,133,85),fill='#273b33');d.rectangle((118,70,131,82),fill='#718171');d.rectangle((116,68,133,72),fill='#a6ad85');d.rectangle((122,74,127,82),fill='#2a463a')
 d.rectangle((88,100,113,118),fill='#8e9164');d.rectangle((92,96,109,121),fill='#8e9164');d.rectangle((99,104,104,109),fill='#dbc89c');d.rectangle((97,102,106,104),fill='#6a8063');d.rectangle((98,110,105,116),fill='#98764d');d.rectangle((98,116,100,120),fill='#243b2d');d.rectangle((104,116,106,120),fill='#243b2d')
 # Stepped circular purple safe-zone ring, no blur or glow texture.
 d.ellipse((23,24,184,185),outline=P['purple'],width=2)
 return im
key=island();key.save(DOC/'KeyArt_native208.png');save(key.resize((1040,1040),N),MENU/'KeyArt.png','P07')

# Dark jungle backdrop authored natively320x180 and enlarged integer6x.
bg=Image.new('RGBA',(320,180),P['background']);d=ImageDraw.Draw(bg)
d.rectangle((0,0,319,79),fill='#0e1c20');d.polygon([(0,75),(26,68),(47,77),(73,57),(99,65),(126,45),(147,62),(170,51),(195,71),(219,62),(252,76),(277,56),(300,68),(320,65),(320,180),(0,180)],fill='#152c29');d.polygon([(0,113),(28,93),(66,106),(99,82),(132,100),(167,78),(198,104),(237,83),(266,106),(302,91),(320,105),(320,180),(0,180)],fill='#1b352d');d.rectangle((0,151,319,179),fill='#182c29')
for x,y,w,h in [(17,10,8,170),(45,37,6,143),(286,4,9,176),(307,30,8,150)]:
 d.rectangle((x,y,x+w,y+h),fill='#091519');d.polygon([(x-17,y+12),(x+3,y),(x+29,y+8),(x+16,y+17),(x+42,y+22),(x+13,y+28),(x-7,y+18),(x-36,y+22)],fill='#10231f')
random.seed(44)
for _ in range(36):
 x=random.randrange(320);y=random.randrange(130,180);d.rectangle((x,y,x+3,y+2),fill='#233d31')
bg.save(DOC/'Background_native320x180.png');save(bg.resize((1920,1080),N),MENU/'Background.png','P07')

# Contact sheets show native exports on A charcoal and checker transparency.
fontsmall=ImageFont.truetype(str(ROOT/'Assets/_JungleMaze/Art/Fonts/BeVietnamPro-Medium.ttf'),14)
sheet=Image.new('RGBA',(840,410),P['background']);sd=ImageDraw.Draw(sheet)
sd.text((16,12),'P06 / EFFECT ICONS — 18px · 28px · 40px',font=fontsmall,fill=P['text'])
for i,(name,im)in enumerate(glyphs.items()):
 x=20+i*118;sd.text((x,50),name,font=fontsmall,fill=P['muted'])
 for yy,size in [(90,18),(148,28),(218,40)]:
  sd.rectangle((x-4,yy-4,x+size+4,yy+size+4),fill=P['panel'],outline=P['bevel']);sheet.alpha_composite(im.resize((size,size),N),(x,yy))
 sd.text((x,290),'32px native',font=fontsmall,fill=P['muted']);sheet.alpha_composite(im.resize((64,64),N),(x,318))
sheet.save(DOC/'Effects_contact.png')
ui=Image.new('RGBA',(1050,600),P['background']);ud=ImageDraw.Draw(ui)
ud.text((16,12),'P01 / P02 — GRAYSCALE TINTABLE MASKS (preview tinted A)',font=fontsmall,fill=P['text'])
for i,(name,size,mode,stroke,pack)in enumerate(mask_specs):
 x=16+(i%4)*258;y=48+(i//4)*142;im=Image.open(HUD/name).convert('RGBA');im.thumbnail((210,90),N)
 # Color multiplication mirrors Unity UI Image tint; alpha remains unchanged.
 tint=Image.new('RGBA',im.size,P['panel']);from PIL import ImageChops
 colored=ImageChops.multiply(im,tint);colored.putalpha(im.getchannel('A'));ui.alpha_composite(colored,(x,y));ud.text((x,y+96),name,font=fontsmall,fill=P['muted'])
ui.save(DOC/'Masks_contact.png')
menu_sheet=Image.new('RGBA',(1200,780),P['background']);menu_sheet.alpha_composite(Image.open(MENU/'Background.png').resize((600,338),N),(0,0));menu_sheet.alpha_composite(Image.open(MENU/'KeyArt.png').resize((338,338),N),(620,0));menu_sheet.alpha_composite(logo.resize((768,192),N),(16,360))
for i,name in enumerate(['ButtonPrimary.png','ButtonSecondary.png','ButtonGhost.png']):menu_sheet.alpha_composite(Image.open(MENU/name),(16+i*224,596))
ImageDraw.Draw(menu_sheet).text((16,730),'P07 / Background · KeyArt · Logo · Primary / Secondary / Ghost',font=fontsmall,fill=P['muted']);menu_sheet.save(DOC/'Menu_contact.png')

border_by_name={
 'HudRound10.png':[24]*4,'HudRound6.png':[20]*4,'HudRound6Stroke2.png':[20]*4,'HudRound6Dashed.png':[20]*4,
 'HudRound6Stroke1.png':[16]*4,'BarFill.png':[6]*4,'BarTrack.png':[6]*4,'KeyCap.png':[12]*4,'MinimapFrame.png':[32]*4,
 'EffectFrame.png':[18]*4,'ToastBg.png':[20]*4,'DangerBanner.png':[28]*4,'ABevel96.png':[24]*4,'AFrameEdge96.png':[24]*4,
 'ABevelSlot128.png':[20]*4,'AFrameEdgeSlot128.png':[20]*4,'ButtonPrimary.png':[32]*4,'ButtonSecondary.png':[32]*4,'ButtonGhost.png':[32]*4}
for item in outputs:
 name=Path(item['path']).name;item['ppu']=100;item['filter']='Point';item['mipmaps']=False;item['compression']='None';item['border_LTRB']=border_by_name.get(name,[0]*4)
 if item['pack']=='P01-overlay':
  item['role']='independent white overlay, transparent center; tint after base'
  item['tint']='#526B69'if 'Bevel'in name else'#0C1417'
 elif item['pack']in['P01','P02']:
  item['role']='neutral white RGBA mask; Image tint owns A or semantic color';item['tint']='integration palette'
 else:item['role']='colored pixel art; Image tint must be white';item['tint']='#FFFFFF'
validation=[]
for item in outputs:
 path=ROOT/item['path'];im=Image.open(path);has_alpha=im.mode=='RGBA';im=im.convert('RGBA');alpha=im.getchannel('A');colors=list(im.get_flattened_data());is_mask=item['pack']in['P01','P02','P01-overlay']
 valid={'path':item['path'],'size':list(im.size),'has_alpha':has_alpha,'alpha_extrema':list(alpha.getextrema()),'grayscale_tintable':all(r==g==b for r,g,b,a in colors)if is_mask else None,'visible_pixels_white':all(r==g==b==255 for r,g,b,a in colors if a)if is_mask else None,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
 if not has_alpha or list(im.size)!=item['size']or(is_mask and not valid['visible_pixels_white']):raise ValueError('Invalid export '+item['path'])
 validation.append(valid)
for path in [HUD/'HudArcFilled.png',HUD/'HudArrowOutline.png']:
 im=Image.open(path);outputs.append({'path':str(path.relative_to(ROOT)).replace('\\','/'),'size':list(im.size),'pack':'P02','binding':'reuse existing; unchanged PNG/meta','sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(DOC/'asset_mapping.json').write_text(json.dumps(outputs,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DOC/'png_validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'produced_pngs':len(validation),'reuse_pngs':2,'all_masks_grayscale':all(v['grayscale_tintable']is not False for v in validation),'effects':len(glyphs),'menu_assets':6,'output':str(DOC)},ensure_ascii=False))
