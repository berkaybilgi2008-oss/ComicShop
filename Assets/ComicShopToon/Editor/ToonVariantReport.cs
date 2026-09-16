using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ComicShop.Rendering.Editor
{
    // Counts actual build candidates after earlier preprocessors, rather than claiming
    // that the raw Cartesian pragma product equals the platform's compiled output.
    public sealed class ToonVariantReport : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        static readonly Dictionary<string, long> Counts = new Dictionary<string, long>();
        public int callbackOrder => int.MaxValue;
        public void OnPreprocessBuild(BuildReport report) => Counts.Clear();
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name != "ComicShop/ToonLit") return;
            string key = snippet.passName + "/" + snippet.shaderType;
            Counts.TryGetValue(key, out long count);
            Counts[key] = count + data.Count;
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            long total = 0;
            foreach (var item in Counts) { Debug.Log($"ToonLit build candidates {item.Key}: {item.Value}"); total += item.Value; }
            Debug.Log($"ToonLit total build shader-stage candidates after preprocessing: {total}. Binary deduplication can reduce the final count.");
        }
    }
}
