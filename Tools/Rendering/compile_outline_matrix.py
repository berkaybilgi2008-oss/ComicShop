"""Compile Step 2 entry points against a Unity Graphics 6000.5 source checkout."""
from pathlib import Path
import argparse, concurrent.futures, json, os, re, subprocess, tempfile
parser = argparse.ArgumentParser()
parser.add_argument('--unity-source', required=True)
parser.add_argument('--dxc', required=True)
parser.add_argument('--report', default='outline-compile-results.json')
args = parser.parse_args()
root = Path(args.unity_source).resolve()
shaders = Path(__file__).resolve().parents[2] / 'Assets/ComicShopToon/Shaders'
workspace = Path(tempfile.mkdtemp(prefix='outline-check-'))
cases = []
for name in ['ToonOutline', 'ScreenSpaceOutline', 'LightBlocker']:
    blocks = re.findall(r'HLSLPROGRAM(.*?)ENDHLSL', (shaders / (name + '.shader')).read_text(), re.S)
    for index, block in enumerate(blocks):
        path = workspace / f'{name}-{index}.hlsl'
        path.write_text(block)
        vertex = re.search(r'#pragma vertex (\w+)', block)[1]
        fragment = re.search(r'#pragma fragment (\w+)', block)[1]
        variants = [[]]
        if 'multi_compile_instancing' in block:
            variants += [['INSTANCING_ON']]
        if '_CASTING_PUNCTUAL_LIGHT_SHADOW' in block:
            variants += [v + ['_CASTING_PUNCTUAL_LIGHT_SHADOW'] for v in list(variants)]
        if '_GBUFFER_NORMALS_OCT' in block:
            variants += [v + ['_GBUFFER_NORMALS_OCT'] for v in list(variants)]
        if 'multi_compile_fog' in block:
            variants += [v + ['FOG_LINEAR'] for v in list(variants)]
        for keywords in variants:
            for entry, stage in [(vertex, 'vs'), (fragment, 'ps')]:
                cases.append((path, entry, stage, keywords))

def compile_case(case):
    path, entry, stage, keywords = case
    command = [args.dxc, '-flegacy-macro-expansion', '-HV', '2018', '-I', str(root), '-I', str(shaders),
               '-T', stage + '_6_0', '-E', entry, '-Fo', os.devnull]
    defines = ['SHADER_API_D3D11=1', 'UNITY_COMPILER_HLSL=1', 'UNITY_VERSION=600050', 'SHADER_TARGET=45',
               'SHADER_STAGE_' + ('VERTEX' if stage == 'vs' else 'FRAGMENT') + '=1'] + keywords
    for define in defines:
        command += ['-D', define]
    result = subprocess.run(command + [str(path)], capture_output=True, text=True)
    return dict(shader=path.name, entry=entry, keywords=keywords, exit=result.returncode,
                messages=result.stdout + result.stderr)

with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
    results = list(pool.map(compile_case, cases))
Path(args.report).write_text(json.dumps(results, indent=2))
failures = [r for r in results if r['exit']]
print(f'Compiled {len(results)} entries; failed {len(failures)}')
for result in failures[:4]:
    print(json.dumps(result, indent=2))
raise SystemExit(bool(failures))
