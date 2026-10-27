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
 g('Panel','', 'filter="url(#shadow)"');r(x,y,w,h,'url(#panel)',10,'#8d8058');r(x+3,y+3,w-6,h-6,'none',8,'#39372c',.7);p(f'M{x+15} {y+1} H{x+w-15}','none','#c4ad71',.75);end()
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
base_icon=icon

def icon(kind,x,y,k=1):
 if kind!='adrenaline':return base_icon(kind,x,y,k)
 g('Item-adrenaline',f'translate({x} {y}) rotate(-10) scale({k})','filter="url(#iconShadow)"');r(-14,-24,28,49,'url(#drink)',7,'#493829',1.7);e(0,-21,12,4,'#e4c493','#8d6540');e(0,-21,6,1.7,'#7e725d');p('M-11 -12 H10 L7 15 H-8Z','#c26038');p('M3 -9 L-5 4 H2 L-2 16 L10 -1 H3Z','#f7df92');p('M-9 -14 V13','none','#f4c685',2);end()

def antique(x,y,w,h,orn=True):
 g('Antique-panel','','filter="url(#shadow)"')
 p(f'M{x+13} {y} H{x+w-13} L{x+w} {y+13} V{y+h-13} L{x+w-13} {y+h} H{x+13} L{x} {y+h-13} V{y+13}Z','url(#stone)','#24271f',3)
 p(f'M{x+16} {y+5} H{x+w-16} L{x+w-5} {y+16} V{y+h-16} L{x+w-16} {y+h-5} H{x+16} L{x+5} {y+h-16} V{y+16}Z','none','#a08a58',1.4)
 p(f'M{x+20} {y+9} H{x+w-20} L{x+w-9} {y+20} V{y+h-20} L{x+w-20} {y+h-9} H{x+20} L{x+9} {y+h-20} V{y+20}Z','none','#66664d',1)
 if orn:
  for xx,yy,rot in [(x+8,y+8,0),(x+w-8,y+8,90),(x+w-8,y+h-8,180),(x+8,y+h-8,270)]:
   g('Folded-gold-corner',f'translate({xx} {yy}) rotate({rot})');p('M0 27 V0 H27 L24 5 H7 V24Z','url(#gold)','#776345',.8);p('M3 21 V3 H21','none','#d2bd85',.8);end()
 end()

def world():
 r(0,0,1280,720,'url(#terrain)');p('M430 720 C443 572 533 486 548 400 C568 281 712 172 773 0 H900 C838 201 682 333 675 430 C664 549 592 637 579 720Z','#858467');p('M1280 0 H1050 Q991 121 1098 230 T1108 442 Q1060 550 1220 720 H1280Z','#3b5e53');p('M1066 0 Q1007 121 1115 230 T1125 444 Q1078 552 1238 720','none','#718775',4)
 for xx,yy,k in [(40,50,1.2),(213,100,1.1),(390,20,1),(106,315,1.3),(20,490,1),(236,446,.9),(947,60,1.1),(1231,366,1.2),(965,545,.9),(1170,690,1.1),(750,720,.9)]:
  g('Olive-tree',f'translate({xx} {yy}) scale({k})','filter="url(#shadow)"');e(6,10,62,36,'#283c2b')
  for dx,dy,rr in [(-30,5,34),(-23,-20,34),(14,-34,36),(40,-8,35),(15,16,37)]:
   e(dx,dy,rr,rr*.76,'url(#canopy)');p(f'M{dx-13} {dy-8} Q{dx} {dy-21} {dx+12} {dy-8}','none','#7b9061',1.3)
  end()
 for xx,yy in [(310,353),(884,441),(398,447),(1008,348)]:
  p(f'M{xx-19} {yy+12} L{xx-15} {yy-12} L{xx+5} {yy-19} L{xx+23} {yy-2} L{xx+18} {yy+14}Z','#666e56');p(f'M{xx-15} {yy-12} L{xx+5} {yy-19} L{xx+23} {yy-2} L{xx-2} {yy+1}Z','#9b9b77')
 r(0,0,1280,720,'url(#vignette)')

