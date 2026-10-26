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
put('<svg xmlns="http://www.w3.org/2000/svg" width="2672" height="900" viewBox="0 0 2672 900"><defs><style>')
for weight,file in [(500,'BeVietnamPro-Medium.ttf'),(700,'BeVietnamPro-Bold.ttf')]:
 data=base64.b64encode((F/file).read_bytes()).decode();put(f'@font-face{{font-family:BVP;src:url(data:font/ttf;base64,{data});font-weight:{weight}}}')
put('text{font-family:BVP,sans-serif}</style>')
for name,c1,c2 in [('panel','#244e47','#102d2a'),('button','#d3e3a1','#94b86e'),('gold','#e2ce91','#9b7d43'),('steel','#e1e8cd','#6c9b92'),('wood','#d1a66b','#8c633d'),('leaf','#c7d76b','#589a54'),('glass','#c9e7ce','#579b95'),('feather','#f2d88d','#be8c49'),('terrain','#477555','#7f986a')]:
 put(f'<linearGradient id="{name}" x1="0" y1="0" x2="0.2" y2="1"><stop stop-color="{c1}"/><stop offset="1" stop-color="{c2}"/></linearGradient>')
put('<linearGradient id="scrim"><stop stop-color="#071e20" stop-opacity=".97"/><stop offset=".43" stop-color="#092523" stop-opacity=".85"/><stop offset=".78" stop-color="#092523" stop-opacity=".1"/><stop offset="1" stop-color="#092523" stop-opacity="0"/></linearGradient><radialGradient id="canopy"><stop stop-color="#679875"/><stop offset=".65" stop-color="#3b7855"/><stop offset="1" stop-color="#245943"/></radialGradient><radialGradient id="vignette"><stop offset=".4" stop-color="#092f2b" stop-opacity="0"/><stop offset="1" stop-color="#072623" stop-opacity=".45"/></radialGradient><filter id="shadow" x="-30%" y="-30%" width="160%" height="170%"><feDropShadow dx="0" dy="5" stdDeviation="6" flood-color="#051c1b" flood-opacity=".35"/></filter><filter id="iconShadow" x="-40%" y="-40%" width="180%" height="190%"><feDropShadow dx="0" dy="2" stdDeviation="1" flood-color="#092420" flood-opacity=".65"/></filter><clipPath id="screen"><rect width="1280" height="720" rx="14"/></clipPath></defs>')
r(0,0,2672,900,'#0b2324');t(40,48,'LASTAWAY / ART EXPLORATION 02',24,'#e6dfc1',700,1);t(40,81,'J · Rừng thám hiểm / Chất liệu, ánh sáng và phân cấp · Concept chưa duyệt',14,'#8eafa0');t(1885,48,'MENU + HUD · 1280 × 720',17,'#c0caa9',700)
g('Menu-1280x720','translate(40 122)','clip-path="url(#screen)"')
img=base64.b64encode((OUT/'J_sources/MenuIsland.png').read_bytes()).decode();put(f'<image id="Menu-painting-only" width="1280" height="720" href="data:image/png;base64,{img}" preserveAspectRatio="xMidYMid slice"/>');r(0,0,1280,720,'url(#scrim)')
p('M60 65 H178 M60 65 V83 M1220 65 H1164 M1220 65 V83','none','#6d8e77',1)
emblem(92,118,.9);t(137,111,'LAST SURVIVOR',11,'#c5c5a0',700,2.7);t(137,131,'JUNGLE EXPEDITION',10,'#8eae97',500,2)
t(60,243,'LASTAWAY',78,'#f5eed7',700,-2.5);p('M65 272 H145 M155 272 H208','none','#c5b77a',2);e(217,272,2,2,'#c5b77a')
t(65,312,'Một hòn đảo. Một người sống sót.',18,'#e1dfc6');t(65,342,'Khám phá. Thích nghi. Sống đến cuối cùng.',13,'#a8bca4')
g('Primary-action','','filter="url(#shadow)"');p('M76 397 H408 L420 409 V455 L408 467 H76 L64 455 V409Z','url(#button)','#dbdfa8',1.2);p('M80 401 H403','none','#edf0c8',1);t(91,440,'CHƠI NGAY',20,'#234332',700,1);p('M373 432 H393 M386 425 L394 432 L386 439','none','#34503a',1.8);end()
for yy,label in [(487,'Cài đặt'),(549,'Thoát')]:
 g('Secondary-action','','filter="url(#shadow)"');r(64,yy,356,50,'url(#panel)',7,'#567d68');t(91,yy+32,label,16,'#e1e5ce');p(f'M378 {yy+24} H392','none','#7b9c7f');end()
