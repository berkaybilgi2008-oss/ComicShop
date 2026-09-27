using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tek tikla Windows masaustu surumu: ComicShop > Build > Windows Masaustu (.exe)
/// Cikti: Masaustu\ComicShop Oyun\ComicShop.exe ve masaustune "ComicShop" kisayolu.
/// Build Settings'teki etkin sahneler kullanilir (su an Assets/Settings/ne.unity).
/// </summary>
public static class ComicShopDesktopBuild
{
    const string FolderName = "ComicShop Oyun";

    [MenuItem("ComicShop/Build/Windows Masaustu (.exe)", priority = 1)]
    public static void BuildWindowsDesktop()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("ComicShop Build", "Once Play modundan cik.", "Tamam");
            return;
        }
        // Kaydedilmemis sahne degisiklikleri (orn. yeni yerlestirilen raf logosu) build'e girsin.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path))
            .Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("ComicShop Build", "Build Settings'te etkin sahne yok.", "Tamam");
            return;
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string folder = Path.Combine(desktop, FolderName);
        string productName = string.IsNullOrWhiteSpace(PlayerSettings.productName) ? "ComicShop" : PlayerSettings.productName;
        string exe = Path.Combine(folder, productName + ".exe");
        Directory.CreateDirectory(folder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        Debug.Log($"[ComicShop Build] Basliyor: {exe}  ({string.Join(", ", scenes)})");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[ComicShop Build] BASARISIZ: {summary.result}, {summary.totalErrors} hata. Console'a bak.");
            EditorUtility.DisplayDialog("ComicShop Build", $"Build basarisiz ({summary.result}). Hatalar Console'da.", "Tamam");
            return;
        }

        string shortcut = Path.Combine(desktop, productName + ".lnk");
        bool linked = CreateShortcut(shortcut, exe, folder);
        Debug.Log($"[ComicShop Build] TAMAM: {exe}  ({summary.totalSize / (1024f * 1024f):0} MB, {summary.totalTime.TotalMinutes:0.0} dk)" +
                  (linked ? $"\nMasaustu kisayolu: {shortcut}" : ""));
        EditorUtility.RevealInFinder(exe);
    }

    // Windows Script Host ile masaustu kisayolu; basarisiz olursa build yine gecerlidir.
    static bool CreateShortcut(string shortcut, string target, string workingDirectory)
    {
        if (Application.platform != RuntimePlatform.WindowsEditor) return false;
        try
        {
            string Escape(string value) => value.Replace("'", "''");
            string script =
                "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + Escape(shortcut) + "');" +
                "$s.TargetPath='" + Escape(target) + "';" +
                "$s.WorkingDirectory='" + Escape(workingDirectory) + "';" +
                "$s.IconLocation='" + Escape(target) + ",0';" +
                "$s.Save()";
            var info = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using (var process = Process.Start(info))
            {
                if (process == null) return false;
                process.WaitForExit(15000);
                return process.HasExited && process.ExitCode == 0 && File.Exists(shortcut);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ComicShop Build] Masaustu kisayolu olusturulamadi: " + exception.Message);
            return false;
        }
    }
}
