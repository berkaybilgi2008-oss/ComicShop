import numpy as np, math, json, struct, pathlib, random
from PIL import Image,ImageDraw,ImageFont,ImageFilter
D=pathlib.Path(__file__).parent
random.seed(12)
P={'Crust':'B87532','Bread':'F3CE8C','ToastMarks':'CC9650','Olive':'737650','OliveDark':'454B36','Red':'C7442D','Sauce':'AA291D','Cheese':'FFD074','Leather':'65452F','Pants':'3F3631','Sole':'B29A6B','Gold':'D7A14E','Ink':'211C19','White':'FFF4D8','Blush':'ED8848'}
C={k:np.array([int(h[i:i+2],16)/255 for i in (0,2,4)]) for k,h in P.items()}
parts=[]
def add(name,v,f,mat):
 v=np.array(v,dtype=float); f=np.array(f,dtype=int)
 cross=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]])
 good=np.linalg.norm(cross,axis=1)>1e-10; f=f[good];cross=cross[good]
 n=np.zeros_like(v)
 for i in range(3):np.add.at(n,f[:,i],cross)
 n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12)
 uv=np.column_stack((np.arctan2(v[:,2]-v[:,2].mean(),v[:,0]-v[:,0].mean())/(2*math.pi)+.5,(v[:,1]-v[:,1].min())/max(np.ptp(v[:,1]),1e-8)))
 parts.append(dict(name=name,v=v,f=f,n=n,uv=uv,mat=mat))
def ell(name,c,s,mat,e=1,nu=24,nv=14):
 def sp(x):return np.sign(x)*abs(x)**e
 v=[]
 for j in range(nv+1):
  a=-math.pi/2+math.pi*j/nv
  for i in range(nu):
   b=2*math.pi*i/nu;v.append(np.array(c)+np.array(s)*[sp(math.cos(a))*sp(math.cos(b)),sp(math.sin(a)),sp(math.cos(a))*sp(math.sin(b))])
 f=[]
 for j in range(nv):
  for i in range(nu):
   a=j*nu+i;b=j*nu+(i+1)%nu;f.extend([(a,a+nu,b),(b,a+nu,b+nu)])
 add(name,v,f,mat)
def tube(name,pts,r,mat,seg=10):
 pts=np.array(pts,float);r=np.broadcast_to(r,(len(pts),));v=[]
 for j,p in enumerate(pts):
  t=pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)];t/=np.linalg.norm(t)
  u=np.cross(t,[0,0,1] if abs(t[2])<.9 else [0,1,0]);u/=np.linalg.norm(u);w=np.cross(t,u)
  v.extend([p+r[j]*(u*math.cos(a)+w*math.sin(a)) for a in np.arange(seg)*2*math.pi/seg])
 f=[]
 for j in range(len(pts)-1):
  for i in range(seg):
   a=j*seg+i;b=j*seg+(i+1)%seg;f.extend([(a,b,a+seg),(b,b+seg,a+seg)])
 v.extend([pts[0],pts[-1]])
 for i in range(seg):f.extend([(len(v)-2,(i+1)%seg,i),(len(v)-1,(len(pts)-1)*seg+i,(len(pts)-1)*seg+(i+1)%seg)])
 add(name,v,f,mat)
def patch(name,pts,mat):
 # shallow double-sided closed detail
 v=[(x,y,z) for x,y,z in pts]+[(x,y,z-.006) for x,y,z in pts];l=len(pts);f=[]
 for i in range(1,l-1):f.extend([(0,i,i+1),(l,l+i+1,l+i)])
 for i in range(l):j=(i+1)%l;f.extend([(i,l+i,j),(j,l+i,l+j)])
 add(name,v,f,mat)
