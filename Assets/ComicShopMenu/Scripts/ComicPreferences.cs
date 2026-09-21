using System;
using UnityEngine;
namespace ComicShop {
public static class ComicPreferences {
    public static event Action Changed;
    public static float Master => PlayerPrefs.GetFloat("cs_master_volume",1f);
    public static float Music => PlayerPrefs.GetFloat("cs_music_volume",.8f);
    public static float Sfx => PlayerPrefs.GetFloat("cs_sfx_volume",.8f);
    public static float Sensitivity => PlayerPrefs.GetFloat("cs_look_multiplier",1f);
    public static float Fov => PlayerPrefs.GetFloat("cs_camera_fov",75f);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Changed=null; }
    public static void Set(string key,float value) {
        PlayerPrefs.SetFloat(key,value); AudioListener.volume=Master;
        ComicMenuButton.GlobalSfxVolume=Sfx; Changed?.Invoke();
    }
    public static void Save() => PlayerPrefs.Save();
}}
