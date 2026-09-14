"""Build a static, editable Unity scene from the committed DrivingTest primitives.
Python 3 standard library only. Run from any directory. No Unity API or runtime
map spawning is needed; original vehicle/camera/physics documents stay intact.
"""
from pathlib import Path
import math
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/Scenes/DrivingTest.unity'
DEST = ROOT / 'Assets/Scenes/CityOutskirts.unity'
text = SOURCE.read_text()
header = text.split('--- !u!', 1)[0]
docs = re.split(r'(?=^--- !u!)', text, flags=re.M)[1:]
def doc_id(d): return int(re.search(r'&(\d+)', d).group(1))
by_id = {doc_id(d): d for d in docs}
def name(d):
    match = re.search(r'^  m_Name: (.+)$', d, re.M)
    return match.group(1) if match else ''
def components(d): return [int(x) for x in re.findall(r'component: \{fileID: (\d+)\}', d)]
example = next(d for d in docs if name(d) == 'Grid X')
template = [example] + [by_id[i] for i in components(example)]
collider = next(d for d in docs if d.startswith('--- !u!65 '))
remove = set()
for d in docs:
    if name(d).startswith(('Grid X', 'Grid Z', '60 m radius corner marker')):
        remove.update([doc_id(d), *components(d)])
kept = [d for d in docs if doc_id(d) not in remove and not d.startswith('--- !u!1660057539 ')]
next_id = 3000000000
created = []
groups = {}
boxes = []
def alloc():
    global next_id
    next_id += 1
    return next_id