def portrait(x,y,k):
 g('Original-survivor-portrait',f'translate({x} {y}) scale({k})');e(0,0,64,64,'#262b24','#7e7957');e(0,1,56,56,'url(#portrait)');p('M-45 49 Q-42 18 -20 18 H20 Q42 18 45 49Z','#777657','#353b2d',2);p('M-20 22 L-7 46 H8 L21 22 L8 15 H-8Z','#c7b77f');p('M-8 15 V31 Q0 41 9 31 V15Z','#bb8b60');p('M-25 -28 Q0 -49 25 -28 L23 3 Q18 27 0 29 Q-21 25 -24 3Z','url(#skin)','#795d40',1.5);p('M-27 -24 Q-14 -55 15 -48 Q33 -41 29 -17 L20 -27 L-14 -29Z','#4c4d36');p('M-24 -23 Q0 -34 25 -23 L37 -14 L31 -9 H-34 L-39 -14Z','url(#hat)','#42452f',2);p('M-28 -26 Q0 -38 27 -25 L23 -35 Q0 -58 -22 -34Z','url(#hat)','#4c4a32',1);p('M-14 -3 H-7 M9 -3 H16','none','#4c4937',3);p('M-3 3 L-5 10 H0 M-7 16 Q0 20 8 16','none','#a07350',1.6);p('M-41 47 L-21 33 M42 47 L23 33','none','#a3a273',3);end()

put('<svg xmlns="http://www.w3.org/2000/svg" width="2672" height="900" viewBox="0 0 2672 900"><defs><style>')
for weight,file in [(500,'BeVietnamPro-Medium.ttf'),(700,'BeVietnamPro-Bold.ttf')]:
 data=base64.b64encode((F/file).read_bytes()).decode();put(f'@font-face{{font-family:BVP;src:url(data:font/ttf;base64,{data});font-weight:{weight}}}')
put('text{font-family:BVP,sans-serif}</style>')
for name,c1,c2 in [('stone','#575747','#3d4034'),('panel','#525244','#353a2f'),('bronze','#a07b43','#6e5136'),('gold','#dbba79','#987340'),('steel','#e7e2cd','#8ba396'),('wood','#c7a06c','#8e623e'),('leaf','#cadb68','#549653'),('glass','#d7edcf','#6aabb1'),('feather','#efd78c','#b98646'),('drink','#e6b66c','#a96b37'),('skin','#e2c493','#bb8c5f'),('hat','#a49b66','#6f7751'),('portrait','#777b58','#444e3b'),('terrain','#3f5942','#6f7f57')]:
 put(f'<linearGradient id="{name}" x1="0" y1="0" x2=".1" y2="1"><stop stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></linearGradient>')
put('<radialGradient id="canopy"><stop stop-color="#6e8155"/><stop offset=".65" stop-color="#485e3f"/><stop offset="1" stop-color="#344b35"/></radialGradient><radialGradient id="well"><stop stop-color="#3c3f32"/><stop offset="1" stop-color="#303429"/></radialGradient><radialGradient id="vignette"><stop offset=".35" stop-color="#17241d" stop-opacity="0"/><stop offset="1" stop-color="#132019" stop-opacity=".53"/></radialGradient><filter id="shadow" x="-30%" y="-30%" width="160%" height="170%"><feDropShadow dx="0" dy="5" stdDeviation="5" flood-color="#10170f" flood-opacity=".5"/></filter><filter id="iconShadow" x="-40%" y="-40%" width="180%" height="190%"><feDropShadow dx="0" dy="3" stdDeviation="1.5" flood-color="#151c14" flood-opacity=".8"/></filter><clipPath id="screen"><rect width="1280" height="720" rx="14"/></clipPath></defs>')
r(0,0,2672,900,'#242a22');t(40,47,'LASTAWAY / ANTIQUE SURVIVAL',24,'#e2d6b3',700,1);t(40,80,'K · Hướng demo theo ảnh bạn chọn / Đá nâu, vàng cổ, icon có khối · Chưa nghiệm thu production',14,'#a7ad8b');t(1890,47,'INVENTORY + HUD · 1280 × 720',16,'#c0b897',700)
# Inventory concept

