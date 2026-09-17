using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ComicShop.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ToonCameraSetup : MonoBehaviour
    {
        float nextCheck;
        void OnEnable() => ConfigureExisting();
        void Update()
        {
            // Also catches a network player camera spawned after scene loading.
            if (Time.realtimeSinceStartup < nextCheck) return;
            nextCheck = Time.realtimeSinceStartup + 1f;
            ConfigureExisting();
        }
        public void ConfigureExisting()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != gameObject.scene || camera.cameraType != CameraType.Game || camera.targetTexture != null) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data.renderType != CameraRenderType.Base) continue;
                camera.allowHDR = true; camera.allowMSAA = false; camera.useOcclusionCulling = true;
                data.renderPostProcessing = true; data.renderShadows = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.Medium;
                data.volumeLayerMask |= 1; // Generated global Volume is on Default.
            }
        }
    }
}
