using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
namespace ComicShop {
// Explicit local-player binding. Does not guess fields on existing controllers.
public sealed class ComicGameplaySettings : MonoBehaviour {
    [Tooltip("Assign only the local player's perspective camera; leave empty when your controller manages FOV.")]
    public Camera playerCamera;
    public AudioMixer mixer;
    public string effectsParameter="SfxVolume";
    [Tooltip("Connect to the player controller's sensitivity setter. Value is a multiplier (0.2 to 3).")]
    public UnityEvent<float> sensitivityChanged = new UnityEvent<float>();
    public UnityEvent<float> fovChanged = new UnityEvent<float>();
    void OnEnable() { ComicPreferences.Changed+=Apply; Apply(); }
    void OnDisable() { ComicPreferences.Changed-=Apply; }
    public void Apply() {
        if(playerCamera && !playerCamera.orthographic) playerCamera.fieldOfView=ComicPreferences.Fov;
        sensitivityChanged.Invoke(ComicPreferences.Sensitivity); fovChanged.Invoke(ComicPreferences.Fov);
        if(mixer && !string.IsNullOrEmpty(effectsParameter)) mixer.SetFloat(effectsParameter, Mathf.Log10(Mathf.Max(.0001f,ComicPreferences.Sfx))*20f);
    }
}}