g('Inventory-1280x720','translate(40 122)','clip-path="url(#screen)"');world();r(0,0,1280,720,'#111b15',0);put('<rect width="1280" height="720" fill="#111b15" opacity=".22"/>')
antique(68,61,1144,601)
# top center sculpted plaque
p('M499 52 H781 L767 91 L744 108 H536 L513 91Z','#34382d','#282e25',2);p('M512 53 H768 L758 85 L743 99 H537 L522 85Z','url(#bronze)','#c09c5f',1.3);p('M519 57 H539 L531 74 L521 68Z','#b89353');p('M761 57 H741 L749 74 L759 68Z','#b89353');t(562,84,'TÚI HÀNH TRANG',19,'#f1e7c8',700)
# close ivory beveled round-square
p('M1166 45 L1189 48 L1202 65 L1196 89 L1176 98 L1155 87 L1151 65Z','url(#gold)','#4a4633',2);p('M1166 51 L1187 54 L1195 67 L1190 84 L1177 90 L1162 82 L1158 67Z','#d7cda4');p('M1169 63 L1184 78 M1184 63 L1169 78','none','#5c5943',5)
# left portrait stat card
antique(106,126,282,491,False);p('M123 220 V150 H180 Q198 127 247 127 Q292 127 311 150 H371 V220','none','#b8975b',4);p('M128 219 V155 H186 M366 219 V155 H306','none','#d2b272',1.6)
portrait(247,199,1.03);t(174,294,'NGƯỜI SỐNG SÓT',15,'#e8dfc1',700);t(197,320,'Sẵn sàng sinh tồn',11,'#aab38c')
p('M132 341 H362','none','#766f4e',1);p('M141 344 H352','none','#444b39',1)
for yy,label,col,val,width in [(375,'MÁU','#c4c87b','100 / 100',222),(432,'GIÁP','#9bb5a8','75 / 100',166),(489,'MŨ','#c8b582','50 / 100',111),(546,'ADRENALINE','#d1a15d','32 / 100',71)]:
 t(136,yy,label,11,'#c7c6a6',700,.8);t(293,yy,val,11,'#e8dfc0');r(136,yy+11,222,8,'#292f24',3,'#686a4d');r(136,yy+11,width,8,col,3);p(f'M140 {yy+12} H{132+width}','none','#e6dcac',.7)
p('M135 595 H218 L247 602 L275 595 H359','none','#b08f55',2)
# right inventory exactly 8 slots, strong grouping
emblem(441,158,.38);t(466,164,'VŨ KHÍ',14,'#ded3ad',700,1);t(1053,164,'4 KHE',11,'#b9b18f',700,1);p('M436 181 H1140','none','#7e7852',1);p('M436 183 H1140','none','#343c2c',1)
items=[('knife','Dao','1'),('sling','Ná cao su','2'),('gun','Shotgun','3'),('branch','Cành cây','4'),('leaf','Lá thần','5'),('adrenaline','Adrenaline','6'),('feather','Lông phượng','7'),('compass','La bàn','8')]
for i,(kind,label,key) in enumerate(items):
 xx=502+(i%4)*178; yy=254 if i<4 else 451
 if i==2:
  e(xx,yy,55,55,'#565741','#bc9c58');e(xx,yy,51,51,'url(#well)','#ddc080');p(f'M{xx-44} {yy-39} l9 -9 M{xx+44} {yy+39} l-9 9','none','#e4c77d',2)
 else:e(xx,yy,51,51,'url(#well)','#555840')
 icon(kind,xx,yy-1,1.18);r(xx-46,yy+28,21,21,'#343b2d',5,'#777553');t(xx-39,yy+43,key,11,'#d7ceaa',700)
 if i>=4:r(xx+27,yy+28,24,21,'#30372a',5);t(xx+35,yy+43,str([2,3,1,1][i-4]),11,'#e5d5ac',700)
 put(f'<text x="{xx}" y="{yy+79}" font-size="13" font-weight="500" fill="#e0d9b9" text-anchor="middle">{label}</text>')
t(436,363,'DƯỢC PHẨM &amp; HỖ TRỢ',14,'#ded3ad',700,1);t(1053,363,'4 KHE',11,'#b9b18f',700,1);p('M436 380 H1140','none','#7e7852',1);p('M436 382 H1140','none','#343c2c',1)
r(435,566,705,52,'#353d2e',7,'#76724f');icon('gun',466,589,.58);t(505,588,'SHOTGUN',12,'#e5d2a0',700,1);t(505,607,'Đang chọn · Nhấn phím 3 để chuyển vũ khí',11,'#b7bf9a');t(1044,598,'ĐANG CẦM',10,'#cdbd82',700)
t(92,693,'TAB  ·  Đóng hành trang',12,'#c4caab');t(748,693,'BỐ CỤC ĐỀ XUẤT · CHƯA CHỐT HÀNH VI INVENTORY',10,'#a5b18f',700,.7)
end()
# HUD antique theme practical thin frames