def guid(label): return uuid.uuid5(uuid.NAMESPACE_URL, 'urban-escape/city-outskirts/' + label).hex
materials = {}
base_mat = (ROOT / 'Assets/Materials/Asphalt.mat').read_text()
colors = {
    'Road': (0.12, 0.15, 0.19), 'Verge': (0.28, 0.34, 0.28),
    'Concrete': (0.52, 0.55, 0.55), 'White': (0.88, 0.90, 0.83),
    'Yellow': (0.95, 0.66, 0.16), 'Brick': (0.48, 0.29, 0.23),
    'Cream': (0.69, 0.66, 0.55), 'Blue': (0.22, 0.37, 0.44),
    'Window': (0.12, 0.22, 0.28), 'Roof': (0.22, 0.24, 0.25),
    'Teal': (0.08, 0.48, 0.48),
}
for label, color in colors.items():
    stem = 'City' + label
    asset = ROOT / ('Assets/Materials/' + stem + '.mat')
    data = base_mat.replace('m_Name: Asphalt', 'm_Name: ' + stem)
    data = re.sub(r'- _Color: .*', '- _Color: {r: %s, g: %s, b: %s, a: 1}' % color, data)
    data = data.replace('_Glossiness: 0.5', '_Glossiness: 0.12')
    asset.write_text(data)
    asset.with_suffix('.mat.meta').write_text('fileFormatVersion: 2\nguid: ' + guid(stem) + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    materials[label] = guid(stem)

def group(label):
    go, tr = alloc(), alloc()
    groups[label] = (go, tr, [])
    return tr
for label in ['Roads', 'Markings', 'Buildings', 'Street furniture']:
    group(label)

def box(label, pos, scale, mat, group_name, yaw=0, solid=False):
    old_ids = [doc_id(d) for d in template]
    mapping = {i: alloc() for i in old_ids}
    go, tr = mapping[old_ids[0]], mapping[old_ids[1]]
    parent = groups[group_name][1]
    groups[group_name][2].append(tr)
    q = (math.sin(math.radians(yaw)/2), math.cos(math.radians(yaw)/2))
    chunks = []
    for source in template:
        d = re.sub(r'(?<=&)(\d+)|(?<=fileID: )(\d+)', lambda m: str(mapping.get(int(m.group()), int(m.group()))), source)
        if d.startswith('--- !u!1 '):
            d = re.sub(r'  m_Name: .*', '  m_Name: ' + label, d)
            if solid:
                cid = alloc()
                d = d.replace('  m_Layer:', '  - component: {fileID: %d}\n  m_Layer:' % cid)
        elif d.startswith('--- !u!4 '):
            d = re.sub(r'  m_LocalPosition: .*', '  m_LocalPosition: {x: %.6f, y: %.6f, z: %.6f}' % pos, d)
            d = re.sub(r'  m_LocalScale: .*', '  m_LocalScale: {x: %.6f, y: %.6f, z: %.6f}' % scale, d)
            d = re.sub(r'  m_LocalRotation: .*', '  m_LocalRotation: {x: 0, y: %.8f, z: 0, w: %.8f}' % q, d)
            d = d.replace('m_Father: {fileID: 0}', 'm_Father: {fileID: %d}' % parent)
        elif d.startswith('--- !u!23 '):
            d = re.sub(r'guid: [a-f0-9]{32}', 'guid: ' + materials[mat], d)
            if not solid: d = d.replace('m_CastShadows: 1', 'm_CastShadows: 0')
        chunks.append(d)
    if solid:
        d = re.sub(r'^--- !u!65 &\d+', '--- !u!65 &%d' % cid, collider)
        d = re.sub(r'  m_GameObject: .*', '  m_GameObject: {fileID: %d}' % go, d)
        d = re.sub(r'  m_Size: .*', '  m_Size: {x: 1, y: 1, z: 1}', d)
        d = re.sub(r'  m_Center: .*', '  m_Center: {x: 0, y: 0, z: 0}', d)
        chunks.append(d)
    created.extend(chunks)
    boxes.append(dict(name=label, position=pos, scale=scale, material=mat, yaw=yaw, solid=solid, group=group_name))

# Replace only the ground's material: one uninterrupted original collision plane.
ground_go = next(d for d in kept if name(d) == 'Test ground 600 x 600 m')
ground_renderer = next(i for i in components(ground_go) if by_id[i].startswith('--- !u!23 '))
for i,d in enumerate(kept):
    if doc_id(d) == ground_renderer:
        kept[i] = re.sub(r'guid: [a-f0-9]{32}', 'guid: '+materials['Verge'], d)

# Clockwise loop, starting on x=0 heading +Z. Sample every <=3 m.
route = []
def point(x,z):
    if not route or math.dist(route[-1], (x,z)) > 0.0001: route.append((x,z))
def line(x1,z1,x2,z2):
    n=max(1, math.ceil(math.hypot(x2-x1,z2-z1)/3))
    for i in range(n+1):point(x1+(x2-x1)*i/n,z1+(z2-z1)*i/n)
def arc(cx,cz,start,end):
    n=24
    for i in range(n+1):
        a=math.radians(start+(end-start)*i/n)
        point(cx+40*math.cos(a),cz+40*math.sin(a))
line(0,-100,0,100)
arc(40,100,180,90)
line(40,140,180,140)
arc(180,100,90,0)
# Smooth S on the east side: zero lateral slope at both joins.
for i in range(81):
    t=i/80
    point(220-28*math.sin(math.pi*t)**2,100-200*t)
arc(180,-100,0,-90)
line(180,-140,40,-140)
arc(40,-100,-90,-180)
if math.dist(route[-1],route[0])<0.001: route.pop()
segments=list(zip(route,route[1:]+route[:1]))
travel=0
for i,(a,b) in enumerate(segments):
    dx,dz=b[0]-a[0],b[1]-a[1]
    length=math.hypot(dx,dz); yaw=math.degrees(math.atan2(dx,dz))
    x,z=(a[0]+b[0])/2,(a[1]+b[1])/2
    # Tiny overlap avoids visible gaps; all road visuals have NO collider.
    box('Road %03d'%i,(x,0.002,z),(24,0.004,length+0.6),'Road','Roads',yaw)
    if int(travel/8)%2==0:
        box('Center dash %03d'%i,(x,0.013,z),(0.18,0.008,length+0.02),'Yellow','Markings',yaw)
    for side in [-1,1]:
        # Leave a 24m-wide open junction to the parking lot.
        if side == -1 and abs(x)<0.01 and -72<z<-48:continue
        box('Road edge %03d %d'%(i,side),(x+side*11.4*dz/length,0.012,z-side*11.4*dx/length),(.18,.008,length+.1),'White','Markings',yaw)
    travel+=length

box('Practice parking 64 x 64',(-58,0.002,-60),(64,.004,64),'Road','Roads')
box('Parking access',(-22,0.002,-60),(44,.004,24),'Road','Roads')
for z in [-83,-37]:
    for x in range(-82,-37,6):
        box('Parking bay', (x,.014,z),(0.12,.008,9),'White','Markings')
# Start line sits behind the unchanged respawn point.
for i in range(12):
    box('Start line',(-11+i*2,.014,-87),(1,.008,1.2),'White','Markings')
# Sparse flat directional arrows along straights.
for x,z,yaw in [(0,-45,0),(0,70,0),(90,140,90),(220,80,180),(115,-140,-90)]:
    a=math.radians(yaw); fx,fz=math.sin(a),math.cos(a)
    box('Route arrow shaft',(x,.025,z),(0.45,.008,4),'White','Markings',yaw)
    for side in [-1,1]:
        box('Route arrow head',(x+fx*2+side*fz*.75,.026,z+fz*2-side*fx*.75),(.4,.008,2.3),'White','Markings',yaw-side*45)

# Generous building setbacks leave recovery space beyond the painted road.
footprints=[]
def building(label,x,z,w,d,h,mat):
    footprints.append((x,z,w+4,d+4))
    box(label+' sidewalk',(x,.12,z),(w+4,.24,d+4),'Concrete','Buildings',solid=True)
    box(label,(x,h/2+.24,z),(w,h,d),mat,'Buildings',solid=True)
    box(label+' roof',(x,h+.44,z),(w+.6,.4,d+.6),'Roof','Buildings')
    for y in range(3,int(h)-1,4):
        for dx in range(-int(w/2)+3,int(w/2)-1,5):
            for side in [-1,1]:
                box(label+' windows',(x+dx,y,z+side*(d/2+.015)),(2.3,1.7,.035),'Window','Buildings')
    box(label+' entrance',(x,1.8,z-d/2-.025),(3,3,.06),'Teal','Buildings')
for data in [
    ('Corner shops',52,-80,42,30,9,'Cream'),
    ('Brick offices',115,-78,38,34,22,'Brick'),
    ('Blue offices',160,-63,22,34,30,'Blue'),
    ('Central warehouse',70,-15,64,42,12,'Cream'),
    ('Service depot',146,8,32,38,11,'Blue'),
    ('North apartments',65,68,46,38,26,'Brick'),
    ('North shops',134,75,44,30,10,'Cream'),
    ('West workshop',-62,35,45,52,12,'Brick'),
    ('West corner store',-55,104,48,30,9,'Cream'),
    ('South warehouse',95,-205,96,42,14,'Blue'),
    ('East offices',272,2,22,64,25,'Cream')]:building(*data)
# A few landmark signs and poles, all outside the recovery shoulder.
for x,z in [(-20,85),(70,161),(180,165),(245,82),(245,-100),(130,-161),(-20,-110)]:
    box('Street lamp post',(x,4,z),(.22,8,.22),'Roof','Street furniture',solid=True)
    box('Street lamp head',(x,8,z),(1.5,.25,.7),'White','Street furniture')

for label,(go,tr,children) in groups.items():
    created.append(f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr}}}
  m_Layer: 0
  m_Name: {label}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:
