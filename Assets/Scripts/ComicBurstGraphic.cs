using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ComicBurstGraphic : MaskableGraphic
{
    public bool star;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Fan(vh, 1f, new Color(0.07f, 0.025f, 0.05f, color.a));
        Fan(vh, star ? 0.91f : 0.89f, color);
    }
    void Fan(VertexHelper vh, float scale, Color tint)
    {
        int start = vh.currentVertCount;
        const int segments = 32;
        Vector2 center = rectTransform.rect.center;
        Vector2 half = rectTransform.rect.size * (0.5f * scale);
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float radius = star ? (i % 2 == 0 ? 1f + 0.18f * Mathf.Sin(i * 2.3f) : 0.58f) : 1f;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y) * radius, tint, Vector2.zero);
        }
        for (int i = 0; i < segments; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
    }
}