g('HUD-1280x720','translate(1352 122)','clip-path="url(#screen)"');world()
g('Topdown-survivor','translate(640 358)','filter="url(#shadow)"');e(0,15,24,11,'#3c4e32');p('M-9 9 L-11 24 M9 9 L13 24','none','#42472f',8);r(-15,-7,30,28,'#9f8053',8,'#615a3a');p('M-12 0 L-25 8 M14 0 L30 -15','none','#cbae77',8);e(0,-16,17,15,'#ccb17b');p('M-18 -20 Q0 -39 19 -20 L21 -15 H-20Z','url(#hat)','#474e32');p('M16 0 L40 -16','none','#303e2e',7);end();p('M719 308 V315 M719 335 V342 M702 325 H709 M729 325 H736','none','#e9e2c4',2)
antique(24,24,203,34,False);t(38,46,'Mây',12,'#e6dfc3',700);icon('knife',92,40,.28);t(111,46,'Bụi rậm',12,'#aeb692')
antique(490,24,301,40,False);p('M509 34 L517 49 H501Z','none','#d3ad65',1.6);t(529,49,'Bom phía Đông',12,'#e6d5b0',700);t(688,49,'1.5s · 7m',12,'#dec087')
antique(1056,24,200,200);r(1067,35,178,178,'#617452',5);p('M1067 89 L1123 63 L1154 106 L1210 61 L1245 86 L1245 125 L1202 105 L1160 150 L1101 112 L1067 143Z','#3c553d');p('M1155 35 Q1206 100 1142 146 L1131 213','none','#89987a',11);e(1157,127,68,68,'none','#c4c18c');e(1157,127,43,43,'none','#e5d2a0');p('M1155 118 L1162 134 L1148 130Z','#fff0c2');t(1147,51,'N',10,'#f0e3b8',700)
antique(1056,232,200,74,False);t(1070,255,'BO THU HẸP',10,'#bfc09a',700,.8);t(1070,287,'01:24',23,'#eee2bd',700);p('M1151 249 V290','none','#706c4c');t(1164,259,'CÒN SỐNG',9,'#bfc09a',700);t(1183,288,'08',23,'#e2d1a1',700)
for xx,kind,sec in [(24,'leaf','2.4s'),(127,'compass','5.0s')]:
 antique(xx,476,94,40,False);icon(kind,xx+21,495,.46);t(xx+42,502,sec,13,'#dfe2ba',700)
antique(24,532,296,164);emblem(46,550,.32);t(64,555,'NGƯỜI SỐNG SÓT',10,'#d0c7a4',700,.8);p('M40 565 H304','none','#706c4b',.8)
for yy,label,ww,col,val in [(577,'MÁU',264,'#c4c87b','100 / 100'),(606,'GIÁP',198,'#9bb5a8','75 / 100'),(635,'MŨ',132,'#c8b582','50 / 100'),(664,'ADRENALINE',84,'#d1a15d','32 / 100')]:
 t(40,yy,label,9,'#bdbb98',700,.8);t(244,yy,val,10,'#e5dfbd');r(40,yy+7,264,8,'#282f24',3,'#666a4c');r(40,yy+7,ww,8,col,3);p(f'M43 {yy+8} H{39+ww}','none','#e8dfb3',.65)
antique(358,566,564,38,False);icon('gun',380,583,.45);t(407,591,'SHOTGUN',12,'#e5d9b5',700,1);t(782,591,'6',18,'#f0deb0',700);t(802,591,'/ 18',12,'#b1b394');p('M743 575 V594','none','#756f4e')
for i,kind in enumerate(['knife','sling','gun','branch','leaf','adrenaline','feather','compass']):
 xx=358+i*71;r(xx,612,66,84,'url(#panel)',8,'#c8a965' if i==2 else '#7a7854',2 if i==2 else 1);e(xx+33,645,26,26,'url(#well)','#65674b');icon(kind,xx+33,645,.74);r(xx+6,677,15,14,'#293025',3);t(xx+10,688,str(i+1),9,'#ccc7a0',700)
 if i>3:t(xx+48,688,str([2,3,1,1][i-4]),10,'#e6dbb5',700)
 if i==2:p(f'M{xx+19} 699 H{xx+47}','none','#d7b974',2)
antique(1115,608,141,36,False);icon('leaf',1134,624,.38);t(1153,631,'Lá thần',11,'#e3ddba');t(1223,631,'+1',11,'#cbd19a',700);antique(1162,664,94,28,False);t(1174,683,'TAB',10,'#d8c68e',700);t(1206,683,'Balo',10,'#bfc6a2')
end();t(40,879,'DEMO THEO HƯỚNG BẠN CHỌN · Chưa nghiệm thu toàn bộ production assets · Chưa gắn Unity / Figma',13,'#aab395');t(1810,879,'BE VIETNAM PRO / EDITABLE TEXT + VECTOR',12,'#bebc97',700,.8);put('</svg>')
(OUT/'K_antique_survival.svg').write_text('\n'.join(s),encoding='utf-8');print('Generated K_antique_survival.svg')
