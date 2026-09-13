"""Build a portable skinned GLB and native Unity import data from V4 geometry."""
import json, math, struct, pathlib
import numpy as np
import build_model as model

D=pathlib.Path(__file__).parent
parts=model.parts
bones=[]
def bone(name,parent,pos):
    bones.append(dict(name=name,parent=parent,position=np.array(pos,float)))
    return len(bones)-1
hips=bone('Hips',-1,[0,1.10,0])
spine=bone('Spine',hips,[0,1.23,0])
chest=bone('Chest',spine,[0,1.44,0])
neck=bone('Neck',chest,[0,1.585,0])
head=bone('Head',neck,[0,1.69,0])
apron=bone('Apron',hips,[0,1.109,.07])
limbs={}
for side,label in [(-1,'Right'),(1,'Left')]:
    upper=bone(label+'UpperArm',chest,[side*.081,1.491,0])
    lower=bone(label+'Forearm',upper,[side*.1488,1.2448,.005])
    hand=bone(label+'Hand',lower,[side*.1998,.94,.013])
    thigh=bone(label+'Thigh',hips,[side*.061,1.10,0])
    shin=bone(label+'Shin',thigh,[side*.083,.56,.001])
    foot=bone(label+'Foot',shin,[side*.092,.176,.008])
    limbs[side]=(upper,lower,hand,thigh,shin,foot)
rest=np.array([b['position'] for b in bones]);parents=np.array([b['parent'] for b in bones])
local=rest.copy()
for i,p in enumerate(parents):
    if p>=0:local[i]-=rest[p]

def smooth(x):
    x=np.clip(x,0,1);return x*x*(3-2*x)

def skin_weights(p):
    n=len(p['v']);j=np.zeros((n,4),np.uint16);w=np.zeros((n,4),float)
    y=p['v'][:,1];name=p['name'];side=1 if p['v'][:,0].mean()>0 else -1
    upper,lower,hand,thigh,shin,foot=limbs[side]
    def rigid(b):j[:,0]=b;w[:,0]=1
    def blend(a,b,f):j[:,0]=a;j[:,1]=b;w[:,0]=1-f;w[:,1]=f
    if name in {'Palm','Finger','Thumb'}:rigid(hand)
    elif name=='Arm':
        blend(upper,lower,smooth((1.285-y)/.08))
        f=smooth((.99-y)/.05);w[:,:2]*=(1-f[:,None]);j[:,2]=hand;w[:,2]=f
    elif name in {'Sleeve','Sleeve_cuff'}:
        blend(chest,upper,smooth((1.50-y)/.065))
    elif name in {'Trouser','Trouser_seam'}:
        blend(thigh,shin,smooth((.61-y)/.10))
    elif name in {'Trouser_cuff','Boot_sole','Boot','Boot_shaft','Toe_cap','Boot_lace','Sole_tread'}:rigid(foot)
    elif name in {'Apron','Apron_pocket','Apron_stitch','Towel','Towel_check'}:rigid(apron)
    elif name in {'Waist','Belt','Buckle_ink','Buckle','Buckle_inset','Buckle_tongue'}:rigid(hips)
    elif name=='Neck':blend(chest,head,smooth((y-1.54)/.13))
    elif y.mean()>1.65:rigid(head)
    else:blend(hips,chest,smooth((y-1.16)/.29))
    assert np.allclose(w.sum(axis=1),1)
    order=np.argsort(-w,axis=1)
    return np.take_along_axis(j,order,axis=1),np.take_along_axis(w,order,axis=1)

for p in parts:
    p['j'],p['w']=skin_weights(p)
    zero=np.linalg.norm(p['n'],axis=1)<.5
    fallback=p['v']-p['v'].mean(axis=0)
    fallback/=np.maximum(np.linalg.norm(fallback,axis=1)[:,None],1e-8)
    p['n'][zero]=fallback[zero]