e(70,651,4,4,'#c5db86');t(84,656,'Máy chủ sẵn sàng',12,'#bdcfb0');t(65,690,'ESC  ·  Thoát',11,'#809e87');t(1003,690,'BẢN THỬ NGHIỆM',11,'#e1dcb9',700,1)
end()
g('HUD-1280x720','translate(1352 122)','clip-path="url(#screen)"')
r(0,0,1280,720,'url(#terrain)')
p('M1280 0 H1028 C1005 107 1150 118 1151 225 C1151 349 1022 401 1055 514 C1080 590 1171 649 1227 720 H1280Z','#366d69');p('M1041 0 C1021 109 1166 117 1165 225 C1165 351 1037 406 1070 511 C1095 586 1180 636 1240 720','none','#85a89b',5)
p('M468 720 C459 592 544 516 553 436 C568 303 764 195 791 0 H909 C859 220 678 338 673 447 C669 536 592 638 604 720Z','#a4a277');p('M513 720 C510 590 597 515 603 438 C613 304 806 182 844 0','none','#b7b086',3)
random.seed(13)
# Natural canopy clusters remain outside the central action area.
for xx,yy,k in [(50,80,1.2),(190,135,1),(340,25,1.3),(466,60,.8),(116,320,1.3),(38,468,1.3),(242,432,.85),(931,80,1),(1230,361,1.1),(981,536,.9),(1163,696,1.2),(759,711,.8)]:
 g('Layered-canopy',f'translate({xx} {yy}) scale({k})','filter="url(#shadow)"');e(5,12,65,35,'#335f43')
 for dx,dy,rr in [(-29,7,32),(-24,-22,36),(14,-35,37),(42,-8,34),(15,15,38)]:
  e(dx,dy,rr,rr*.77,'url(#canopy)');p(f'M{dx-14} {dy-8} Q{dx} {dy-22} {dx+13} {dy-9}','none','#74a67a',1.5)
 for j in range(5):leaf(random.randint(-45,45),random.randint(-15,20),.38,random.randint(-90,90),'#3d805c')
 end()
# Small weathered ruin and natural stones, with bevel planes.
g('Ancient-stones','translate(1010 373)');p('M-38 35 L-31 -42 L8 -49 L35 -32 L30 39Z','#738b77');p('M-31 -42 L8 -49 L35 -32 L-4 -28Z','#a3ae8a');p('M-4 -28 L35 -32 L30 39 L-8 42Z','#5b7769');p('M-24 -19 L-14 -9 L-23 3 M8 -15 L18 -5','none','#a0ad87',2);leaf(-15,40,.55,-25,'#5b8b59');end()
for xx,yy in [(310,353),(884,441),(397,445)]:
 p(f'M{xx-19} {yy+12} L{xx-15} {yy-12} L{xx+5} {yy-19} L{xx+23} {yy-2} L{xx+18} {yy+14}Z','#788d78');p(f'M{xx-15} {yy-12} L{xx+5} {yy-19} L{xx+23} {yy-2} L{xx-2} {yy+1}Z','#a4af8a')
for i in range(42):
 xx=random.randint(20,1260);yy=random.randint(30,710)
 if 375<xx<930 and 175<yy<535:continue
 p(f'M{xx} {yy} l-3 -8 m3 8 l4 -6','none','#9fb388',1)
