using UnityEngine;
using UnityEngine.SceneManagement;

// IMGUI draws the room menu even without a player camera. Clear the display
// first so an empty session cannot retain desktop, cursor or overlay pixels.
public sealed class SessionBackgroundCamera : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var manager in Object.FindObjectsByType<ConnectionManager>(FindObjectsSortMode.None))
        {
            if (manager.GetComponentInChildren<SessionBackgroundCamera>(true) != null) continue;
            var background = new GameObject("Session Background Camera");
            background.transform.SetParent(manager.transform, false);
            background.AddComponent<SessionBackgroundCamera>();
        }
    }

    private void Awake()
    {
        var camera = gameObject.AddComponent<Camera>();
        camera.depth = -1000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f, 1f);
        camera.cullingMask = 0;
        camera.targetTexture = null;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        // No MainCamera tag or AudioListener: the local player owns those.
        // Keep this first clear pass through connecting, playing and shutdown.
    }
}