# Y up, +Z face, 2.15m stylized body
ell('Toast_crust',(0,1.745,0),(.254,.227,.123),'Crust',.24,32,20)
ell('Soft_bread_face',(0,1.745,.117),(.223,.193,.018),'Bread',.26,32,20)
ell('Soft_bread_back',(0,1.745,-.117),(.218,.191,.012),'Bread',.27,24,16)
for side in [-1,1]:
 x=side*.089
 ell('Eye_ink',(x,1.771,.141),(.051,.068,.014),'Ink')
 ell('Eye_white',(x,1.772,.151),(.046,.062,.01),'White')
 ell('Pupil',(x+.009,1.770,.161),(.032,.042,.009),'Ink')
 ell('Eye_glint',(x+.003,1.789,.169),(.011,.014,.004),'White',nu=12,nv=8)
 ell('Cheek',(side*.151,1.690,.1352),(.006,.005,.0003),'Blush',nu=16,nv=10)
 tube('Eyebrow',[(x-.031,1.862,.144),(x-.013,1.873,.146),(x+.012,1.876,.145),(x+.032,1.868,.143)],.008,'Leather')
tube('Smile',[(x,1.662+.021*(x/.054)**2,.142) for x in np.linspace(-.054,.054,15)],.0045,'Ink',8)
for i in range(54):
 x=random.uniform(-.198,.198);y=random.uniform(1.58,1.91)
 if abs(x)<.16 and 1.64<y<1.89:continue
 ell('Bread_pore',(x,y,.137),(random.uniform(.002,.005),random.uniform(.002,.005),.0018),'ToastMarks',nu=8,nv=6)
# V4: retain the RNG sequence for the hat, but remove all facial pores.
parts=[p for p in parts if p['name']!='Bread_pore']
P['Blush']='F0CCA0'
C['Blush']=np.array([int(P['Blush'][i:i+2],16)/255 for i in (0,2,4)])
# clothing
ell('Neck',(0,1.48,0),(.029,.088,.03),'Bread')
ell('Shirt',(0,1.293,0),(.123,.166,.067),'Olive',.48,24,18)
ell('Waist',(0,1.102,0),(.105,.079,.058),'Pants',.4)
ell('Belt',(0,1.128,0),(.112,.025,.066),'Leather',.3)
ell('Buckle_ink',(0,1.128,.071),(.030,.030,.009),'Ink')
ell('Buckle',(0,1.128,.078),(.026,.026,.007),'Gold')
ell('Buckle_inset',(0,1.128,.084),(.019,.019,.003),'Leather')
tube('Buckle_tongue',[(-.004,1.128,.09),(.023,1.128,.09)],.003,'Gold',8)
for side in [-1,1]:
 patch('Collar',[(side*.008,1.456,.050),(side*.057,1.462,.025),(side*.092,1.403,.061),(side*.034,1.424,.078)],'OliveDark')
 shoulder=(side*.118,1.402,0);elbow=(side*.184,1.225,.005);wrist=(side*.235,1.027,.013)
 tube('Sleeve',[(side*.065,1.400,0),(side*.085,1.399,0),(side*.111,1.387,0),(side*.136,1.360,0),(side*.151,1.331,0),(side*.160,1.307,0)],[.024,.038,.043,.042,.040,.039],'Olive',24)
 tube('Sleeve_cuff',[(side*.157,1.316,0),(side*.162,1.300,0)],[.043,.043],'OliveDark',16)
 arm_pts=[(side*.161,1.300,0),elbow,wrist]
 tube('Arm',[np.array(arm_pts[j])*(1-t)+np.array(arm_pts[j+1])*t for j in range(2) for t in np.linspace(0,1,7,endpoint=(j==1))],[.022+(.018-.022)*t if j==0 else .018+(.014-.018)*t for j in range(2) for t in np.linspace(0,1,7,endpoint=(j==1))],'Bread',12)
 ell('Palm',(side*.239,.993,.013),(.023,.041,.015),'Bread',.6,16,10)
 for finger in range(4):
  x=side*(.222+finger*.012);l=[.043,.055,.052,.041][finger]
  tube('Finger',[(x,.974,.014),(x+side*.003,.951,.021),(x,.974-l,.029)],[.0065,.006,.0045],'Bread',8)
 tube('Thumb',[(side*.218,1.005,.02),(side*.205,.981,.03),(side*.21,.967,.04)],[.008,.007,.005],'Bread',8)
 leg_pts=[(side*.061,1.10,0),(side*.073,.84,-.012),(side*.083,.56,.001),(side*.092,.176,.008)]
 leg_r=[.047,.041,.033,.032]
 tube('Trouser',[np.array(leg_pts[j])*(1-t)+np.array(leg_pts[j+1])*t for j in range(3) for t in np.linspace(0,1,6,endpoint=(j==2))],[leg_r[j]*(1-t)+leg_r[j+1]*t for j in range(3) for t in np.linspace(0,1,6,endpoint=(j==2))],'Pants',16)
 seam_pts=[(side*.094,1.064,.026),(side*.107,.80,.019),(side*.112,.56,.018),(side*.117,.20,.027)]
 tube('Trouser_seam',[np.array(seam_pts[j])*(1-t)+np.array(seam_pts[j+1])*t for j in range(3) for t in np.linspace(0,1,6,endpoint=(j==2))],.002,'Leather',6)
 ell('Trouser_cuff',(side*.092,.185,.008),(.037,.018,.037),'Leather',.4,16,10)
 ell('Boot_sole',(side*.097,.029,.043),(.055,.027,.102),'Sole',.35,20,10)
 ell('Boot',(side*.097,.076,.049),(.052,.052,.097),'Leather',.4,20,12)
 ell('Boot_shaft',(side*.092,.128,.005),(.041,.066,.050),'Leather',.45,16,12)
 ell('Toe_cap',(side*.097,.065,.119),(.052,.039,.036),'Pants',.5,16,10)
 for j in range(5):
  y=.087+j*.013;z=.102-j*.014
  tube('Boot_lace',[(side*.097-.024,y,z),(side*.097+.024,y+.007,z-.006)],.0028,'Gold',6)
 for j in range(5):ell('Sole_tread',(side*.097,.013,-.033+j*.038),(.056,.009,.010),'Pants',.3,12,6)
