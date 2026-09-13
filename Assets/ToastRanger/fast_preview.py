import ctypes,pathlib,math
import numpy as np
from PIL import Image
lib=ctypes.CDLL(str(pathlib.Path(__file__).with_name('fast_raster.so')))
ptr=ctypes.c_void_p
lib.raster.argtypes=[ptr,ptr,ptr,ctypes.c_int,ptr,ctypes.c_int,ctypes.c_int,ptr,ptr]
def render(parts,palette,angle,w,h,scale,center):
 a=math.radians(angle);right=np.array([math.cos(a),0,-math.sin(a)]);forward=np.array([math.sin(a),.07,math.cos(a)]);forward/=np.linalg.norm(forward);up=np.cross(forward,right);M=np.array([right,up,forward])
 depth=np.full((h,w),-1e9);rgb=np.zeros((h,w,3),np.uint8);rgb[:]=[28,33,32]
 for p in parts:
  v=p['v']@M.T;v[:,0]=v[:,0]*scale+w/2;v[:,1]=h/2-(v[:,1]-center)*scale
  v=np.ascontiguousarray(v);n=np.ascontiguousarray(p['n'],dtype=np.float64);f=np.ascontiguousarray(p['f'],dtype=np.int32)
  col=np.array([int(palette[p['mat']][i:i+2],16) for i in (0,2,4)],float)
  lib.raster(v.ctypes.data,n.ctypes.data,f.ctypes.data,len(f),col.ctypes.data,w,h,depth.ctypes.data,rgb.ctypes.data)
 mask=depth>-1e8;edge=np.zeros_like(mask)
 for dy,dx in [(1,0),(-1,0),(0,1),(0,-1)]:
  zn=np.roll(depth,(dy,dx),(0,1));mn=np.roll(mask,(dy,dx),(0,1));edge|=mask&((~mn)|((abs(depth-zn)>.035)&mn))
 rgb[edge]=[25,21,18];return Image.fromarray(rgb)
