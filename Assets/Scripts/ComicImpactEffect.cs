using UnityEngine;
using UnityEngine.UI;

// Geometry-based comic burst: crisp at any resolution, no opaque image background.
// Each peer uses the authoritative knockdown timestamp to expire it after two seconds.
public sealed class ComicImpactEffect : MonoBehaviour
{
    float age;
    CanvasGroup group;
    RectTransform burst;
    readonly RectTransform[] dust = new RectTransform[10];
    Vector3 origin;

    public static void Play(Vector3 point, float elapsed)
    {
        if (elapsed >= 2f) return;
        var root = new GameObject("Head impact POW", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 450);
        root.transform.position = point;
        root.transform.localScale = Vector3.one * 0.003f;
        var effect = root.AddComponent<ComicImpactEffect>();
        effect.age = elapsed;
        effect.origin = point;
        effect.group = root.GetComponent<CanvasGroup>();
        effect.group.blocksRaycasts = false;
        effect.group.interactable = false;
        effect.Build();
    }

    void Build()
    {
        for (int i = 0; i < dust.Length; i++)
        {
            var puff = Shape("Dust", transform, new Vector2(85, 70), false, new Color(0.92f, 0.88f, 0.74f));
            dust[i] = puff.rectTransform;
        }
        var graphic = Shape("Burst", transform, new Vector2(420, 260), true, new Color(1f, 0.83f, 0.04f));
        burst = graphic.rectTransform;
        burst.localRotation = Quaternion.Euler(0, 0, 12);
        var textObject = new GameObject("POW!", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(burst, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 160);
        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "POW!";
        text.fontSize = 105;
        text.fontStyle = FontStyle.BoldAndItalic;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.03f, 0.13f);
        text.raycastTarget = false;
        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.07f, 0.025f, 0.05f);
        outline.effectDistance = new Vector2(5, -5);
    }

    static ComicBurstGraphic Shape(string name, Transform parent, Vector2 size, bool star, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ComicBurstGraphic));
        obj.transform.SetParent(parent, false);
        var graphic = obj.GetComponent<ComicBurstGraphic>();
        graphic.rectTransform.sizeDelta = size;
        graphic.star = star;
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= 2f) { Destroy(gameObject); return; }
        var local = NetworkPlayerSetup.LocalPlayer;
        Camera camera = local != null ? local.playerCamera : Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
        transform.position = camera != null && Vector3.Distance(camera.transform.position, origin) < 0.9f
            ? camera.transform.position + camera.transform.forward * 1.2f + camera.transform.up * 0.15f
            : origin + Vector3.up * (age * 0.12f);
        group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.35f, 2f, age));
        float pop = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(age / 0.09f));
        burst.localScale = Vector3.one * pop;
        for (int i = 0; i < dust.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / dust.Length;
            float radius = 70f + Mathf.Sqrt(age) * 115f;
            dust[i].anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.55f - 80f);
            dust[i].localScale = Vector3.one * (0.6f + age * 0.6f);
        }
    }
}
