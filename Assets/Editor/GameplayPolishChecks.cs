#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// No live scene edits, no preference writes, no Relay connection.
public static class GameplayPolishChecks
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("[POLISH FAIL] " + name);
        checks++;
    }
    [MenuItem("ComicShop/Tests/Run Gameplay Polish Checks (Edit Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
        checks = 0;
        var previous = SceneManager.GetActiveScene();
        var random = UnityEngine.Random.state;
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        Material material = null;
        try
        {
            var budget = new RecoveryBudget();
            for (int episode = 0; episode < 6; episode++)
            {
                Check(budget.Attempt(episode * 100, 3, 30), "separate loss can retry " + episode);
                budget.ObserveSafe(); budget.ObserveSafe();
                Check(budget.Attempts == 0 && budget.RetryAt == 0, "safe book releases retry budget " + episode);
            }
            Check(budget.Attempt(700, 3, 30) && budget.Attempt(701, 3, 30) && budget.Attempt(702, 3, 30), "three consecutive attempts");
            Check(!budget.Attempt(703, 3, 30), "backoff prevents retry loop");
            Check(budget.Attempt(732, 3, 30), "backoff expires instead of permanently abandoning book");

            var group = new GameObject("Book Spawn Corridors");
            var zones = new BoxCollider[3];
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = new GameObject(i == 2 ? "Third Corridor" : "Corridor " + i);
                zone.transform.SetParent(group.transform, false);
                zone.transform.position = new Vector3(i * 10, 0, 0);
                zones[i] = zone.AddComponent<BoxCollider>(); zones[i].size = new Vector3(3, .1f, 6); zones[i].enabled = false;
            }
            var spawner = new GameObject("QA Spawner").AddComponent<BookSpawner>();
            spawner.corridorAreas = new[] { zones[0], zones[1] };
            Check(spawner.DiscoverCorridors() == 1 && spawner.corridorAreas.Length == 3, "unassigned Third Corridor discovered");
            Check(spawner.DiscoverCorridors() == 0, "discovery is idempotent");
            Check(spawner.ValidateSpawnAreas(out _), "disabled area colliders are valid");
            for (int seed = 0; seed < 20; seed++)
            {
                UnityEngine.Random.InitState(seed);
                var positions = spawner.CreateSpawnPositions(150);
                for (int i = 0; i < 3; i++)
                {
                    var local = zones[i].transform.InverseTransformPoint(positions[i] - Vector3.up * spawner.spawnHeight);
                    Check(Mathf.Abs(local.x) <= 1.15f && Mathf.Abs(local.z) <= 2.65f, "guaranteed in-bounds spawn " + seed + "/" + i);
                }
            }
            zones[2].gameObject.SetActive(false);
            Check(!spawner.ValidateSpawnAreas(out string reason) && reason.Contains("Third Corridor"), "inactive area explains failure");
            zones[2].gameObject.SetActive(true); zones[2].size = new Vector3(.5f, .1f, 6);
            Check(!spawner.ValidateSpawnAreas(out _), "padding consumes narrow area");
            zones[2].size = new Vector3(3, .1f, 6); zones[2].transform.localScale = Vector3.zero;
            Check(!spawner.ValidateSpawnAreas(out _), "zero scale rejected");
            zones[2].transform.localScale = Vector3.one;
            spawner.corridorAreas[2] = null;
            Check(!spawner.ValidateSpawnAreas(out reason) && reason.Contains("[2]"), "missing reference reports index");
            spawner.corridorAreas[2] = zones[0];
            Check(!spawner.ValidateSpawnAreas(out _), "duplicate area rejected");

            var preferences = new ShopSettings.Preferences { sensitivity = float.NaN, fov = 999, master = -1, keys = new[] { 0 } };
            ShopSettings.Validate(preferences);
            Check(preferences.sensitivity == 2.2f && preferences.fov == 105 && preferences.master == 0, "preference bounds and NaN recovery");
            Check(preferences.keys.Length == 10 && preferences.keys[6] == (int)KeyCode.Mouse0, "corrupt key bindings recover");
            Check(!ShopRound.ShouldComplete(0, 0) && !ShopRound.ShouldComplete(150, 149) && ShopRound.ShouldComplete(150, 150), "completion boundary");

            var shader = Shader.Find("ComicShop/ToonLit");
            Check(shader != null, "ToonLit imported");
            material = new Material(shader);
            Check(material.FindPass("ToonMask") >= 0 && material.HasProperty("_OutlineEnabled"), "outline opt-out pass contract");
            Check(material.FindPass("ShadowCaster") >= 0 && material.FindPass("DepthNormals") >= 0, "current regression shader contract");
            Debug.Log($"[POLISH PASS] {checks} assertions. Render output, audio, build compilation and WAN co-op still need Player tests.");
        }
        finally
        {
            if (material != null) Object.DestroyImmediate(material);
            UnityEngine.Random.state = random;
            EditorSceneManager.CloseScene(testScene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
#endif