def quatx(a):return np.array([math.sin(a/2),0,0,math.cos(a/2)])
def matq(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                     [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                     [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])

def pose(clip,t):
    if clip.endswith('Book'):
        tr,rot=pose(clip[:-4],t)
        upper,lower,hand,*_=limbs[-1]
        rot[upper]=quatx(math.radians(-38))
        rot[lower]=quatx(math.radians(-72))
        rot[hand]=quatx(math.radians(20))
        return tr,rot
    duration={'Idle':2.,'Walk':1.,'Run':.64}[clip]
    phase=(t/duration)%1
    rotations=np.tile([0.,0.,0.,1.],(len(bones),1));translations=local.copy()
    if clip=='Idle':
        translations[chest,1]+=.002*math.sin(2*math.pi*phase)
        rotations[chest]=quatx(math.radians(.5)*math.sin(2*math.pi*phase))
        return translations,rotations
    run=clip=='Run';cycle=2*math.pi*phase
    translations[hips,1]-=(.067 if run else .035)
    translations[hips,1]+=(.018 if run else .008)*math.cos(2*cycle)
    rotations[chest]=quatx(math.radians(9 if run else 2))
    rotations[head]=quatx(math.radians(-7 if run else -1.5))
    rotations[apron]=quatx(math.radians(-33 if run else -16)-math.radians(4)*math.cos(2*cycle))
    for side,(upper,lower,hand,thigh,shin,foot) in limbs.items():
        q=(phase+(0 if side==1 else .5))%1
        stance=.43 if run else .60
        stride=.64 if run else .37
        if q<stance:
            z=stride*(.5-q/stance);lift=0
        else:
            s=(q-stance)/(1-stance)
            z=-stride/2+stride*smooth(s);lift=(.17 if run else .07)*math.sin(math.pi*s)**2
        # Sagittal two-bone IK; foot maintains its bind-pose orientation.
        upper_vec=local[shin];lower_vec=local[foot]
        l1=np.linalg.norm(upper_vec[1:]);l2=np.linalg.norm(lower_vec[1:])
        dy=.176+lift-translations[hips,1]
        dz=.008+z
        distance=min(math.hypot(dy,dz),l1+l2-.0001)
        phi=math.atan2(-dz,-dy)
        alpha=math.acos(np.clip((l1*l1+distance*distance-l2*l2)/(2*l1*distance),-1,1))
        knee=math.pi-math.acos(np.clip((l1*l1+l2*l2-distance*distance)/(2*l1*l2),-1,1))
        bind_upper=math.atan2(-upper_vec[2],-upper_vec[1]);bind_lower=math.atan2(-lower_vec[2],-lower_vec[1])
        hip_angle=phi-alpha-bind_upper;knee_angle=knee+bind_upper-bind_lower
        rotations[thigh]=quatx(hip_angle);rotations[shin]=quatx(knee_angle)
        rotations[foot]=quatx(-hip_angle-knee_angle)
        # Opposite arm swing; bent elbows on the run.
        swing=math.cos(2*math.pi*q)
        rotations[upper]=quatx(math.radians((26 if run else 17)*swing+(12 if run else 0)))
        rotations[lower]=quatx(math.radians(-64 if run else -12)+math.radians(8)*math.sin(2*math.pi*q))
        rotations[hand]=quatx(math.radians(5 if run else 0))
    return translations,rotations

def matrices(tr,rot):
    world=[]
    for i,p in enumerate(parents):
        m=np.eye(4);m[:3,:3]=matq(rot[i]);m[:3,3]=tr[i]
        if p>=0:m=world[p]@m
        world.append(m)
    skin=np.array(world)
    skin[:,:3,3]-=np.einsum('bij,bj->bi',skin[:,:3,:3],rest)
    return skin

def deform(p,matrices):
    m=matrices[p['j']]
    v=np.einsum('nvij,nj->nvi',m[:,:,:3,:3],p['v'])+m[:,:,:3,3]
    n=np.einsum('nvij,nj->nvi',m[:,:,:3,:3],p['n'])
    result=dict(p);result['v']=np.einsum('nv,nvi->ni',p['w'],v);result['n']=np.einsum('nv,nvi->ni',p['w'],n)
    result['n']/=np.maximum(np.linalg.norm(result['n'],axis=1)[:,None],1e-10)
    return result

clips=[]
for name,duration in [('Idle',2.),('Walk',1.),('Run',.64),('IdleBook',2.),('WalkBook',1.),('RunBook',.64)]:
    times=np.linspace(0,duration,round(duration*50)+1)
    poses=[pose(name,t) for t in times]
    tr=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses])
    assert np.allclose(tr[0],tr[-1]) and np.allclose(rot[0],rot[-1])
    clips.append(dict(name=name,duration=duration,times=times,translations=tr,rotations=rot))

# Unity data: same mesh order, skin and keyframes as the portable GLB.
vertices=np.concatenate([p['v'] for p in parts]);normals=np.concatenate([p['n'] for p in parts])
uv=np.concatenate([p['uv'] for p in parts]);joints=np.concatenate([p['j'] for p in parts]);weights=np.concatenate([p['w'] for p in parts])
sub={k:[] for k in model.C};offset=0
for p in parts:sub[p['mat']].extend((p['f']+offset).flatten().tolist());offset+=len(p['v'])
data=dict(version=4,vertices=vertices.flatten().round(7).tolist(),normals=normals.flatten().round(7).tolist(),uv=uv.flatten().round(7).tolist(),joints=joints.flatten().tolist(),weights=weights.flatten().round(7).tolist(),
          materials=[dict(name=k,color=list(c)) for k,c in model.C.items()],submeshes=[dict(triangles=indices) for indices in sub.values()],
          bones=[dict(name=b['name'],parent=b['parent'],position=local[i].tolist()) for i,b in enumerate(bones)],
          clips=[dict(name=c['name'],duration=c['duration'],times=c['times'].tolist(),tracks=[dict(bone=i,positions=c['translations'][:,i].flatten().tolist(),rotations=c['rotations'][:,i].flatten().tolist()) for i in range(len(bones))]) for c in clips])
