from pathlib import Path
import base64, random, math

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
F=ROOT/'Assets/_JungleMaze/Art/Fonts'
s=[]
group_counts={}
def put(v): s.append(v)
def g(name,tr='',extra=''):
 group_counts[name]=group_counts.get(name,0)+1
 put(f'<g id="{name}-{group_counts[name]}" transform="{tr}" {extra}>')
def end():put('</g>')
def r(x,y,w,h,c,rx=0,st=None,sw=1):put(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{c}"'+(f' stroke="{st}" stroke-width="{sw}"' if st else '')+'/>')
def p(d,c,st=None,sw=1):put(f'<path d="{d}" fill="{c}"'+(f' stroke="{st}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"' if st else '')+'/>')
def t(x,y,txt,sz=16,c='#f3eedc',wt=500,ls=0):put(f'<text x="{x}" y="{y}" font-size="{sz}" font-weight="{wt}" letter-spacing="{ls}" fill="{c}">{txt}</text>')
def e(x,y,rx,ry,c,st=None):put(f'<ellipse cx="{x}" cy="{y}" rx="{rx}" ry="{ry}" fill="{c}"'+(f' stroke="{st}" stroke-width="1"' if st else '')+'/>')
def leaf(x,y,k,rot,c='#387e66'):
 g('Leaf',f'translate({x} {y}) rotate({rot}) scale({k})');p('M0 0 C-24 -10 -34 -40 -3 -68 C24 -48 27 -19 0 0Z',c);p('M-1 -5 L-3 -58 M-2 -25 L10 -36 M-2 -38 L-13 -45','none','#87b185',1.2);end()
def emblem(x,y,k=1):
 g('Expedition-emblem',f'translate({x} {y}) scale({k})');p('M0 -28 L25 -3 L0 28 L-25 -3Z','none','#b9aa74',1.4);p('M0 -20 L7 4 L0 0 L-7 4Z','#e9dcaa');p('M0 20 L-6 -3 L0 1 L6 -3Z','#789b80');p('M-36 0 L-16 0 M16 0 L36 0','none','#bbad78',1);end()
def panel(x,y,w,h):
 g('Panel','', 'filter="url(#shadow)"');r(x,y,w,h,'url(#panel)',10,'#678a76');r(x+3,y+3,w-6,h-6,'none',8,'#284f48',.7);p(f'M{x+15} {y+1} H{x+w-15}','none','#b6b28a',.75);end()
def icon(kind,x,y,k=1):
 g('Item-'+kind,f'translate({x} {y}) scale({k})','filter="url(#iconShadow)"')
 if kind=='knife':
  p('M-17 22 L-8 11 L0 17 L-10 29Z','url(#wood)','#182f2e',1.5);p('M-11 12 L-4 20','none','#e0b779',3);p('M-7 11 L17 -21 L25 -27 L23 -12 L1 19Z','url(#steel)','#213f3b',1.5);p('M0 11 L20 -18','none','#f0f3de',1)
 elif kind=='sling':
  p('M0 24 L0 0 M0 0 L-16 -19 M0 0 L17 -19','none','#473b2c',10);p('M0 23 L0 0 M0 0 L-16 -19 M0 0 L17 -19','none','#ba8c51',7);p('M-16 -19 Q0 1 17 -19','none','#e2cb8d',2.5);p('M-3 12 L3 12 M-3 15 L3 15','none','#6d4d32',2)
 elif kind=='gun':
  p('M-28 -6 L19 -10 L27 -6 L26 0 L-6 3 L-10 16 L-20 13 L-15 0 L-28 1Z','url(#steel)','#122d2d',1.5);p('M-29 -4 L9 -6','none','#e0e9d0',2);p('M-19 4 L-23 13 L-16 17 L-9 5Z','url(#wood)','#21372d');r(-6,-2,19,7,'#8f6a40',2)
 elif kind=='branch':
  p('M-17 24 L13 -23 M1 -2 L-14 -10 M7 -12 L21 -8','none','#3c3529',9);p('M-17 24 L13 -23 M1 -2 L-14 -10 M7 -12 L21 -8','none','#b48b58',6);leaf(14,-14,.25,60,'#88b66b');p('M-13 14 L-9 17 M-3 -2 L1 1','none','#edcc89',1.4)
 elif kind=='leaf':
  p('M-8 23 C-31 -2 -14 -28 21 -24 C27 9 9 26 -8 23Z','url(#leaf)','#254c39',1.5);p('M-13 29 L15 -18 M-2 10 L-14 -2 M5 0 L17 0 M6 -1 L-3 -13','none','#e0e6a0',1.5);e(6,-13,2,3,'#f4f5b4')
 elif kind=='vial':
  p('M-7 -20 H7 V-9 Q16 -6 16 10 V22 Q0 30 -16 22 V10 Q-16 -6 -7 -9Z','url(#glass)','#1e5047',1.5);p('M-12 7 Q0 13 12 7 V21 Q0 27 -12 21Z','#71c4b1');r(-8,-25,16,8,'url(#wood)',2,'#67472d');p('M-8 -4 V15','none','#d8f0d0',2.5);p('M-3 10 L3 10 M0 7 V13','none','#e7edbd',2)
 elif kind=='feather':
  p('M-12 23 Q-31 -9 17 -30 Q34 -10 13 12 Q4 23 -12 23Z','url(#feather)','#826c3d',1);p('M-18 30 L17 -24 M-2 11 L-15 4 M6 2 L-9 -7 M12 -8 L0 -14','none','#f7e7ad',1.5)
 elif kind=='compass':
  e(0,0,23,23,'url(#gold)','#213d35');e(0,0,18,18,'#e3dcc1','#685d40');p('M0 -13 L6 7 L0 3 L-6 7Z','#b75442');p('M0 13 L-6 -7 L0 -3 L6 -7Z','#3f786c');e(0,0,2,2,'#e3c988');p('M0 -18 V-15 M0 18 V15 M18 0 H15 M-18 0 H-15','none','#776c48',1.5);r(-4,-29,8,7,'#af975f',2)
 end()

# Dark 2D UI is composed independently from the pixel world below it.
def card(x,y,w,h,active=False,rx=12):
 g('Dark-card','','filter="url(#panelShadow)"')
 r(x,y,w,h,'url(#card)',rx,'#b49561' if active else '#344b58',1.5 if active else 1)
 p(f'M{x+rx+2} {y+1} H{x+w-rx-2}','none','#d8be85' if active else '#53727b',.8)
 end()

def portrait(x,y,k=1):
 g('Smooth-survivor-portrait',f'translate({x} {y}) scale({k})')
 e(0,0,70,70,'url(#portraitGlow)');e(0,0,58,58,'#152b37','#48616a')
 p('M-44 50 Q-42 21 -20 19 H20 Q42 21 44 50Z','#4f6d71','#1b3541',2)
 p('M-21 23 L-7 46 H8 L21 23 L8 15 H-8Z','#91a19a');p('M-8 15 V30 Q0 40 9 30 V15Z','#b88a62')
 p('M-25 -28 Q0 -48 25 -28 L23 3 Q18 27 0 29 Q-21 25 -24 3Z','url(#skin)','#795f47',1.5)
 p('M-27 -24 Q-14 -55 15 -48 Q33 -41 29 -17 L20 -27 L-14 -29Z','#354c4e')
 p('M-24 -23 Q0 -34 25 -23 L37 -14 L31 -9 H-34 L-39 -14Z','url(#hat)','#233e43',2)
 p('M-28 -26 Q0 -38 27 -25 L23 -35 Q0 -58 -22 -34Z','url(#hat)','#365252',1)
 p('M-14 -3 H-7 M9 -3 H16','none','#4c4937',3);p('M-3 3 L-5 10 H0 M-7 16 Q0 20 8 16','none','#a07350',1.6)
 p('M-41 47 L-21 33 M42 47 L23 33','none','#78949a',3)
 end()

def background(darken=.28):
 import xml.etree.ElementTree as E
 E.register_namespace('', 'http://www.w3.org/2000/svg')
 root=E.fromstring((OUT/'L_pixel_survival.svg').read_text(encoding='utf-8'))
 for name in ['Native-world-320x180-1','Hero16-1']:
  node=next(n for n in root.iter() if n.attrib.get('id')==name)
  put(E.tostring(node,encoding='unicode'))
 put(f'<rect width="1280" height="720" fill="#07131e" opacity="{darken}"/>')

old_icon=icon
def icon(kind,x,y,k=1):
 if kind!='can':return old_icon(kind,x,y,k)
 g('Item-energy-drink',f'translate({x} {y}) rotate(-8) scale({k})','filter="url(#iconShadow)"')
 r(-14,-24,28,49,'url(#drink)',7,'#724733',1.5);e(0,-21,12,4,'#edc9a0','#ad8060');e(0,-21,6,1.7,'#9b7158');p('M-11 -12 H10 L7 15 H-8Z','#be6449');p('M3 -9 L-5 4 H2 L-2 16 L10 -1 H3Z','#f6d9a1');p('M-9 -14 V13','none','#f3c8a4',2);end()
put('<svg xmlns="http://www.w3.org/2000/svg" width="2672" height="900" viewBox="0 0 2672 900"><defs><style>')
for weight,file in [(500,'BeVietnamPro-Medium.ttf'),(700,'BeVietnamPro-Bold.ttf')]:
 data=base64.b64encode((F/file).read_bytes()).decode();put(f'@font-face{{font-family:BVP;src:url(data:font/ttf;base64,{data});font-weight:{weight}}}')
put('text{font-family:BVP,sans-serif}</style>')
for name,c1,c2 in [('card','#203440','#13232e'),('slot','#243c49','#11212c'),('header','#263d48','#142530'),('steel','#e5eadd','#809d9d'),('wood','#d2a875','#896a4c'),('leaf','#c0d586','#4f9367'),('glass','#c6eadc','#62a4ac'),('feather','#edcf86','#bc8b52'),('gold','#e6c993','#997a4f'),('drink','#e6b17d','#ae7054'),('skin','#e4c19c','#bc8e68'),('hat','#7d9b92','#466c6a')]:
 put(f'<linearGradient id="{name}" x1="0" y1="0" x2=".2" y2="1"><stop stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></linearGradient>')
put('<radialGradient id="portraitGlow"><stop stop-color="#70c2be" stop-opacity=".45"/><stop offset=".65" stop-color="#4b8c92" stop-opacity=".17"/><stop offset="1" stop-color="#142733" stop-opacity="0"/></radialGradient><radialGradient id="slotLight" cx=".2" cy="0" r="1"><stop stop-color="#7dbdbe" stop-opacity=".17"/><stop offset=".7" stop-color="#142b37" stop-opacity="0"/></radialGradient><filter id="panelShadow" x="-35%" y="-35%" width="170%" height="190%"><feDropShadow dx="0" dy="8" stdDeviation="9" flood-color="#020a12" flood-opacity=".7"/></filter><filter id="iconShadow" x="-45%" y="-45%" width="190%" height="200%"><feDropShadow dx="0" dy="3" stdDeviation="2" flood-color="#04111c" flood-opacity=".8"/></filter><clipPath id="screen"><rect width="1280" height="720" rx="14"/></clipPath></defs>')
r(0,0,2672,900,'#090f18');t(40,48,'LASTAWAY / DARK 2D UI',25,'#e8eee8',700,.7);t(40,81,'M · Khối rõ, tối màu, bóng đổ mềm và ánh sáng cục bộ · UI mượt, không pixel',14,'#8faab0');t(1885,48,'INVENTORY + HUD / 1280 × 720',16,'#b3c7c8',700)
# Inventory: separate top rail, survivor card, inventory card and detail.
g('Inventory-1280x720','translate(40 122)','clip-path="url(#screen)"');background(.78)
g('Inventory-shell','','filter="url(#panelShadow)"');r(64,48,1152,624,'#0e1924',20,'#344550');r(80,64,1120,68,'url(#header)',13,'#3b5661');p('M101 65 H1179','none','#5c7d84',.7);end()
r(104,83,4,28,'#7ab9b5',2);t(124,105,'HÀNH TRANG',25,'#eff2e9',700);t(855,103,'4 VŨ KHÍ',11,'#93b2b6',700,1);r(933,86,1,20,'#3d5d68');t(950,103,'4 HỖ TRỢ',11,'#93b2b6',700,1)
card(1144,80,40,36,False,9);p('M1158 91 L1170 103 M1170 91 L1158 103','none','#c6d6d3',2)
card(88,152,280,488);r(110,170,4,26,'#6eaba9',2);t(126,188,'NGƯỜI SỐNG SÓT',12,'#cfdfd9',700,.4)
portrait(228,263,1);t(228,359,'Sẵn sàng sinh tồn',12,'#91adb1');p('M112 377 H344','none','#35505d',1)
for yy,label,col,val,width in [(401,'MÁU','#8bbda6','100 / 100',232),(456,'GIÁP','#83a9bd','75 / 100',174),(511,'MŨ','#b7b6a1','50 / 100',116),(566,'ADRENALINE','#c3a16e','32 / 100',74)]:
 t(112,yy,label,10,'#a4b9bb',700,.7);t(279,yy,val,10,'#e1e7de');r(112,yy+11,232,10,'#081720',5,'#2e4a56');r(112,yy+11,width,10,col,5);p(f'M116 {yy+12} H{108+width}','none','#e4f0df',.6)
# Inventory card clear compartment grouping
card(392,152,800,488);t(416,182,'VŨ KHÍ',12,'#a5bdbe',700,1);t(1082,182,'1 — 4',10,'#6f929c');p('M486 178 H1156','none','#35515e',.8)
data=[('knife','Dao'),('sling','Ná cao su'),('gun','Shotgun'),('branch','Cành cây'),('leaf','Lá thần'),('can','Adrenaline'),('feather','Lông phượng'),('compass','La bàn')]
for i,(kind,label)in enumerate(data):
 xx=416+(i%4)*188;yy=204 if i<4 else 392;active=i==2
 g('Inventory-slot-'+str(i+1),'','filter="url(#panelShadow)"');r(xx,yy,168,132,'url(#slot)',12,'#ba9d68'if active else'#385562',1.5 if active else 1);r(xx+1,yy+1,166,106,'url(#slotLight)',11);p(f'M{xx+13} {yy+1} H{xx+155}','none','#d1b17a'if active else'#5f8690',.75);r(xx+10,yy+10,23,22,'#0e222d',6,'#375460');t(xx+18,yy+26,str(i+1),10,'#bed2cf',700);icon(kind,xx+84,yy+59,1.12)
 if active:r(xx+126,yy+12,28,5,'#d5b67f',2.5)
 t(xx+14,yy+116,label,12,'#eee9d7'if active else'#d0deda',700 if active else 500)
 if i>=4:r(xx+132,yy+100,24,22,'#0d202c',6);t(xx+140,yy+116,str([2,3,1,1][i-4]),11,'#e0dfc2',700)
 end()
t(416,370,'DƯỢC PHẨM &amp; HỖ TRỢ',12,'#a5bdbe',700,.8);t(1082,370,'5 — 8',10,'#6f929c');p('M606 366 H1156','none','#35515e',.8)
r(416,548,752,68,'#0b1c28',10,'#355260');r(416,548,4,68,'#b99d6a',2);icon('gun',449,580,.68);t(486,573,'SHOTGUN',12,'#e3cd9e',700,1);t(486,596,'Đang chọn · Phím 3 để chuyển vũ khí',11,'#8eabb1');r(1036,570,110,24,'#243e43',6,'#426465');t(1050,586,'ĐANG CẦM',9,'#bad4c9',700,.4)
t(88,704,'TAB  ·  Đóng hành trang',12,'#92aeb2');t(813,704,'Bố cục đề xuất · Chưa chốt hành vi inventory',11,'#6f8e98');end()
# Smooth UI on the existing authored pixel-world concept.
g('HUD-1280x720','translate(1352 122)','clip-path="url(#screen)"');background(.27)
p('M718 309 V316 M718 335 V342 M702 325 H709 M728 325 H735','none','#e5e8d6',2)
card(24,24,204,36,False,9);t(40,47,'Mây',12,'#e5e8d8',700);icon('knife',92,41,.28);t(116,47,'Bụi rậm',12,'#8ca8ad')
card(490,24,301,40,False,10);r(502,36,3,12,'#c8a56c',1.5);e(503.5,52,1.5,1.5,'#c8a56c');t(520,50,'Bom phía Đông',12,'#e2d5b8',700);t(689,50,'1.5s · 7m',12,'#d4ba85')
card(1056,24,200,200);r(1064,32,184,184,'#2f4c4c',8);p('M1064 89 L1123 63 L1154 106 L1210 61 L1248 86 L1248 125 L1202 105 L1160 150 L1101 112 L1064 143Z','#233c3e');p('M1155 32 Q1206 100 1142 146 L1131 216','none','#52736c',11);e(1157,127,68,68,'none','#94afa0');e(1157,127,43,43,'none','#c3b485');p('M1155 118 L1162 134 L1148 130Z','#f1e7c4');t(1150,50,'N',10,'#c2d5cd',700)
card(1056,232,200,74,False,10);t(1070,255,'BO THU HẸP',10,'#97b5b6',700,.5);t(1070,287,'01:24',23,'#e7e7d8',700);p('M1152 248 V292','none','#35535e',1);t(1165,256,'CÒN SỐNG',9,'#97b5b6',700);t(1183,287,'08',23,'#d8d3b7',700)
for xx,kind,sec in [(24,'leaf','2.4s'),(127,'compass','5.0s')]:
 card(xx,476,94,40,False,9);icon(kind,xx+21,495,.46);t(xx+42,502,sec,13,'#d0ddd0',700)
card(24,532,296,164);r(40,545,3,13,'#6ea8a7',1.5);t(51,555,'NGƯỜI SỐNG SÓT',10,'#b0c9c6',700,.3);p('M40 565 H304','none','#33505d',.8)
for yy,label,ww,col,val in [(577,'MÁU',264,'#8bbda6','100 / 100'),(606,'GIÁP',198,'#83a9bd','75 / 100'),(635,'MŨ',132,'#b7b6a1','50 / 100'),(664,'ADRENALINE',84,'#c3a16e','32 / 100')]:
 t(40,yy,label,9,'#91aeb3',700,.5);t(244,yy,val,10,'#ceded7');r(40,yy+7,264,8,'#081822',4,'#294854');r(40,yy+7,ww,8,col,4);p(f'M44 {yy+8} H{36+ww}','none','#dbe7d9',.6)
card(358,566,564,38,False,10);icon('gun',380,583,.45);t(407,591,'SHOTGUN',12,'#e3e7d8',700,.7);t(782,591,'6',18,'#ebdfbe',700);t(802,591,'/ 18',12,'#8ba8b0');p('M744 576 V594','none','#35535f',1)
for i,(kind,_)in enumerate(data):
 xx=358+i*71;active=i==2;card(xx,612,66,84,active,9);r(xx+4,616,58,57,'#122a35',7);r(xx+4,616,58,57,'url(#slotLight)',7);icon(kind,xx+33,645,.75);r(xx+6,676,16,16,'#0b1e2a',4);t(xx+11,688,str(i+1),10,'#a6c0c0',700)
 if i>=4:t(xx+48,688,str([2,3,1,1][i-4]),10,'#d2d5ba',700)
 if active:r(xx+18,698,30,2,'#c7aa74',1)
card(1115,608,141,36,False,9);icon('leaf',1134,624,.38);t(1153,631,'Lá thần',11,'#cdddcf');t(1223,631,'+1',11,'#b5c798',700);card(1162,664,94,28,False,8);t(1173,683,'TAB',10,'#c5c9b1',700);t(1206,683,'Balo',10,'#a4bdbd');end()
t(40,879,'DEMO UI TỐI MÀU 2D · Chưa nghiệm thu production · Chưa gắn Unity / chưa ghi được Figma',13,'#88a5ad');t(1770,879,'UI SMOOTH 2D / PIXEL GAME BACKDROP / BE VIETNAM PRO',11,'#a2babd',700,.5);put('</svg>')
(OUT/'M_dark_2d.svg').write_text('\n'.join(s),encoding='utf-8');print('Generated M_dark_2d.svg')
