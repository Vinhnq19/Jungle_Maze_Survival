"""Author pixel UI and sprites directly on integer grids; never raster-filter K."""
from pathlib import Path
import base64, random
ROOT=Path(__file__).resolve().parents[3]; OUT=Path(__file__).resolve().parent
F=ROOT/'Assets/_JungleMaze/Art/Fonts'; s=[]; counts={}
def put(v):s.append(v)
def g(name,tr='',extra=''):
 counts[name]=counts.get(name,0)+1;put(f'<g id="{name}-{counts[name]}" transform="{tr}" {extra}>')
def end():put('</g>')
def r(x,y,w,h,c):put(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{c}"/>')
def p(d,c):put(f'<path d="{d}" fill="{c}"/>')
def t(x,y,txt,sz=14,c='#e5d5a5',wt=500,pixel=False,anchor=None):
 put(f'<text x="{x}" y="{y}" font-family="'+('Pixelify'if pixel else'BVP')+f'" font-size="{sz}" font-weight="{wt}" fill="{c}"'+(f' text-anchor="{anchor}"'if anchor else'')+f'>{txt}</text>')
P={'0':'#20271e','1':'#353c2a','2':'#4a5136','3':'#697044','4':'#89935b','5':'#b9bd77','6':'#e0cf8f','7':'#a78049','8':'#785437','9':'#d2a56a','a':'#e7d9ae','b':'#b3c4b1','c':'#718b82','d':'#455e58','e':'#9fb65c','f':'#5b8848','g':'#355d35','h':'#cd7f3e','i':'#91512d','j':'#e3b75e','k':'#87b5b0','l':'#4e8c8a','m':'#dae4be','n':'#b85337','o':'#6b7752'}
def canvas(n=16):return [['.']*n for _ in range(n)]
def box(a,x,y,w,h,c):
 for yy in range(max(0,y),min(len(a),y+h)):
  for xx in range(max(0,x),min(len(a[0]),x+w)):a[yy][xx]=c
def line(a,x1,y1,x2,y2,c,w=1):
 steps=max(abs(x2-x1),abs(y2-y1),1)
 for i in range(steps+1):box(a,round(x1+(x2-x1)*i/steps),round(y1+(y2-y1)*i/steps),w,w,c)
def sprite(a,x,y,k,name):
 g(name,f'translate({x} {y}) scale({k})','shape-rendering="crispEdges"')
 for yy,row in enumerate(a):
  xx=0
  while xx<len(row):
   col=row[xx];stop=xx+1
   while stop<len(row) and row[stop]==col:stop+=1
   if col!='.':r(xx,yy,stop-xx,1,P[col])
   xx=stop
 end()
icons={}
a=canvas();line(a,2,13,5,10,'0',3);line(a,3,13,6,10,'8',2);line(a,5,9,7,11,'7',2);line(a,6,9,13,2,'d',3);line(a,7,8,13,2,'b',2);line(a,8,7,13,2,'m');box(a,13,1,1,2,'m');icons['knife']=a
a=canvas();line(a,7,7,7,14,'8',3);line(a,2,2,7,8,'8',3);line(a,12,2,7,8,'8',3);line(a,3,2,7,7,'9',2);line(a,12,2,8,7,'9',2);line(a,8,8,8,13,'9');line(a,3,3,8,6,'6');line(a,12,3,8,6,'6');box(a,7,10,3,1,'7');icons['sling']=a
a=canvas();box(a,1,5,14,3,'d');box(a,1,5,13,1,'m');box(a,2,6,11,1,'b');box(a,0,6,3,2,'8');box(a,3,8,8,2,'7');box(a,4,8,3,3,'8');box(a,3,10,3,2,'8');box(a,3,10,1,1,'9');box(a,10,7,3,1,'c');icons['gun']=a
a=canvas();line(a,3,14,11,1,'8',3);line(a,4,13,11,1,'9');line(a,8,7,2,5,'8',2);line(a,9,4,14,6,'8',2);box(a,12,3,3,2,'f');box(a,13,2,2,1,'e');icons['branch']=a
a=canvas();box(a,5,2,8,10,'g');box(a,3,4,10,7,'g');box(a,4,3,8,8,'f');box(a,6,2,7,6,'e');box(a,3,6,8,5,'f');box(a,4,11,5,2,'g');line(a,3,14,11,4,'6');line(a,7,9,4,6,'5');line(a,8,7,11,7,'5');icons['leaf']=a
a=canvas();box(a,4,2,8,12,'i');box(a,5,1,6,1,'6');box(a,5,2,6,1,'9');box(a,4,3,7,10,'h');box(a,5,4,1,8,'9');box(a,5,13,6,1,'8');box(a,6,4,4,8,'i');line(a,9,4,7,8,'6');line(a,7,8,9,8,'6');line(a,9,8,7,12,'6');icons['can']=a
a=canvas();box(a,8,1,4,2,'6');box(a,5,3,7,8,'7');box(a,4,5,7,6,'j');box(a,6,3,5,6,'6');box(a,3,8,6,5,'j');box(a,4,12,3,1,'7');line(a,2,14,10,3,'a');line(a,5,10,3,8,'7');line(a,8,7,6,5,'7');icons['feather']=a
a=canvas();box(a,6,0,4,2,'7');box(a,4,2,8,12,'7');box(a,2,4,12,8,'7');box(a,3,4,10,8,'6');box(a,4,3,8,10,'6');box(a,4,5,8,6,'a');box(a,5,4,6,8,'a');line(a,8,4,6,8,'n');line(a,6,8,9,7,'n');line(a,8,11,9,7,'d');box(a,7,7,2,2,'8');box(a,3,6,1,4,'9');icons['compass']=a
# Original 32px portrait with a stepped gold badge, no smooth circle.
face=canvas(32);box(face,8,1,16,2,'7');box(face,4,3,24,3,'6');box(face,2,6,28,20,'7');box(face,4,5,24,23,'6');box(face,6,7,20,22,'1');box(face,9,4,14,3,'2');box(face,7,8,18,3,'3');box(face,5,11,22,3,'4');box(face,8,14,16,10,'9');box(face,9,14,14,7,'a');box(face,10,17,3,1,'8');box(face,19,17,3,1,'8');box(face,15,19,2,2,'9');box(face,13,23,6,1,'8');box(face,9,25,14,4,'o');box(face,6,28,20,3,'3');box(face,13,25,6,5,'9');box(face,11,27,2,3,'5');box(face,19,27,2,3,'5')
hero=canvas();box(hero,5,1,6,2,'3');box(hero,3,3,10,2,'4');box(hero,5,5,6,4,'9');box(hero,4,9,8,4,'7');box(hero,2,9,2,3,'9');box(hero,12,8,2,3,'9');box(hero,5,13,2,3,'1');box(hero,9,13,2,3,'1');line(hero,11,9,15,5,'d',2)
# Deliberately jagged 32px tree canopy, 3-tone blocks and a solid shadow.
tree=canvas(32)
for x,y,w,h,c in [(9,4,13,3,'1'),(5,7,21,4,'1'),(2,11,27,12,'1'),(5,23,22,4,'1'),(9,27,14,3,'0'),(10,2,11,4,'2'),(6,5,20,5,'2'),(3,10,26,10,'2'),(6,20,21,6,'2'),(10,25,13,3,'1'),(10,4,10,5,'3'),(6,9,12,8,'3'),(17,10,8,8,'3'),(8,17,15,6,'3'),(9,7,7,2,'4'),(5,12,4,2,'4'),(19,11,4,2,'4'),(12,20,6,2,'4')]:box(tree,x,y,w,h,c)
def stepped(x,y,w,h,c,step=8):
 p(f'M{x+step} {y} H{x+w-step} V{y+step} H{x+w} V{y+h-step} H{x+w-step} V{y+h} H{x+step} V{y+h-step} H{x} V{y+step} H{x+step}Z',c)
def panel(x,y,w,h,orn=False):
 g('Pixel-panel','','shape-rendering="crispEdges"');stepped(x+4,y+6,w,h,'#1b221a');stepped(x,y,w,h,'#282e23');stepped(x+4,y+4,w-8,h-8,'#8c7749',4);stepped(x+6,y+6,w-12,h-12,'#424735',4);stepped(x+10,y+10,w-20,h-20,'#4c503c',4);r(x+12,y+8,w-24,2,'#ac9660');r(x+12,y+h-10,w-24,2,'#343b2a')
 if orn:
  for xx,yy in [(x+8,y+8),(x+w-28,y+8),(x+8,y+h-28),(x+w-28,y+h-28)]:
   r(xx,yy,20,4,'#d0ad67');r(xx,yy,4,20,'#d0ad67');r(xx+4,yy+4,12,2,'#776138');r(xx+4,yy+4,2,12,'#776138')
 end()
def well(x,y,size,selected=False):
 g('Pixel-item-well','','shape-rendering="crispEdges"');stepped(x,y,size,size,'#292f24',12);stepped(x+4,y+4,size-8,size-8,'#333a29',8);r(x+16,y+4,size-32,2,'#1c241a')
 if selected:
  r(x+12,y-4,size-24,2,'#d6b56e');r(x+12,y+size+2,size-24,2,'#d6b56e');r(x-4,y+12,2,size-24,'#d6b56e');r(x+size+2,y+12,2,size-24,'#d6b56e')
 end()
def world():
 g('Native-world-320x180','scale(4)','shape-rendering="crispEdges"');r(0,0,320,180,'#576846')
 for yy,xx in [(0,194),(16,186),(32,178),(48,164),(64,153),(80,142),(96,137),(112,128),(128,120),(144,116),(160,112)]:
  r(xx,yy,30,16,'#7b7c55');r(xx+4,yy,2,8,'#8e8a5c')
 for yy,xx in [(0,271),(20,269),(40,278),(60,284),(80,280),(100,270),(120,267),(140,279),(160,296)]:
  r(xx,yy,320-xx,20,'#344f44');r(xx,yy,2,20,'#6b8060')
 random.seed(7)
 for _ in range(75):
  xx=random.randrange(320);yy=random.randrange(180)
  if 90<xx<234 and 43<yy<135:continue
  r(xx,yy,2,1,'#687a4e');r(xx+1,yy-1,1,1,'#738154')
 for xx,yy in [(0,0),(35,13),(70,-10),(12,65),(-11,104),(44,102),(224,-6),(291,71),(225,130),(286,158),(172,164)]:sprite(tree,xx,yy,1,'Tree32')
 for xx,yy in [(78,89),(220,109),(98,112),(247,87)]:
  stepped(xx,yy,10,7,'#6e7754',2);r(xx+2,yy,6,2,'#9a9c6e');r(xx+2,yy+5,6,2,'#485338')
 end()
def item(name,x,y,k):sprite(icons[name],x,y,k,'Sprite16-'+name)
def pixelring(x,y,radius,color,thickness=1):
 """Circle semantics authored as a closed band of 4px square cells."""
 g('Pixel-circle-zone','','shape-rendering="crispEdges"')
 for yy in range(-radius,radius):
  for xx in range(-radius,radius):
   distance=(xx+.5)**2+(yy+.5)**2
   if (radius-thickness)**2<=distance<radius**2:r(x+xx*4,y+yy*4,4,4,color)
 end()
put('<svg xmlns="http://www.w3.org/2000/svg" width="2672" height="900" viewBox="0 0 2672 900"><defs><style>')
for weight,file,fam in [(500,'BeVietnamPro-Medium.ttf','BVP'),(700,'BeVietnamPro-Bold.ttf','BVP'),(700,'PixelifySans-VariableFont_wght.ttf','Pixelify')]:
 data=base64.b64encode((F/file).read_bytes()).decode();put(f'@font-face{{font-family:{fam};src:url(data:font/ttf;base64,{data});font-weight:{weight}}}')
put('</style><clipPath id="screen"><rect width="1280" height="720"/></clipPath></defs>')
r(0,0,2672,900,'#252d22');t(40,48,'LASTAWAY / PIXEL SURVIVAL',28,'#e0cf8f',700,True);t(40,80,'L · Đúng đồ họa pixel / Giữ đá nâu và vàng cổ / Bản demo chưa duyệt production',14,'#adb48d');t(1880,48,'INVENTORY + HUD / 1280 x 720',18,'#c3c39b',700,True)
g('Inventory-1280x720','translate(40 122)','clip-path="url(#screen)"');r(0,0,1280,720,'#1d271d')
panel(64,60,1152,604,True)
# Bronze stepped plaque with folded pixel corners.
g('Bronze-title','','shape-rendering="crispEdges"');stepped(496,48,288,60,'#292e23');stepped(504,52,272,48,'#785437',8);r(516,56,248,4,'#b99052');r(516,92,248,4,'#4e4029');r(508,56,12,12,'#b99052');r(760,56,12,12,'#b99052');end();t(640,85,'HÀNH TRANG',20,'#f0deb0',700,False,'middle')
g('Pixel-close','','shape-rendering="crispEdges"');stepped(1152,44,52,52,'#594c30');stepped(1156,44,44,44,'#d5c58c',8);r(1164,48,28,4,'#e5d6a0');r(1168,60,4,4,'#675f3c');r(1184,60,4,4,'#675f3c');r(1172,64,4,4,'#675f3c');r(1180,64,4,4,'#675f3c');r(1176,68,4,4,'#675f3c');r(1172,72,4,4,'#675f3c');r(1180,72,4,4,'#675f3c');r(1168,76,4,4,'#675f3c');r(1184,76,4,4,'#675f3c');end()
panel(104,124,284,496,True);sprite(face,182,136,4,'Portrait32');t(246,294,'NGƯỜI SỐNG SÓT',14,'#e8dcb5',700,False,'middle');t(246,320,'Sẵn sàng sinh tồn',11,'#b1bb90',500,False,'middle');r(132,340,228,2,'#81764a')
for yy,label,col,val,width in [(375,'MÁU','#b9bd77','100 / 100',220),(432,'GIÁP','#a2b4a1','75 / 100',164),(489,'MŨ','#c8b582','50 / 100',108),(546,'ADRENALINE','#c9a05e','32 / 100',72)]:
 t(136,yy,label,11,'#c8c49d',700);t(296,yy,val,13,'#e8dcaf',700,True);r(136,yy+9,224,12,'#282f22');r(138,yy+11,width,8,col);r(138,yy+11,width,2,'#ddd3a0')
t(436,164,'VŨ KHÍ',14,'#ded3ad',700);t(1080,164,'4 KHE',14,'#bdb58c',700,True);r(436,180,704,2,'#8a7c4e');r(436,184,704,2,'#343d2b')
data=[('knife','Dao'),('sling','Ná cao su'),('gun','Shotgun'),('branch','Cành cây'),('leaf','Lá thần'),('can','Adrenaline'),('feather','Lông phượng'),('compass','La bàn')]
for i,(name,label)in enumerate(data):
 xx=456+(i%4)*176; yy=208 if i<4 else 404;well(xx,yy,96,i==2);item(name,xx+16,yy+16,4);r(xx+4,yy+76,20,20,'#3f4830');t(xx+8,yy+91,str(i+1),15,'#ddd2a4',700,True)
 if i>=4:r(xx+76,yy+76,20,20,'#2b3323');t(xx+81,yy+91,str([2,3,1,1][i-4]),15,'#ddd2a4',700,True)
 t(xx+48,yy+124,label,13,'#e2d7ad',500,False,'middle')
t(436,362,'DƯỢC PHẨM &amp; HỖ TRỢ',14,'#ded3ad',700);t(1080,362,'4 KHE',14,'#bdb58c',700,True);r(436,378,704,2,'#8a7c4e');r(436,382,704,2,'#343d2b')
stepped(432,566,708,52,'#353f2b',4);r(440,566,692,2,'#80784c');item('gun',448,576,2);t(500,587,'SHOTGUN',17,'#e3cea0',700,True);t(500,607,'Đang chọn · Phím 3 để chuyển vũ khí',11,'#b9c196');t(1040,598,'ĐANG CẦM',10,'#d0bb7c',700);t(92,695,'TAB  ·  Đóng hành trang',12,'#bfc99d');end()
# HUD same pixel material and pixel world.
g('HUD-1280x720','translate(1352 122)','clip-path="url(#screen)"');world();sprite(hero,608,328,4,'Hero16');r(716,308,4,8,'#e9dfa7');r(716,336,4,8,'#e9dfa7');r(700,324,8,4,'#e9dfa7');r(728,324,8,4,'#e9dfa7')
panel(24,24,204,36);t(40,48,'Mây',12,'#e2d5ae',700);item('knife',80,28,2);t(119,48,'Bụi rậm',12,'#acb78e')
panel(488,24,304,40);r(504,36,4,12,'#d5ae66');r(504,52,4,4,'#d5ae66');t(524,50,'Bom phía Đông',12,'#ead8ae',700);t(684,51,'1.5s · 7m',14,'#dcb875',700,True)
panel(1056,24,200,200,True);r(1068,36,176,176,'#61734d');p('M1068 84 H1100 V68 H1132 V100 H1164 V84 H1196 V52 H1228 V84 H1244 V116 H1212 V132 H1180 V164 H1148 V132 H1116 V116 H1084 V148 H1068Z','#3d5638');p('M1148 36 H1160 V84 H1172 V132 H1160 V180 H1148 V212 H1136 V164 H1148Z','#81916a');pixelring(1156,124,17,'#a9b270');pixelring(1156,124,11,'#dfca8d');p('M1152 116 H1156 V124 H1160 V132 H1148 V124 H1152Z','#f2e5b4');t(1150,53,'N',14,'#f0dfa7',700,True)
panel(1056,232,200,76);t(1072,255,'BO THU HẸP',10,'#c0be93',700);t(1072,289,'01:24',27,'#e9d8a5',700,True);r(1152,248,2,44,'#77704a');t(1164,257,'CÒN SỐNG',9,'#bdbb90',700);t(1180,289,'08',27,'#e1cd94',700,True)
for xx,name,sec in [(24,'leaf','2.4s'),(128,'compass','5.0s')]:panel(xx,476,96,40);item(name,xx+8,480,2);t(xx+44,503,sec,16,'#dfdda9',700,True)
panel(24,532,296,164,True);t(40,555,'NGƯỜI SỐNG SÓT',10,'#d1c69c',700);r(40,564,264,2,'#80744a')
for yy,label,ww,col,val in [(579,'MÁU',264,'#b9bd77','100 / 100'),(608,'GIÁP',196,'#a2b4a1','75 / 100'),(637,'MŨ',132,'#c8b582','50 / 100'),(666,'ADRENALINE',84,'#c9a05e','32 / 100')]:
 t(40,yy,label,9,'#c2be94',700);t(244,yy,val,12,'#e5d9a8',700,True);r(40,yy+5,264,10,'#293123');r(40,yy+5,ww,8,col);r(40,yy+5,ww,2,'#ded3a0')
panel(358,566,564,38);item('gun',370,572,2);t(412,591,'SHOTGUN',18,'#e3d5aa',700,True);t(784,591,'6 / 18',20,'#e5d4a0',700,True);r(744,576,2,16,'#7e7247')
for i,(name,_)in enumerate(data):
 xx=358+i*71;stepped(xx,612,66,84,'#8f7a46'if i==2 else'#646c43',4);stepped(xx+2,614,62,80,'#414b32',4);well(xx+7,618,52,False);item(name,xx+9,620,3);r(xx+4,676,16,16,'#293224');t(xx+8,689,str(i+1),14,'#d7cfa0',700,True)
 if i>=4:t(xx+48,689,str([2,3,1,1][i-4]),14,'#e0d4a3',700,True)
 if i==2:r(xx+16,698,32,2,'#cbb06b')
panel(1116,608,140,36);item('leaf',1124,612,2);t(1160,631,'Lá thần',10,'#e2d5a9');t(1224,632,'+1',15,'#c8ce8b',700,True);panel(1160,664,96,28);t(1172,684,'TAB',14,'#d8bf7f',700,True);t(1208,683,'Balo',10,'#bfc69a');end()
t(40,879,'ART ĐƯỢC VẼ TRÊN LƯỚI PIXEL · Sprite 16/32 px, scale nguyên · Chưa thay Unity / chưa ghi được Figma',13,'#aab18b');t(1830,879,'PIXELIFY SANS + BE VIETNAM PRO',18,'#bebc91',700,True);put('</svg>')
(OUT/'L_pixel_survival.svg').write_text('\n'.join(s),encoding='utf-8');print('Generated L_pixel_survival.svg')