# apron and small details
patch('Apron', [(-.103,1.109,.068),(.100,1.109,.068),(.089,.901,.081),(-.087,.907,.081)],'Leather')
patch('Apron_pocket',[(-.052,1.025,.086),(.059,1.025,.086),(.053,.952,.09),(-.048,.951,.09)],'Pants')
for x in [-.079,.080]:tube('Apron_stitch',[(x,1.095,.077),(x*.87,.917,.09)],.0018,'Gold',6)
ell('Bandana_knot',(0,1.424,.080),(.020,.022,.016),'Red')
tube('Bandana_neck',[(-.05,1.452,.035),(-.032,1.433,.067),(0,1.422,.074),(.035,1.434,.06),(.048,1.456,.023)],.012,'Red',10)
patch('Bandana_left',[(-.008,1.423,.087),(-.044,1.338,.087),(-.012,1.355,.093),(.008,1.410,.09)],'Red')
patch('Bandana_right',[(.008,1.423,.085),(.035,1.363,.092),(.052,1.382,.080),(.02,1.430,.080)],'Sauce')
tube('Shirt_placket',[(0,1.355,.067),(0,1.157,.061)],.003,'OliveDark',6)
for y in [1.18,1.23,1.28,1.33]:ell('Shirt_button',(0,y,.074),(.004,.004,.003),'Gold',nu=8,nv=6)
for side in [-1,1]:
 patch('Shirt_pocket',[(side*.030,1.36,.061),(side*.094,1.36,.051),(side*.088,1.302,.062),(side*.059,1.291,.07),(side*.028,1.307,.073)],'OliveDark')
ell('Comics_patch',(.065,1.367,.066),(.035,.014,.004),'Red',.22,16,8)
# small label lettering made from strokes
font={'C':[(1,0),(0,0),(0,1),(1,1)],'O':[(1,0),(0,0),(0,1),(1,1),(1,0)],'M':[(0,0),(0,1),(.5,.4),(1,1),(1,0)],'I':[(.5,0),(.5,1)],'S':[(0,0),(1,0),(1,.5),(0,.5),(0,1),(1,1)]}
for i,ch in enumerate('COMICS'):
 tube('Label_'+ch,[(.035+i*.009+a*.006,1.363+b*.008,.072) for a,b in font[ch]],.0008,'White',5)
