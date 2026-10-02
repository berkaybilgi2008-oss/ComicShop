using UnityEngine;
using UnityEngine.UI;

// Ana menu butonlarinin (PLAY / SETTINGS / CREDITS / QUIT) cizim dili, her boyutta:
// siyah golge, kalin siyah dis cizgi, ince krem cizgi, ic siyah cizgi, kagit/turuncu dolgu
// ve saga dogru yogunlasan baski noktalari. Kose kesikleri butonlardaki gibi asimetrik.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HudShape : MaskableGraphic
{
    public enum Frame { None, Thin, Menu }

    // Ana menu gorsellerinden olculen renkler.
    public static readonly Color Ink = new Color32(10, 6, 5, 255);
    public static readonly Color Line = new Color32(255, 244, 181, 255);

    Color fill = Color.white;
    Frame frame = Frame.Menu;
    float cut = 14f;
    bool dots = true, shadow = true;

    public void Set(Color fill, Frame frame, float cut, bool dots = true, bool shadow = true)
    {
        if (this.fill == fill && this.frame == frame && Mathf.Approximately(this.cut, cut) && this.dots == dots && this.shadow == shadow) return;
        this.fill = fill; this.frame = frame; this.cut = cut; this.dots = dots; this.shadow = shadow;
        SetVerticesDirty();
    }

    public Color Fill
    {
        get => fill;
        set { if (fill == value) return; fill = value; SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        if (r.width <= 1 || r.height <= 1) return;
        float c = Mathf.Min(cut, r.width * .45f, r.height * .45f);

        float outer = 0, line = 0, inner = 0;
        if (frame == Frame.Menu)
        {
            // Butonlarda ~ 5 / 2 / 3 px; kucuk kutularda orantili incelir.
            float k = Mathf.Clamp01(Mathf.Min(r.width, r.height) / 70f);
            outer = Mathf.Lerp(3, 5, k); line = Mathf.Lerp(1.5f, 2, k); inner = Mathf.Lerp(2, 3, k);
        }
        else if (frame == Frame.Thin) outer = 2;

        if (shadow && fill.a > .5f)
            Fan(vh, Points(Offset(r, 4, -5), c), Tint(new Color(0, 0, 0, .55f)));

        if (frame != Frame.None)
        {
            Fan(vh, Points(r, c), Tint(Ink));
            if (line > 0)
            {
                Fan(vh, Points(Inset(r, outer), Cut(c, outer)), Tint(Line));
                Fan(vh, Points(Inset(r, outer + line), Cut(c, outer + line)), Tint(Ink));
            }
        }
        float d = outer + line + inner;
        var body = Inset(r, d);
        float bc = Cut(c, d);
        if (body.width <= 1 || body.height <= 1) return;
        Fan(vh, Points(body, bc), Tint(fill));
        if (dots) Dots(vh, body, bc);
    }

    Color Tint(Color c) => c * color;

    // Sol tarafta seyrek, saga dogru buyuyen noktalar (ana menu butonlarindaki baski dokusu).
    void Dots(VertexHelper vh, Rect r, float c)
    {
        const float step = 6f;
        var dot = new Color(fill.r * .78f, fill.g * .70f, fill.b * .55f, .55f * fill.a);
        float from = r.xMin + r.width * .18f;
        int row = 0;
        for (float y = r.yMin + 3; y < r.yMax - 2; y += step, row++)
        {
            for (float x = r.xMin + 3 + (row % 2 == 1 ? step * .5f : 0); x < r.xMax - 2; x += step)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(from, r.xMax, x));
                if (t <= 0) continue;
                float size = .8f + 1.9f * t;
                // Kesik koselerin disina tasma.
                if (x - r.xMin + (r.yMax - y) < c + 2) continue;
                if (r.xMax - x + (y - r.yMin) < c + 2) continue;
                Quad(vh, x, y, size, Tint(dot));
            }
        }
    }

    static float Cut(float c, float inset) => Mathf.Max(0, c - inset * .586f);
    static Rect Inset(Rect r, float n) => new Rect(r.x + n, r.y + n, r.width - 2 * n, r.height - 2 * n);
    static Rect Offset(Rect r, float x, float y) => new Rect(r.x + x, r.y + y, r.width, r.height);

    // Saat yonunde; sol-ust ve sag-alt buyuk kesik, sag-ust ve sol-alt kucuk kesik.
    static Vector2[] Points(Rect r, float c)
    {
        float s = c * .4f;
        return new[]
        {
            new Vector2(r.xMin + c, r.yMax), new Vector2(r.xMax - s, r.yMax), new Vector2(r.xMax, r.yMax - s),
            new Vector2(r.xMax, r.yMin + c), new Vector2(r.xMax - c, r.yMin), new Vector2(r.xMin + s, r.yMin),
            new Vector2(r.xMin, r.yMin + s), new Vector2(r.xMin, r.yMax - c)
        };
    }

    static void Fan(VertexHelper vh, Vector2[] p, Color c)
    {
        int start = vh.currentVertCount;
        var center = Vector2.zero;
        foreach (var v in p) center += v;
        vh.AddVert(center / p.Length, c, Vector2.zero);
        foreach (var v in p) vh.AddVert(v, c, Vector2.zero);
        for (int i = 0; i < p.Length; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % p.Length);
    }

    static void Quad(VertexHelper vh, float x, float y, float s, Color c)
    {
        int i = vh.currentVertCount; float h = s * .5f;
        vh.AddVert(new Vector2(x - h, y - h), c, Vector2.zero); vh.AddVert(new Vector2(x - h, y + h), c, Vector2.zero);
        vh.AddVert(new Vector2(x + h, y + h), c, Vector2.zero); vh.AddVert(new Vector2(x + h, y - h), c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
    }
}