'''+''.join('  - {fileID: %d}\n'%c for c in children)+'''  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
''')
all_docs=kept+re.split(r'(?=^--- !u!)', "".join(created), flags=re.M)[1:]
roots=[doc_id(d) for d in all_docs if d.startswith('--- !u!4 ') and 'm_Father: {fileID: 0}' in d]
all_docs.append('--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n'+''.join('  - {fileID: %d}\n'%i for i in roots))
result=header+''.join(all_docs)
# Reject broken local references before saving; external asset IDs are exempt.
ids=[doc_id(d) for d in all_docs]
assert len(ids)==len(set(ids)), 'Duplicate Unity file IDs'
for match in re.finditer(r'\{fileID: (-?\d+)([^}]*)\}',result):
    value=int(match[1])
    assert value==0 or 'guid:' in match[2] or value in ids, ('Unresolved reference',value)
# Every original physics/script/camera document is byte-identical.
for d in docs:
    if d.startswith(('--- !u!54 ', '--- !u!146 ', '--- !u!114 ', '--- !u!20 ')):
        assert d in result, 'Vehicle/camera component changed'
# Check each solid generated obstacle's rectangle against the route corridor.
for b in boxes:
    if not b['solid']:continue
    x,_,z=b['position']; w,_,depth=b['scale']
    assert b['yaw']==0
    distance=min(math.hypot(max(abs(px-x)-w/2,0),max(abs(pz-z)-depth/2,0)) for px,pz in route)
    assert distance>15, (b['name'],'insufficient road clearance',distance)
DEST.write_text(result)
DEST.with_suffix('.unity.meta').write_text('fileFormatVersion: 2\nguid: '+guid('scene')+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
import json
(ROOT/'Docs/city-layout.json').write_text(json.dumps(dict(route=route,boxes=boxes,length=travel),indent=2))
print('CityOutskirts: %.0fm loop, %d editable objects; references, vehicle preservation and obstacle clearance passed.'%(travel,len(boxes)))
