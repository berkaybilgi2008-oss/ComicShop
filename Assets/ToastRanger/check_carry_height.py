"""Numerical reach/length checks for the two-bone height correction."""
import json,pathlib,numpy as np
D=pathlib.Path(__file__).parent
data=json.loads((D/'Unity/ToastRangerRig.json').read_text())
bones=data['bones'];indices={b['name']:i for i,b in enumerate(bones)}
def matrix(q,t):
 x,y,z,w=q;m=np.eye(4)
 m[:3,:3]=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
 m[:3,3]=t;return m
samples=0;max_error=0
for clip in data['clips']:
 if not clip['name'].endswith('Book'):continue
 for frame in range(len(clip['times'])):
  world=[]
  for i,b in enumerate(bones):
   tr=clip['tracks'][i];m=matrix(tr['rotations'][frame*4:frame*4+4],tr['positions'][frame*3:frame*3+3])
   if b['parent']>=0:m=world[b['parent']]@m
   world.append(m)
  a,b,c=[world[indices[name]][:3,3] for name in ['RightUpperArm','RightForearm','RightHand']]
  l1=np.linalg.norm(b-a);l2=np.linalg.norm(c-b)
  for height in np.linspace(-.1,.16,14):
   target=c+[0,height,0];direction=target-a;distance=np.linalg.norm(direction);direction/=distance
   reach=np.clip(distance,abs(l1-l2)+.0001,l1+l2-.0001);target=a+direction*reach
   bend=b-a-direction*np.dot(b-a,direction);bend/=np.linalg.norm(bend)
   along=(l1*l1+reach*reach-l2*l2)/(2*reach);across=np.sqrt(max(0,l1*l1-along*along))
   elbow=a+direction*along+bend*across
   error=max(abs(np.linalg.norm(elbow-a)-l1),abs(np.linalg.norm(target-elbow)-l2))
   assert np.isfinite(elbow).all() and error<1e-10
   max_error=max(max_error,float(error));samples+=1
result={'height_samples':samples,'bone_length_max_error':max_error,'reach_and_finite_targets':'PASS','unity_execution':False}
(D/'CarryHeightValidation.json').write_text(json.dumps(result,indent=2));print(result)
