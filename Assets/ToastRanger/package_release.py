"""Package user-facing sources, meshes, animations and previews; omit local intermediates."""
import ast,json,pathlib,zipfile
from PIL import Image
D=pathlib.Path(__file__).parent
for file in D.glob('*.py'):ast.parse(file.read_text())
data=json.loads((D/'Unity/ToastRangerRig.json').read_text())
assert data['version']==4 and {c['name'] for c in data['clips']}=={'Idle','Walk','Run','IdleBook','WalkBook','RunBook'}
for clip in data['clips']:
 for track in clip['tracks']:
  assert len(track['positions'])==3*len(clip['times'])
  assert len(track['rotations'])==4*len(clip['times'])
  assert 0<=track['bone']<len(data['bones'])
assert len(data['materials'])==len(data['submeshes'])
for name in ['Walk.gif','Run.gif','WalkBook.gif','RunBook.gif']:
 with Image.open(D/name) as image:assert image.n_frames==16
for file in ['ToastRanger_Animated.glb','README_TR.md','Validation.json','Unity/Editor/ToastAnimatedSetup.cs','Unity/ToastLocomotion.cs','Unity/ToastBookCarry.cs','CarryHeightValidation.json']:
 assert (D/file).stat().st_size>0
with zipfile.ZipFile(D.parent/'ToastRanger_3D_Package.zip','w',zipfile.ZIP_DEFLATED) as archive:
 for file in sorted(D.rglob('*')):
  if not file.is_file() or '__pycache__' in file.parts or file.suffix in {'.npz','.so','.pyc'}:continue
  if file.stem.startswith(('Walk_','Run_','WalkBook_','RunBook_')):continue
  archive.write(file,'ToastRanger/'+str(file.relative_to(D)))
print('V4 packaged:',(D.parent/'ToastRanger_3D_Package.zip').stat().st_size,'bytes')
