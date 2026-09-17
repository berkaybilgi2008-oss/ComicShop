from pathlib import Path
import subprocess, argparse, tempfile
parser=argparse.ArgumentParser(description="Check the toon emission HLSL against Unity Graphics sources with DXC")
parser.add_argument("--unity-source",required=True)
parser.add_argument("--dxc",required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
p=root/'Assets/ComicShopToon/Shaders/ToonEmission.shader';s=p.read_text().split('HLSLINCLUDE')[1].split('ENDHLSL')[0];path=Path(tempfile.mkdtemp(prefix='toon-emission-'))/'emission.hlsl';path.write_text(s)
count=0
for entry,stage in [('EmissionVertex','vs'),('EmissionFragment','ps'),('DepthFragment','ps'),('NormalsFragment','ps')]:
 for variant in [[],['INSTANCING_ON','FOG_LINEAR','_GBUFFER_NORMALS_OCT']]:
  command=[args.dxc,'-flegacy-macro-expansion','-HV','2018','-I',args.unity_source,'-T',stage+'_6_0','-E',entry,'-Fo','/dev/null']
  for d in ['SHADER_API_D3D11=1','UNITY_COMPILER_HLSL=1','UNITY_VERSION=600050','SHADER_TARGET=45','SHADER_STAGE_'+('VERTEX' if stage=='vs' else 'FRAGMENT')+'=1']+variant:command+=['-D',d]
  r=subprocess.run(command+[str(path)],capture_output=True,text=True)
  if r.returncode:raise RuntimeError(r.stdout+r.stderr)
  count+=1
print('Emission HLSL passed',count)