# cloth hanging at hip
patch('Towel',[(.078,1.113,.082),(.105,1.111,.08),(.123,.941,.09),(.092,.941,.092)],'Red')
for j in range(9):tube('Towel_check',[(.087+j*.0015,1.095-j*.017,.087),(.107+j*.0017,1.095-j*.017,.089)],.0015,'Bread',6)
# sculpted cowboy brim, upswept sides and dipped front
N=72;R=8
def brim(a,t=1):
 x=(.135+.254*t)*math.cos(a);z=(.102+.139*t)*math.sin(a)
 y=1.935+.13*t**2*abs(math.cos(a))**4-.030*t*max(math.sin(a),0)+.009*math.sin(3*a)*t+.007*math.sin(9*a+.4)*t**3
 return np.array([x,y,z])
v=[]
for layer in [0,1]:
 for j in range(R+1):
  for i in range(N):v.append(brim(2*math.pi*i/N,j/R)+[0,-.009*layer,0])
f=[];K=(R+1)*N
for j in range(R):
 for i in range(N):
  a=j*N+i;b=j*N+(i+1)%N
  f.extend([(a,b,a+N),(b,b+N,a+N),(K+a,K+a+N,K+b),(K+b,K+a+N,K+b+N)])
for i in range(N):
 a=R*N+i;b=R*N+(i+1)%N
 f.extend([(a,K+a,b),(b,K+a,K+b),(i,(i+1)%N,K+i),((i+1)%N,K+(i+1)%N,K+i)])
add('Cowboy_brim',v,f,'Crust')
tube('Rolled_brim',[brim(a) for a in np.linspace(0,2*math.pi,97)],.004,'Bread',8)
# crown with pinched top, sloping sides
v=[];rings=[(1.936,.145,.113),(1.978,.138,.106),(2.054,.119,.092),(2.130,.093,.074),(2.160,.083,.061)]
for j,(y,rx,rz) in enumerate(rings):
 for i in range(48):
  a=2*math.pi*i/48;v.append([rx*math.cos(a),y-(.026*math.sin(a)**2 if j==4 else 0),rz*math.sin(a)])
f=[]
for j in range(4):
 for i in range(48):
  a=j*48+i;b=j*48+(i+1)%48;f.extend([(a,a+48,b),(b,a+48,b+48)])
v.append([0,2.137,0])
for i in range(48):f.append((len(v)-1,4*48+(i+1)%48,4*48+i))
add('Pinched_crown',v,f,'Crust')
# band at crown base
for y in [1.961,1.980]:tube('Hat_band',[(.142*math.cos(a),y,.109*math.sin(a)) for a in np.linspace(0,2*math.pi,65)],.012,'Leather',8)
pts=[]
for i in range(10):
 a=math.pi/2+i*math.pi/5;r=.034 if i%2==0 else .015
 pts.append((r*math.cos(a),1.977+r*math.sin(a),.126))
patch('Sheriff_star',pts,'Gold');ell('Star_center',(0,1.977,.132),(.008,.008,.003),'Crust',nu=10,nv=6)
# red sauce and golden cheese draping on brim
for start,end in [(.16,1.1),(1.22,2.8),(3.2,4.7),(5.1,6.1)]:
 tube('Sauce_ribbon',[brim(a,.78)+[0,.017,0] for a in np.linspace(start,end,20)],.014,'Sauce',10)
 tube('Cheese_ribbon',[brim(a,.93)+[0,.014,0] for a in np.linspace(start,end,22)],.012,'Cheese',10)
for a,length in [(.60,.081),(1.01,.123),(1.80,.051),(2.53,.074),(4.15,.065),(5.6,.05)]:
 p=brim(a,.95);q=p+[0,-length,0]
 tube('Cheese_drip',[p+[0,.012,0],p+[0,-.018,.005],q+[0,.012,.009],q],[.014,.01,.005,.004],'Cheese',10)
 ell('Drip_tip',q,(.007,.011,.007),'Cheese',nu=12,nv=8)
