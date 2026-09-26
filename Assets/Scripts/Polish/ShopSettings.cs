using System;
using UnityEngine;

public enum ShopAction { Forward, Back, Left, Right, Jump, Sprint, Pickup, Place, Throw, Recall }

// User preferences only. A new session intentionally starts a new tidy-up round.
public static class ShopSettings
{
    const string StorageKey = "ComicShop.Preferences.v1";
    [Serializable]
    public sealed class Preferences
    {
        public float sensitivity = 2.2f, fov = 75f, master = .8f, effects = .8f, ambience = .3f, music = .55f;
        public bool fovChosen, invertY, vsync = true, hints = true;
        public int fps = 120, width, height, windowMode = 1;
        public int[] keys = DefaultKeys();
    }
    static Preferences current;
    static float? nativeFov;
    public static Preferences Current { get { if (current == null) Load(); return current; } }
    public static event Action Changed;
    static int[] DefaultKeys() => new[] { (int)KeyCode.W, (int)KeyCode.S, (int)KeyCode.A, (int)KeyCode.D,
        (int)KeyCode.Space, (int)KeyCode.LeftShift, (int)KeyCode.Mouse0, (int)KeyCode.Mouse1, (int)KeyCode.Q, (int)KeyCode.E };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { current = null; nativeFov = null; Changed = null; }
    static float Clamp(float value, float low, float high, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, low, high);
    public static void Validate(Preferences p)
    {
        p.sensitivity = Clamp(p.sensitivity, .1f, 10f, 2.2f);
        p.fov = Clamp(p.fov, 55, 105, 75);
        p.master = Clamp(p.master, 0, 1, .8f); p.effects = Clamp(p.effects, 0, 1, .8f);
        p.ambience = Clamp(p.ambience, 0, 1, .3f); p.music = Clamp(p.music, 0, 1, .55f);
        p.fps = Mathf.Clamp(p.fps, 30, 240); p.windowMode = Mathf.Clamp(p.windowMode, 0, 1);
        if (p.width < 640 || p.width > 16384 || p.height < 480 || p.height > 16384) { p.width = 0; p.height = 0; }
        var defaults = DefaultKeys();
        if (p.keys == null || p.keys.Length != defaults.Length) p.keys = defaults;
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (int key in p.keys)
            if (!Enum.IsDefined(typeof(KeyCode), key) || key == (int)KeyCode.Escape || key == (int)KeyCode.None || !seen.Add(key))
            { p.keys = defaults; break; }
    }
    static void Load()
    {
        try { current = JsonUtility.FromJson<Preferences>(PlayerPrefs.GetString(StorageKey, "")); }
        catch (Exception) { current = null; }
        if (current == null) current = new Preferences();
        Validate(current);
    }
    public static KeyCode Key(ShopAction action) => (KeyCode)Current.keys[(int)action];
    public static bool Rebind(ShopAction action, KeyCode key)
    {
        if (key == KeyCode.None || key == KeyCode.Escape || !Enum.IsDefined(typeof(KeyCode), key)) return false;
        int index = (int)action;
        for (int i = 0; i < Current.keys.Length; i++) if (i != index && Current.keys[i] == (int)key) return false;
        Current.keys[index] = (int)key; Apply(); return true;
    }
    public static void ResetDefaults() { current = new Preferences(); Apply(); Save(); }
    public static void Apply()
    {
        Validate(Current);
        QualitySettings.vSyncCount = Current.vsync ? 1 : 0;
        Application.targetFrameRate = Current.vsync ? -1 : Current.fps;
        var player = NetworkPlayerSetup.LocalPlayer;
        if (player != null) ApplyPlayer(player.GetComponent<PlayerInteraction>());
        Changed?.Invoke();
    }
    public static void ApplyPlayer(PlayerInteraction interaction)
    {
        if (interaction == null) return;
        interaction.pickupKey = Key(ShopAction.Pickup);
        interaction.dropKey = Key(ShopAction.Place);
        interaction.throwKey = Key(ShopAction.Throw);
        if (interaction.playerCamera != null)
        {
            if (!nativeFov.HasValue) nativeFov = interaction.playerCamera.fieldOfView;
            if (!Current.fovChosen) Current.fov = Mathf.Clamp(nativeFov.Value, 55, 105);
            interaction.playerCamera.fieldOfView = Current.fov;
        }
    }
    public static void Save() { PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); }
    public static string Label(ShopAction action)
    {
        string[] keys = { "action.forward", "action.back", "action.left", "action.right", "action.jump", "action.sprint", "action.pickup", "action.place", "action.throw", "action.recall" };
        return Loc.T(keys[(int)action]);
    }
}
