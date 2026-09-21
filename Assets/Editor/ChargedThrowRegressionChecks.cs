#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

// Edit-mode checks in an isolated preview scene; no changes to the user's scene.
public static class ChargedThrowRegressionChecks
{
    [MenuItem("Tools/ComicShop/Validate Q aim and pile support")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var randomState = UnityEngine.Random.state;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            UnityEngine.Random.InitState(417);
            GameObject Make(string name)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }
            var spawn = Make("Spawner").AddComponent<BookSpawner>();
            var left = Make("Left").AddComponent<BoxCollider>();
            var right = Make("Third").AddComponent<BoxCollider>();
            left.size = right.size = new Vector3(10, 0.1f, 10);
            left.transform.position = new Vector3(-2.5f, 0, 0);
            right.transform.position = new Vector3(2.5f, 0, 0);
            left.enabled = right.enabled = false;
            spawn.corridorAreas = new[] { left, right };
            spawn.corridorEdgePadding = 0;
            int intersection = 0;
            const int samples = 30000;
            for (int i = 0; i < samples; i++)
            {
                Vector3 p = spawn.SampleSpawnPosition();
                Check(Mathf.Abs(p.x) <= 7.5f && Mathf.Abs(p.z) <= 5f, "Sample outside corridor union");
                if (Mathf.Abs(p.x) <= 2.5f) intersection++;
            }
            Check(Mathf.Abs(intersection / (float)samples - 1f / 3f) < 0.02f,
                "Intersection density should equal single-covered area density");

            var player = Make("Player").AddComponent<PlayerInteraction>();
            var camera = Make("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(10000, 2, 10000);
            player.playerCamera = camera;
            Check(Mathf.Abs(player.ChargedThrowSpeed(1f) * 3.6f - 207f) < 0.001f, "Full charge speed changed");
            var book = Make("Book").AddComponent<BookItem>();
            Vector3 origin = camera.transform.position + new Vector3(-0.65f, -0.1f, 0.6f);
            var method = typeof(PlayerInteraction).GetMethod("AimChargedThrow", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 velocity = (Vector3)method.Invoke(player, new object[] { book, origin, 57.5f });
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 target = ray.GetPoint(100);
            float t = (target.z - origin.z) / velocity.z;
            Check(Mathf.Abs((origin + velocity * t).x - target.x) < 0.001f, "Left-offset throw misses crosshair horizontally");

            var floor = Make("Floor").AddComponent<BoxCollider>();
            floor.size = new Vector3(5, 1, 5);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            var pileObject = Make("Supported book");
            var body = pileObject.AddComponent<Rigidbody>();
            var box = pileObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.4f, 0.02f, 0.6f);
            pileObject.transform.position = new Vector3(0, 0.012f, 0);
            var pileBook = pileObject.AddComponent<BookItem>();
            typeof(BookItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pileBook, null);
            Physics.SyncTransforms();
            Check(pileBook.InitializeSpawnSupport(floor) && body.isKinematic, "Supported pile did not stabilize");
            UnityEngine.Object.DestroyImmediate(floor);
            typeof(BookItem).GetField("nextSupportCheck", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pileBook, -1f);
            typeof(BookItem).GetMethod("CheckFrozenSupport", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pileBook, null);
            Check(!body.isKinematic, "Removing support must release the pile");
            Debug.Log("[Q CHECK PASS] 207 km/h; crosshair convergence; 30,000 overlap samples; support removal. Multiplayer/animation still require two-player Play Mode.");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
#endif
