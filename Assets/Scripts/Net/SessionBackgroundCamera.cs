using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps a deterministic image behind the connection menu when no player exists.
// It immediately yields to the owner's gameplay camera, so it can never paint
// over the room after a host/client connection succeeds.
public sealed class SessionBackgroundCamera : MonoBehaviour
{
    private Camera backgroundCamera;

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
        backgroundCamera = gameObject.AddComponent<Camera>();
        backgroundCamera.depth = -1000;
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = new Color(0.045f, 0.055f, 0.075f, 1f);
        backgroundCamera.cullingMask = 0;
        backgroundCamera.targetTexture = null;
        backgroundCamera.rect = new Rect(0, 0, 1, 1);
        backgroundCamera.allowHDR = false;
        backgroundCamera.allowMSAA = false;
        backgroundCamera.useOcclusionCulling = false;
        // No MainCamera tag or AudioListener: the local player owns those.
    }

    private void LateUpdate()
    {
        var localPlayer = NetworkPlayerSetup.LocalPlayer;
        bool localGameplayCameraIsReady = localPlayer != null &&
            localPlayer.playerCamera != null && localPlayer.playerCamera.enabled;
        backgroundCamera.enabled = !localGameplayCameraIsReady;
    }
}