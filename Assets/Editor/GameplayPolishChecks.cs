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

    [MenuItem("ComicShop/Tests/Check Natural Pile Walkways (Edit Mode)")]
    public static void RunPileWalkwayChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
        checks = 0;
        var previous = SceneManager.GetActiveScene();
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        try
        {
            var spawner = new GameObject("Pile walkway regression").AddComponent<BookSpawner>();
            spawner.gatherInFrontOfShelves = true;
            // Three shelf rows create two middle walking lanes at x = -6 and +6.
            for (int row = -1; row <= 1; row++)
            {
                var shelf = new GameObject("Fixture shelf " + row);
                shelf.transform.position = new Vector3(row * 12f, 1f, 0f);
                shelf.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2f, 30f);
                shelf.AddComponent<ShelfSlot>();
            }
            var zone = new GameObject("Assigned spawn box includes both walkways").AddComponent<BoxCollider>();
            zone.size = new Vector3(40f, 0.1f, 40f);
            zone.enabled = false;
            spawner.corridorAreas = new[] { zone };
            Physics.SyncTransforms();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BookSpawner).GetMethod("BuildShelfFootprints", flags).Invoke(spawner, null);
            var allowed = typeof(BookSpawner).GetMethod("IsNaturalHeapFootprintAllowed", flags);
            bool Fits(float x, float z, float yaw, Vector2 half) =>
                (bool)allowed.Invoke(spawner, new object[] { new Vector3(x, 0f, z), half, yaw });
            var cover = new Vector2(0.4f, 0.25f);
            foreach (float yaw in new[] { 0f, 37f, 90f, 173f })
            {
                Check(Fits(-3f, 0f, yaw, cover) && Fits(3f, 0f, yaw, cover), "shelf-front piles still allowed " + yaw);
                Check(!Fits(-6f, 0f, yaw, cover), "left middle walkway empty with assigned box " + yaw);
                Check(!Fits(6f, 0f, yaw, cover), "right middle walkway empty with assigned box " + yaw);
                Check(!Fits(3f, 15.5f, yaw, cover), "end crossing empty " + yaw);
                Check(!Fits(4.7f, 0f, yaw, cover), "overhanging cover cannot reach a walkway " + yaw);
                Check(!Fits(6f, 0f, yaw, new Vector2(3f, 0.25f)), "wide book cannot bridge the walkway " + yaw);
            }
            // The fix must not make assigned bounds optional either.
            spawner.gatherInFrontOfShelves = false;
            Check(!Fits(19.5f, 0f, 0f, cover), "area edge padding still enforced");
            spawner.gatherInFrontOfShelves = true;
            spawner.corridorAreas = Array.Empty<BoxCollider>();
            spawner.areaSize = new Vector2(40f, 40f);
            Check(!Fits(-6f, 0f, 0f, cover) && !Fits(6f, 0f, 0f, cover), "legacy area also preserves both lanes");
            Debug.Log($"[PILE WALKWAY PASS] {checks} checks. Full scene layout, visuals and multiplayer require Play Mode.");
        }
        finally
        {
            EditorSceneManager.CloseScene(testScene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
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