(D/'Unity/ToastRangerRig.json').write_text(json.dumps(data,separators=(',',':')))

blob=bytearray();g={'asset':{'version':'2.0','generator':'ToastRanger V4 skeletal animation'},'scene':0,'scenes':[{'nodes':[0]}],
 'nodes':[{'name':'ToastRanger','children':[1,2]},{'name':'Body','mesh':0,'skin':0}],
 'meshes':[{'primitives':[]}],'materials':[],'buffers':[{}],'bufferViews':[],'accessors':[],'animations':[]}
for i,b in enumerate(bones):
    node={'name':b['name'],'translation':local[i].tolist()}
    children=[j+2 for j,p in enumerate(parents) if p==i]
    if children:node['children']=children
    g['nodes'].append(node)
def acc(a,typ,ctype=5126,bounds=False):
    dtype={5126:'<f4',5125:'<u4',5123:'<u2'}[ctype]
    a=np.ascontiguousarray(a,dtype=dtype)
    while len(blob)%4:blob.append(0)
    start=len(blob);blob.extend(a.tobytes());g['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':a.nbytes})
    entry={'bufferView':len(g['bufferViews'])-1,'componentType':ctype,'count':len(a),'type':typ}
    if bounds:
        entry['min']=np.atleast_1d(a.min(axis=0)).tolist();entry['max']=np.atleast_1d(a.max(axis=0)).tolist()
    g['accessors'].append(entry);return len(g['accessors'])-1
attributes={'POSITION':acc(vertices,'VEC3',bounds=True),'NORMAL':acc(normals,'VEC3'),'TEXCOORD_0':acc(uv,'VEC2'),'JOINTS_0':acc(joints,'VEC4',5123),'WEIGHTS_0':acc(weights,'VEC4')}
for i,(k,col) in enumerate(model.C.items()):
    # glTF colors are linear; convert the source sRGB palette.
    linear=np.where(col<=.04045,col/12.92,((col+.055)/1.055)**2.4)
    g['materials'].append({'name':k,'pbrMetallicRoughness':{'baseColorFactor':[*linear.tolist(),1],'metallicFactor':0,'roughnessFactor':1}})
    if sub[k]:g['meshes'][0]['primitives'].append({'attributes':attributes,'indices':acc(np.array(sub[k]),'SCALAR',5125),'material':i})
ibm=np.tile(np.eye(4),(len(bones),1,1));ibm[:,:3,3]=-rest
g['skins']=[{'name':'ToastRangerRig','joints':list(range(2,len(bones)+2)),'skeleton':2,'inverseBindMatrices':acc(ibm.transpose(0,2,1).reshape(-1,16),'MAT4')}]
for c in clips:
    ani={'name':c['name'],'channels':[],'samplers':[]};time=acc(c['times'],'SCALAR',bounds=True)
    for i in range(len(bones)):
        for path,key,typ in [('translation','translations','VEC3'),('rotation','rotations','VEC4')]:
            ani['channels'].append({'sampler':len(ani['samplers']),'target':{'node':i+2,'path':path}})
            ani['samplers'].append({'input':time,'output':acc(c[key][:,i],typ),'interpolation':'LINEAR'})
    g['animations'].append(ani)
g['buffers'][0]['byteLength']=len(blob)
while len(blob)%4:blob.append(0)
jb=json.dumps(g,separators=(',',':')).encode();jb+=b' '*((-len(jb))%4)
(D/'ToastRanger_Animated.glb').write_bytes(struct.pack('<III',0x46546c67,2,28+len(jb)+len(blob))+struct.pack('<II',len(jb),0x4e4f534a)+jb+struct.pack('<II',len(blob),0x004e4942)+blob)
np.savez_compressed(D/'rig_preview.npz',parts=np.array(parts,dtype=object),rest=rest,parents=parents,local=local,clips=np.array(clips,dtype=object))
stats=json.loads((D/'model_stats.json').read_text());stats.update(rigged=True,bones=len(bones),animations=[c['name'] for c in clips],rig_type='Custom Generic; no Humanoid avatar',root_motion=False)
(D/'model_stats.json').write_text(json.dumps(stats,indent=2))
print('Built rig:',stats)
