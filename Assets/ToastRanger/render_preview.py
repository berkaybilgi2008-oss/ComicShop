import numpy as np, math,pathlib
from PIL import Image, ImageDraw,ImageFont,ImageFilter
try:
 import fast_preview
except (ImportError,OSError):
 fast_preview=None
D=pathlib.Path(__file__).parent
parts=np.load(D/'geometry_preview.npz',allow_pickle=True)['parts']
P={'Crust':'B87532','Bread':'F3CE8C','ToastMarks':'CC9650','Olive':'737650','OliveDark':'454B36','Red':'C7442D','Sauce':'AA291D','Cheese':'FFD074','Leather':'65452F','Pants':'3F3631','Sole':'B29A6B','Gold':'D7A14E','Ink':'211C19','White':'FFF4D8','Blush':'F0CCA0'}
def render(angle,w,h,scale,center):
 if fast_preview is not None:return fast_preview.render(parts,P,angle,w,h,scale,center)
 a=math.radians(angle);right=np.array([math.cos(a),0,-math.sin(a)]);forward=np.array([math.sin(a),.07,math.cos(a)]);forward/=np.linalg.norm(forward);up=np.cross(forward,right);M=np.array([right,up,forward]);zbuf=np.full((h,w),-1e9);rgb=np.zeros((h,w,3),dtype=np.uint8);rgb[:]=[28,33,32];ids=np.full((h,w),-1)
 light=np.array([-.45,.75,.65]);light/=np.linalg.norm(light)
 for idx,p in enumerate(parts):
  v=p['v']@M.T;v[:,0]=v[:,0]*scale+w/2;v[:,1]=h/2-(v[:,1]-center)*scale
  ns=p['n'];col=np.array([int(P[p['mat']][i:i+2],16) for i in (0,2,4)])
  for f in p['f']:
   tri=v[f];xmin=max(0,int(np.floor(tri[:,0].min())));xmax=min(w-1,int(np.ceil(tri[:,0].max())));ymin=max(0,int(np.floor(tri[:,1].min())));ymax=min(h-1,int(np.ceil(tri[:,1].max())))
   if xmin>xmax or ymin>ymax:continue
   x,y=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
   t0,t1,t2=tri;den=(t1[1]-t2[1])*(t0[0]-t2[0])+(t2[0]-t1[0])*(t0[1]-t2[1])
   if abs(den)<1e-8:continue
   b0=((t1[1]-t2[1])*(x-t2[0])+(t2[0]-t1[0])*(y-t2[1]))/den;b1=((t2[1]-t0[1])*(x-t2[0])+(t0[0]-t2[0])*(y-t2[1]))/den;b2=1-b0-b1
   z=b0*t0[2]+b1*t1[2]+b2*t2[2];sub=zbuf[ymin:ymax+1,xmin:xmax+1];mask=(b0>=0)&(b1>=0)&(b2>=0)&(z>sub)
   if not mask.any():continue
   n=b0[...,None]*ns[f[0]]+b1[...,None]*ns[f[1]]+b2[...,None]*ns[f[2]];n/=np.maximum(np.linalg.norm(n,axis=-1,keepdims=True),1e-10)
   diffuse=n@light;shade=np.where(diffuse>.6,1.05,np.where(diffuse>.05,.81,.46));shaded=np.clip(col*shade[...,None],0,255)
   sub[mask]=z[mask];rgb[ymin:ymax+1,xmin:xmax+1][mask]=shaded[mask];ids[ymin:ymax+1,xmin:xmax+1][mask]=idx
 mask=ids>=0
 # silhouette plus discontinuity ink in software preview
 edge=np.zeros_like(mask)
 for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
  zn=np.roll(zbuf,(dy,dx),(0,1));mn=np.roll(mask,(dy,dx),(0,1));edge|=mask&((~mn)|((abs(zbuf-zn)>.035)&mn))
 rgb[edge]=[25,21,18]
 img=Image.fromarray(rgb);return img
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
def ft(n):return ImageFont.truetype(font,n)
if __name__=='__main__':
 canvas=Image.new('RGB',(1500,1400),(28,33,32));dr=ImageDraw.Draw(canvas)
 dr.text((65,38),'TOAST RANGER',font=ft(40),fill='#F3CE8C');dr.text((68,94),'CEL-SHADED  /  V4 / CLEAN FACE / IDLE + WALK + RUN',font=ft(17),fill='#B7BAA5')
 for angle,x,label in [(0,20,'FRONT'),(33,480,'THREE-QUARTER'),(180,950,'BACK')]:
  im=render(angle,460,1040,422,1.17);canvas.paste(im,(x,155));dr.text((x+25,125),label,font=ft(14),fill='#B7BAA5')
 dr.line((65,1220,1435,1220),fill='#555B4D',width=1)
 for i,(k,c) in enumerate(P.items()):dr.rounded_rectangle((65+i*88,1250,125+i*88,1280),6,fill='#'+c)
 dr.text((65,1310),'Actual mesh preview | Skeletal animation included | Unity validation pending',font=ft(17),fill='#B7BAA5')
 canvas.save(D/'Preview.png');print('preview saved')
