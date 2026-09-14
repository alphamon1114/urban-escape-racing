"""Optional top-down layout preview (requires Pillow); not a Unity screenshot."""
from pathlib import Path
import json, math
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'Docs/city-layout.json').read_text())
im=Image.new('RGB',(1400,1450),'#101b25'); d=ImageDraw.Draw(im)
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
F=lambda n:ImageFont.truetype(font,n)
d.text((65,40),'CITY OUTSKIRTS',font=F(42),fill='#edf1ee')
d.text((65,100),'941 m loop  /  24 m roads  /  original drift handling',font=F(21),fill='#a7babf')
def p(x,z):return (int(350+x*2.65),int(685-z*2.65))
colors={'Road':'#303d49','Concrete':'#8b938e','White':'#d9dfd4','Yellow':'#eeb54c','Brick':'#ab7561','Cream':'#c9bea2','Blue':'#617f8e','Roof':'#2d3641','Window':'#16333e','Teal':'#398e85'}
# Draw ground, then asphalt and markings, then building footprints.
d.rectangle((40,160,1360,1340),fill='#4a5b50')
for b in data['boxes']:
 if b['group']=='Street furniture' or b['material'] in ['Roof','Window','Teal']:continue
 x,y,z=b['position'];w,h,depth=b['scale'];a=math.radians(b['yaw'])
 pts=[p(x+dx*math.cos(a)+dz*math.sin(a),z-dx*math.sin(a)+dz*math.cos(a)) for dx,dz in [(-w/2,-depth/2),(-w/2,depth/2),(w/2,depth/2),(w/2,-depth/2)]]
 d.polygon(pts,fill=colors[b['material']])
# Screen labels for the route and major zones.
for x,z,label in [(0,15,'01  START STRAIGHT'),(96,163,'02  WIDE CORNERS'),(228,0,'03  S-BENDS'),(-89,-104,'PRACTICE LOT')]:
 px,py=p(x,z); tw=d.textbbox((0,0),label,font=F(17))[2]
 d.rounded_rectangle((px-8,py-6,px+tw+8,py+26),radius=5,fill='#12222a')
 d.text((px,py),label,font=F(17),fill='#f0eee0')
x,y=p(0,-80)
d.ellipse((x-9,y-9,x+9,y+9),fill='#48dfb7',outline='white',width=2)
d.text((x+16,y-10),'SPAWN / R',font=F(18),fill='#ffffff')
d.text((65,1370),'LAYOUT DIAGRAM — static scene geometry, not a Unity render',font=F(20),fill='#a7babf')
im.save(root/'Docs/city-outskirts-layout.png')