r(0,0,1280,720,'url(#vignette)')
# Native character silhouette; UI icon art remains vector.
g('Survivor','translate(640 358)','filter="url(#shadow)"');e(0,15,24,11,'#4a6548');p('M-9 9 L-11 24 M9 9 L13 24','none','#314b3b',8);r(-15,-7,30,28,'#ac7d4f',8,'#5f573b');p('M-12 0 L-25 8 M14 0 L30 -15','none','#d6b176',8);e(0,-16,17,15,'#ddb67a');p('M-18 -20 Q0 -39 19 -20 L21 -15 H-20Z','#6d8260','#344d3b');p('M16 0 L40 -16','none','#243e36',7);end()
p('M719 308 V315 M719 335 V342 M702 325 H709 M729 325 H736','none','#f1ebcd',2)
panel(24,24,203,34);t(38,46,'Mây',12,'#e6e5c9',700);icon('knife',92,40,.28);t(111,46,'Bụi rậm',12,'#9bb99c')
panel(490,24,301,40);p('M509 34 L517 49 H501Z','none','#d5ae66',1.6);t(529,49,'Bom phía Đông',12,'#eedbbb',700);t(688,49,'1.5s · 7m',12,'#e8c882')
# Minimap and semantic time/status.
panel(1056,24,200,200);r(1064,32,184,184,'#547d64',7);p('M1064 89 L1123 63 L1154 106 L1210 61 L1248 86 L1248 125 L1202 105 L1160 150 L1101 112 L1064 143Z','#315f4c');p('M1155 32 Q1206 100 1142 146 L1131 216','none','#83a78a',11);e(1157,127,69,69,'none','#bcce91');e(1157,127,44,44,'none','#e2d7af');p('M1155 118 L1162 134 L1148 130Z','#fff4ca');t(1147,50,'N',10,'#f2e6bb',700)
panel(1056,232,200,74);t(1070,255,'BO THU HẸP',10,'#a9c3a1',700,1);t(1070,287,'01:24',23,'#f0e9cd',700);p('M1151 249 V290','none','#426658');t(1164,259,'CÒN SỐNG',9,'#a9c3a1',700);t(1183,288,'08',23,'#e2d7af',700)
for xx,kind,sec in [(24,'leaf','2.4s'),(127,'compass','5.0s')]:
 panel(xx,476,94,40);icon(kind,xx+21,495,.46);t(xx+42,502,sec,13,'#e0e7c5',700)
panel(24,532,296,164);emblem(46,550,.32);t(64,555,'NGƯỜI SỐNG SÓT',10,'#bbccac',700,1);p('M40 565 H304','none','#416352',.8)
for yy,label,ww,col,val in [(580,'MÁU',264,'#c4d990','100 / 100'),(617,'GIÁP',198,'#86b7b0','75 / 100'),(654,'ADRENALINE',84,'#d7b570','32 / 100')]:
 t(40,yy,label,9,'#acbea0',700,1);t(244,yy,val,10,'#e1e6ca');r(40,yy+7,264,8,'#0d2725',3,'#375a4b');r(40,yy+7,ww,8,col,3);p(f'M43 {yy+8} H{39+ww}','none','#eff1c8',.65)
panel(358,566,564,38);icon('gun',380,583,.45);t(407,591,'SHOTGUN',12,'#e9e5ce',700,1);t(782,591,'6',18,'#f1e6be',700);t(802,591,'/ 18',12,'#9eb79e');p('M743 575 V594','none','#4e715c')
for i,kind in enumerate(['knife','sling','gun','branch','leaf','vial','feather','compass']):
 xx=358+i*71;g('Quick-slot-'+str(i+1),'','filter="url(#shadow)"');r(xx,612,66,84,'url(#panel)',8,'#d7cb8d' if i==2 else '#577b64',2 if i==2 else 1);r(xx+4,616,58,57,'#254c3d',5);p(f'M{xx+8} 617 H{xx+58}','none','#6c8d6a',.7);icon(kind,xx+33,645,.72);r(xx+6,677,15,14,'#102b27',3);t(xx+10,688,str(i+1),9,'#c4d2ad',700)
 if i>3:t(xx+48,688,str([2,3,1,1][i-4]),10,'#e7e6c9',700)
 if i==2:p(f'M{xx+19} 699 H{xx+47}','none','#d9cd91',2)
 end()
panel(1115,608,141,36);icon('leaf',1134,624,.38);t(1153,631,'Lá thần',11,'#e4e9cf');t(1223,631,'+1',11,'#c6d897',700);panel(1162,664,94,28);t(1174,683,'TAB',10,'#d6cfaa',700);t(1206,683,'Balo',10,'#bcd0b1')
end()
t(40,879,'CONCEPT CHƯA DUYỆT · Minh họa riêng + UI text/vector editable · Chưa thay asset hoặc prefab Unity',13,'#8fae9b');t(1850,879,'BE VIETNAM PRO / NATIVE SVG LAYERS',12,'#aebd9d',700,1)
put('</svg>')
(OUT/'J_jungle_polished.svg').write_text('\n'.join(s),encoding='utf-8')
print('Generated J_jungle_polished.svg')
