using UnityEngine;

// Lightweight immediate-mode opening card. No textures, font imports or scene rewiring.
public sealed class ShopLoadingScreen : MonoBehaviour
{
    static ShopLoadingScreen instance;
    float progress;
    public static bool IsVisible => instance != null;
    public static void Show()
    {
        if (instance == null) instance = new GameObject("Shop opening card").AddComponent<ShopLoadingScreen>();
        instance.progress = 0f;
    }
    public static void Progress(float value) { if (instance != null) instance.progress = Mathf.Clamp01(value); }
    public static void Hide()
    {
        if (instance == null) return;
        var old = instance;
        instance = null;
        old.enabled = false;
        Destroy(old.gameObject);
    }
    void OnDestroy() { if (instance == this) instance = null; }
    void OnGUI()
    {
        GUI.depth = -30000;
        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.13f, 0.075f, 0.045f));
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
        Color ink = new Color(0.105f, 0.055f, 0.035f);
        Color gold = new Color(0.89f, 0.66f, 0.28f);
        Color cream = new Color(0.98f, 0.91f, 0.76f);
        Color muted = new Color(0.65f, 0.47f, 0.29f);
        // Quiet halftone print texture on the illustration side.
        for (int y = 60; y < 650; y += 24)
            for (int x = 770; x < 1220; x += 24)
                Fill(new Rect(x, y, 3, 3), new Color(0.25f, 0.15f, 0.085f));
        Fill(new Rect(64, 58, 5, 34), gold);
        Label(new Rect(84, 58, 700, 34), Loc.T("load.brand"), 17, gold);
        Fill(new Rect(64, 151, 145, 28), gold);
        Label(new Rect(76, 152, 130, 26), Loc.T("load.day"), 13, ink);
        Label(new Rect(60, 208, 710, 156), Loc.T("load.headline"), 48, cream);
        Label(new Rect(66, 391, 620, 35), Loc.T("load.sub"), 20, muted);
        // Three printed covers, with heavy outlines and offset ink shadows.
        Cover(new Rect(863, 221, 220, 286), -13, muted, ink, cream, 0);
        Cover(new Rect(905, 188, 220, 286), 9, new Color(0.56f, 0.28f, 0.12f), ink, cream, 1);
        Cover(new Rect(867, 171, 220, 286), -4, gold, ink, cream, 2);
        Fill(new Rect(64, 547, 1152, 2), muted);
        string phase = progress < 0.45f ? Loc.T("load.p1") : progress < 0.85f ? Loc.T("load.p2") : Loc.T("load.p3");
        Label(new Rect(64, 573, 800, 32), phase, 19, cream);
        Label(new Rect(1130, 571, 100, 36), Mathf.FloorToInt(progress * 100) + "%", 22, gold);
        Fill(new Rect(64, 627, 1152, 9), ink);
        Fill(new Rect(64, 627, 1152 * progress, 9), gold);
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }
    static void Cover(Rect rect, float angle, Color cover, Color ink, Color cream, int variant)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, rect.center);
        Fill(new Rect(rect.x + 12, rect.y + 14, rect.width, rect.height), ink);
        Fill(rect, ink);
        Fill(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 10), cover);
        Fill(new Rect(rect.x + 17, rect.y + 18, 5, rect.height - 36), ink);
        Label(new Rect(rect.x + 32, rect.y + 24, 168, 40), variant == 2 ? "COMICS" : "STORIES", 25, ink);
        Fill(new Rect(rect.x + 32, rect.y + 70, 153, 3), ink);
        Label(new Rect(rect.x + 45, rect.y + 100, 155, 105), "POW!", 38, cream);
        Fill(new Rect(rect.x + 33, rect.y + 233, 105, 4), ink);
        Fill(new Rect(rect.x + 33, rect.y + 246, 73, 4), ink);
        GUI.matrix = saved;
    }
    static readonly System.Collections.Generic.Dictionary<int, GUIStyle> styles = new System.Collections.Generic.Dictionary<int, GUIStyle>();
    static void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
    static void Label(Rect rect, string text, int size, Color color)
    {
        GUI.color = color;
        if (!styles.TryGetValue(size, out var style))
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft,
                fontStyle = size >= 25 ? FontStyle.Bold : FontStyle.Normal };
            style.normal.textColor = Color.white;
            styles[size] = style;
        }
        // Dile gore font: basliklar menu fontu, digerleri okunakli govde fontu.
        style.font = size >= 25 ? Loc.Display(null) : Loc.Body(null);
        GUI.Label(rect, text, style);
    }
}
