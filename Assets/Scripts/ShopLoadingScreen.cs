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
        var gold = new Color(0.83f, 0.66f, 0.35f);
        var dim = new Color(0.35f, 0.24f, 0.13f);
        Fill(new Rect(60, 48, 1160, 1), dim);
        Fill(new Rect(60, 671, 1160, 1), dim);
        Label(new Rect(240, 115, 800, 30), "ÇİZGİ ROMAN DÜKKÂNI", 19, gold);
        // A small illuminated shop window, filled by book spines as real work completes.
        Fill(new Rect(490, 200, 300, 3), gold);
        Fill(new Rect(490, 200, 3, 150), gold);
        Fill(new Rect(787, 200, 3, 150), gold);
        for (int row = 0; row < 2; row++)
        {
            Fill(new Rect(490, 273 + row * 76, 300, 3), gold);
            for (int col = 0; col < 16; col++)
            {
                float threshold = (row * 16 + col + 1) / 32f;
                float height = 32 + (col * 17 % 28);
                Fill(new Rect(505 + col * 17, 270 + row * 76 - height, 10, height), progress >= threshold ? gold : dim);
            }
        }
        Label(new Rect(160, 399, 960, 65), "Birazdan kapılar açılıyor", 36, new Color(0.96f, 0.88f, 0.71f));
        Label(new Rect(200, 472, 880, 35), "Yeni bir hikâye için son hazırlıklar…", 18, gold);
        Fill(new Rect(440, 552, 400, 3), dim);
        Fill(new Rect(440, 552, 400 * progress, 3), gold);
        float x = 440 + Mathf.PingPong(Time.unscaledTime * 120, 390);
        Fill(new Rect(x, 550, 10, 7), new Color(1f, 0.88f, 0.57f));
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }
    static void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
    static void Label(Rect rect, string text, int size, Color color)
    {
        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}