for a in [.33,.85,1.37,2.2,2.75,3.6,4.4,5.1,5.85]:
 p=brim(a,.6)+[0,.025,0]
 ell('Pepperoni_edge',p,(.039,.011,.035),'Crust',nu=20,nv=8)
 ell('Pepperoni',p+[0,.006,0],(.034,.009,.031),'Red',nu=20,nv=8)
 for i in range(5):
  dx=random.uniform(-.021,.021);dz=random.uniform(-.018,.018)
  ell('Pepperoni_fat',p+[dx,.015,dz],(.004,.002,.003),'Cheese',nu=6,nv=4)
for i in range(40):
 a=random.random()*math.tau;y=random.uniform(2.008,2.127);rx=.138-(y-1.978)*.32;rz=rx*.77
 ell('Crown_toast_spot',(rx*math.cos(a),y,rz*math.sin(a)),(.004,.006,.004),'ToastMarks',nu=8,nv=6)
# V2: fried corn-chip blisters and toasted freckles, not a fabric hat.
for i in range(130):
 a=random.random()*math.tau;t=random.uniform(.14,.91);pos=brim(a,t)+[0,.005,0]
 r=random.uniform(.0025,.0065)
 ell('Chip_toasted_freckle',pos,(r,.0018,r*.8),'Leather' if i%3==0 else 'ToastMarks',nu=8,nv=6)
for i in range(32):
 a=random.random()*math.tau;t=random.uniform(.15,.90);pos=brim(a,t)+[0,.003,0]
 r=random.uniform(.006,.012)
 ell('Chip_blister',pos,(r,.004,r*.8),'Bread',nu=10,nv=6)
for i in range(70):
 a=random.random()*math.tau;y=random.uniform(2.005,2.126)
 # Interpolate the actual crown profile so marks sit ON its outside.
 for j in range(len(rings)-1):
  y0,rx0,rz0=rings[j];y1,rx1,rz1=rings[j+1]
  if y0<=y<=y1:
   t=(y-y0)/(y1-y0);rx=rx0+(rx1-rx0)*t;rz=rz0+(rz1-rz0)*t;break
 r=random.uniform(.002,.0045)
 ell('Crown_fried_spot',((rx+.001)*math.cos(a),y,(rz+.001)*math.sin(a)),(r,r*1.3,r),'Leather',nu=8,nv=6)
# Lengthen the original torso and arms while retaining the original costume.
hand_names={'Palm','Finger','Thumb'}
body_names={'Neck','Shirt','Collar','Sleeve','Sleeve_cuff','Bandana_knot','Bandana_neck','Bandana_left','Bandana_right','Shirt_placket','Shirt_button','Shirt_pocket','Comics_patch'}
for part in parts:
 v=part['v'];name=part['name']
 if name in hand_names:v[:,1]-=.087
 elif name=='Arm':v[:,1]=.94+(v[:,1]-1.027)*(1.3602-.94)/(1.30-1.027)
 elif name in body_names or name.startswith('Label_'):v[:,1]=1.128+(v[:,1]-1.128)*1.35
 elif v[:,1].mean()>1.52:
  v[:,1]+=.14
  if name in {'Toast_crust','Soft_bread_face','Soft_bread_back','Eye_ink','Eye_white','Pupil','Eye_glint','Cheek','Eyebrow','Smile','Bread_pore'}:v[:,0]*=1.045
 # V3: narrow torso and keep sleeve-to-arm alignment.
 if name in body_names-{'Neck'} or name.startswith('Label_'):v[:,0]*=.78
 elif name in hand_names or name=='Arm':v[:,0]-=np.sign(v[:,0].mean())*.0352
 elif name in {'Waist','Belt','Apron','Apron_pocket','Apron_stitch','Towel','Towel_check'}:v[:,0]*=.90
 # Recompute smooth normals after the nonuniform geometry edits.
 f=part['f'];cross=np.cross(v[f[:,1]]-v[f[:,0]],v[f[:,2]]-v[f[:,0]])
 n=np.zeros_like(v)
 for j in range(3):np.add.at(n,f[:,j],cross)
 n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12);part['n']=n

