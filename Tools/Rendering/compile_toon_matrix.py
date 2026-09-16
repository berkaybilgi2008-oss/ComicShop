from pathlib import Path
import subprocess,json,itertools,concurrent.futures,argparse,tempfile
parser=argparse.ArgumentParser(description="Compile representative ToonLit HLSL variants against Unity Graphics 6000.5 sources.")
parser.add_argument("--unity-source",required=True,help="Root containing the Unity Graphics Packages directory")
parser.add_argument("--dxc",required=True,help="DXC executable")
parser.add_argument("--report",default="toon-compile-results.json")
args=parser.parse_args()
root=Path(args.unity_source).resolve()
shader=Path(__file__).resolve().parents[2]/'Assets/ComicShopToon/Shaders'
dxc=args.dxc
workspace=Path(tempfile.mkdtemp(prefix='toon-compile-'))
meta=(shader/'ToonLit.shader').read_text().split('Name "Meta"')[1].split('HLSLPROGRAM')[1].split('ENDHLSL')[0];(workspace/'ToonMeta.hlsl').write_text(meta)
cases=[]
lights=[[],['_MAIN_LIGHT_SHADOWS'],['_MAIN_LIGHT_SHADOWS_CASCADE','_ADDITIONAL_LIGHTS','_ADDITIONAL_LIGHT_SHADOWS','_SHADOWS_SOFT'],['_MAIN_LIGHT_SHADOWS_SCREEN','_CLUSTER_LIGHT_LOOP','_ADDITIONAL_LIGHT_SHADOWS','_SHADOWS_SOFT_HIGH']]
gis=[[],['LIGHTMAP_ON'],['LIGHTMAP_ON','DIRLIGHTMAP_COMBINED','SHADOWS_SHADOWMASK','LIGHTMAP_SHADOW_MIXING'],['PROBE_VOLUMES_L1'],['PROBE_VOLUMES_L2'],['DYNAMICLIGHTMAP_ON'],['LIGHTMAP_ON','DYNAMICLIGHTMAP_ON','USE_LEGACY_LIGHTMAPS']]
features=[[],['_NORMALMAP','_TOON_HALFTONE','_TOON_GLOBAL_HALFTONE','_TOON_GLOBAL_SPECULAR','_TOON_GLOBAL_RIM','INSTANCING_ON','FOG_LINEAR'],['_NORMALMAP','_TOON_LOCAL_STYLE','_TOON_HALFTONE','FOG_EXP2']]
for a,b,c in itertools.product(lights,gis,features):
 for entry,stage in [('ToonVertex','vs'),('ToonFragment','ps')]:cases.append((shader/'ToonForward.hlsl',entry,stage,a+b+c))
for normal,instance in itertools.product([[],['_NORMALMAP']],[[],['INSTANCING_ON']]):
 for entry,stage,extra in [('AuxVertex','vs',[]),('ShadowVertex','vs',[]),('ShadowVertex','vs',['_CASTING_PUNCTUAL_LIGHT_SHADOW']),('DepthFragment','ps',[]),('NormalsFragment','ps',[]),('NormalsFragment','ps',['_GBUFFER_NORMALS_OCT']),('MaskFragment','ps',[])]:cases.append((shader/'ToonAuxiliary.hlsl',entry,stage,normal+instance+extra))
for entry,stage in [('ToonMetaVertex','vs'),('ToonMetaFragment','ps')]:cases.append((workspace/'ToonMeta.hlsl',entry,stage,[]))
def run(case):
 f,entry,stage,defines=case
 args=[dxc,'-flegacy-macro-expansion','-HV','2018','-I',str(root),'-I',str(shader),'-T',stage+'_6_0','-E',entry,'-Fo','/dev/null']
 for v in ['SHADER_API_D3D11=1','UNITY_COMPILER_HLSL=1','UNITY_VERSION=600050','SHADER_TARGET=45','SHADER_STAGE_'+('VERTEX' if stage=='vs' else 'FRAGMENT')+'=1']+defines:args+=['-D',v]
 r=subprocess.run(args+[str(f)],capture_output=True,text=True)
 return {'entry':entry,'defines':defines,'exit':r.returncode,'messages':r.stdout+r.stderr}
with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:results=list(pool.map(run,cases))
fails=[x for x in results if x['exit']];Path(args.report).write_text(json.dumps(results,indent=2));print('Compiled',len(results),'Failed',len(fails));
for f in fails[:4]: print(json.dumps(f,indent=2))

raise SystemExit(1 if fails else 0)
