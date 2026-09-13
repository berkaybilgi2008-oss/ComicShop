"""Read the exported GLB independently, validate skinning and render its keyframes."""
import json,struct,pathlib,sys
import numpy as np
from PIL import Image,ImageDraw
import render_preview as renderer
D=pathlib.Path(__file__).parent
raw=(D/'ToastRanger_Animated.glb').read_bytes()
assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
jl,jt=struct.unpack_from('<II',raw,12);assert jt==0x4e4f534a
g=json.loads(raw[20:20+jl]);binary=raw[28+jl:]
width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
def read(i):
 a=g['accessors'][i];v=g['bufferViews'][a['bufferView']]
 assert v['byteOffset']+v['byteLength']<=g['buffers'][0]['byteLength']
 value=np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']],count=a['count']*width[a['type']],offset=v['byteOffset']).reshape(a['count'],width[a['type']]).copy()
 assert np.isfinite(value).all();return value
for i in range(len(g['accessors'])):read(i)
attr=g['meshes'][0]['primitives'][0]['attributes'];v=read(attr['POSITION']);n=read(attr['NORMAL']);j=read(attr['JOINTS_0']);w=read(attr['WEIGHTS_0'])
assert np.allclose(w.sum(axis=1),1) and w.min()>=0 and np.all(w[:,:-1]>=w[:,1:])
assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-4)
skin=g['skins'][0];ids=skin['joints'];assert j.max()<len(ids)
ibm=read(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
parents={child:parent for parent,node in enumerate(g['nodes']) for child in node.get('children',[])}
def transform(t,q):
 x,y,z,s=q
 m=np.eye(4);m[:3,:3]=[[1-2*(y*y+z*z),2*(x*y-z*s),2*(x*z+y*s)], [2*(x*y+z*s),1-2*(x*x+z*z),2*(y*z-x*s)], [2*(x*z-y*s),2*(y*z+x*s),1-2*(x*x+y*y)]];m[:3,3]=t
 return m
def skin_at(animation=None,frame=0):
 tr={i:node.get('translation',[0,0,0]) for i,node in enumerate(g['nodes'])}
 ro={i:node.get('rotation',[0,0,0,1]) for i,node in enumerate(g['nodes'])}
 if animation:
  for channel in animation['channels']:
   sampler=animation['samplers'][channel['sampler']];value=read(sampler['output'])[frame]
   (tr if channel['target']['path']=='translation' else ro)[channel['target']['node']]=value
 world={}
 for i in range(len(g['nodes'])):
  m=transform(tr[i],ro[i]);world[i]=world[parents[i]]@m if i in parents else m
 return np.array([world[i] for i in ids])@ibm
assert np.allclose(skin_at(),np.eye(4),atol=2e-7)
def geometry(mats):
 m=mats[j];position=np.einsum('nv,nvij,nj->ni',w,m[:,:,:3,:3],v)+np.einsum('nv,nvi->ni',w,m[:,:,:3,3])
 normal=np.einsum('nv,nvij,nj->ni',w,m[:,:,:3,:3],n);normal/=np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-12)
 return position,normal
assert np.allclose(geometry(skin_at())[0],v,atol=3e-7)
primitives=[]
for p in g['meshes'][0]['primitives']:
 f=read(p['indices']).reshape(-1,3);assert f.min()>=0 and f.max()<len(v)
 primitives.append(dict(f=f,mat=g['materials'][p['material']]['name']))
floor={};metrics={}
for ani in g['animations']:
 times=read(ani['samplers'][0]['input']).flatten()
 assert np.all(np.diff(times)>0)
 assert np.allclose(skin_at(ani,0),skin_at(ani,len(times)-1),atol=1e-6)
 minimum=[]
 for i in range(len(times)):
  pose,normals=geometry(skin_at(ani,i));assert np.isfinite(pose).all()
  minimum.append(float(pose[:,1].min()))
 floor[ani['name']]=[min(minimum),max(minimum)]
metrics.update(bones=len(ids),clips=[a['name'] for a in g['animations']],min_max_lowest_vertex_y=floor,bind_pose_max_error=float(abs(geometry(skin_at())[0]-v).max()),loop_seams='PASS',finite_vertices='PASS',weights='PASS',unity_executed=False,blender_executed=False)
data=json.loads((D/'Unity/ToastRangerRig.json').read_text())
by_name={c['name']:c for c in data['clips']}
carry_bones={i for i,b in enumerate(data['bones']) if b['name'] in {'RightUpperArm','RightForearm','RightHand'}}
for name in ['Idle','Walk','Run']:
 base=by_name[name];carry=by_name[name+'Book']
 assert base['times']==carry['times']
 for a,b in zip(base['tracks'],carry['tracks']):
  if a['bone'] not in carry_bones:assert a==b
metrics['book_lower_body_tracks_identical']='PASS'
(D/'Validation.json').write_text(json.dumps(metrics,indent=2));print(metrics,flush=True)
if '--validate-only' in sys.argv:sys.exit()
for ani in g['animations']:
 if ani['name'].startswith('Idle'):continue
 if '--book-only' in sys.argv and not ani['name'].endswith('Book'):continue
 times=read(ani['samplers'][0]['input']).flatten();frames=[]
 indices=np.round(np.linspace(0,len(times)-1,17)[:-1]).astype(int)
 for k,i in enumerate(indices):
  matrices=skin_at(ani,i)
  pos,normals=geometry(matrices);renderer.parts=[dict(p,v=pos,n=normals) for p in primitives]
  if ani['name'].endswith('Book'):
   hi=next(k for k,node in enumerate(ids) if g['nodes'][node]['name']=='RightHand')
   world=matrices[hi]@np.linalg.inv(ibm[hi])
   # A neutral size-reference book, matching the Unity generated preview prop.
   for center,size,material in [([0,-.06,.025],[.15,.22,.018],'White'),([0,-.06,.036],[.16,.23,.004],'Sauce'),([0,-.06,.014],[.16,.23,.004],'Sauce')]:
    points=[];normal=[];faces=[]
    for axis in range(3):
     for sign in [-1,1]:
      u=(axis+1)%3;vv=(axis+2)%3;off=len(points)
      for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:
       pt=np.array(center,float);pt[axis]+=sign*size[axis]/2;pt[u]+=a*size[u]/2;pt[vv]+=b*size[vv]/2
       nn=np.zeros(3);nn[axis]=sign;points.append(world[:3,:3]@pt+world[:3,3]);normal.append(world[:3,:3]@nn)
      faces.extend([[off,off+1,off+2],[off,off+2,off+3]])
    renderer.parts.append(dict(v=np.array(points),n=np.array(normal),f=np.array(faces),mat=material))
  img=renderer.render(55,420,700,270,1.15);draw=ImageDraw.Draw(img)
  draw.text((22,15),ani['name'].upper()+' / V4',font=renderer.ft(22),fill='#F3CE8C')
  draw.text((22,665),'Exported skeletal animation',font=renderer.ft(13),fill='#B7BAA5')
  frames.append(img)
  if k in [0,4,8,12]:img.save(D/(ani['name']+'_'+str(k)+'.png'))
  if k%4==0:print(ani['name'],k+1,'/ 16',flush=True)
 frames[0].save(D/(ani['name']+'.gif'),save_all=True,append_images=frames[1:],duration=round(float(times[-1])*1000/16),loop=0)
 print(ani['name']+' GIF saved',flush=True)
renderer.parts=[dict(p,v=v,n=n) for p in primitives]
face=renderer.render(0,700,520,1000,1.86);face.save(D/'Face_V4.png')