# write OBJ, MTL and GLB, merged per material (15 draw slots)
with open(D/'Unity/ToastRanger.mtl','w') as o:
 for k,col in C.items():o.write(f'newmtl {k}\nKd '+ ' '.join(map(str,col))+'\nKa 0 0 0\nKs 0 0 0\nd 1\nillum 1\n\n')
with open(D/'Unity/ToastRanger.obj','w') as o:
 o.write('mtllib ToastRanger.mtl\no ToastRanger\ns 1\n');off=1
 for p in parts:
  o.write('g '+p['name']+'\nusemtl '+p['mat']+'\n')
  for v in p['v']:o.write('v '+' '.join(f'{x:.6f}' for x in v)+'\n')
  for uv in p['uv']:o.write('vt '+' '.join(f'{x:.6f}' for x in uv)+'\n')
  for n in p['n']:o.write('vn '+' '.join(f'{x:.6f}' for x in n)+'\n')
  for f in p['f']:o.write('f '+' '.join(f'{x+off}/{x+off}/{x+off}' for x in f)+'\n')
  off+=len(p['v'])
blob=bytearray();g={'asset':{'version':'2.0','generator':'ToastRanger procedural geometry'},'scene':0,'scenes':[{'nodes':[0]}],'nodes':[{'name':'ToastRanger','mesh':0}],'meshes':[{'primitives':[]}],'materials':[],'buffers':[{}],'bufferViews':[],'accessors':[]}
def acc(a,typ,ctype):
 global blob
 a=np.ascontiguousarray(a,dtype='<f4' if ctype==5126 else '<u4');start=len(blob);blob+=a.tobytes()
 g['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':a.nbytes});entry={'bufferView':len(g['bufferViews'])-1,'componentType':ctype,'count':len(a),'type':typ}
 if typ=='VEC3':entry.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
 g['accessors'].append(entry);return len(g['accessors'])-1
for k,col in C.items():
 ps=[p for p in parts if p['mat']==k]
 g['materials'].append({'name':k,'pbrMetallicRoughness':{'baseColorFactor':[*col.tolist(),1],'metallicFactor':0,'roughnessFactor':1}})
 if not ps:continue
 vs=[];ns=[];uvs=[];fs=[];n=0
 for p in ps:vs.append(p['v']);ns.append(p['n']);uvs.append(p['uv']);fs.append(p['f']+n);n+=len(p['v'])
 g['meshes'][0]['primitives'].append({'attributes':{'POSITION':acc(np.concatenate(vs),'VEC3',5126),'NORMAL':acc(np.concatenate(ns),'VEC3',5126),'TEXCOORD_0':acc(np.concatenate(uvs),'VEC2',5126)},'indices':acc(np.concatenate(fs).flatten(),'SCALAR',5125),'material':len(g['materials'])-1})
g['buffers'][0]['byteLength']=len(blob);jb=json.dumps(g,separators=(',',':')).encode();jb+=b' '*((-len(jb))%4)
with open(D/'ToastRanger.glb','wb') as o:o.write(struct.pack('<III',0x46546c67,2,12+8+len(jb)+8+len(blob))+struct.pack('<II',len(jb),0x4e4f534a)+jb+struct.pack('<II',len(blob),0x004e4942)+blob)
np.savez_compressed(D/'geometry_preview.npz',parts=np.array(parts,dtype=object))
stats={'vertices':sum(len(p['v']) for p in parts),'triangles':sum(len(p['f']) for p in parts),'materials':len(C),'height_m':float(max(p['v'][:,1].max() for p in parts)),'rigged':False,'uv':'Per-part overlapping procedural UVs; no unique texture atlas','unity_tested':False}
(D/'model_stats.json').write_text(json.dumps(stats,indent=2));print(stats)
