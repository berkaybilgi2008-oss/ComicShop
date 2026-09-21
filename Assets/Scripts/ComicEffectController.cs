using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime comic feedback: a short BAM on head impact and rotating stars while knocked down.
/// No scene/prefab setup is required.
/// </summary>
public static class ComicEffectController
{
    private const float BamLifetime = 0.65f;
    private static readonly Dictionary<GameObject, GameObject> dizzyEffects = new Dictionary<GameObject, GameObject>();

    public static void PlayBam(Vector3 worldPosition)
    {
        GameObject root = new GameObject("ComicEffect_BAM");
        root.transform.position = worldPosition + Vector3.up * 0.15f;
        root.AddComponent<ComicBamEffect>();
    }

    public static void SetDizzyStars(GameObject target, bool enabled)
    {
        if (target == null) return;

        if (!enabled)
        {
            if (dizzyEffects.TryGetValue(target, out GameObject old) && old != null)
                Object.Destroy(old);
            dizzyEffects.Remove(target);
            return;
        }

        if (dizzyEffects.TryGetValue(target, out GameObject existing) && existing != null)
            return;

        GameObject root = new GameObject("ComicEffect_DizzyStars");
        root.transform.SetParent(target.transform, false);
        root.AddComponent<ComicDizzyStars>();
        dizzyEffects[target] = root;
    }

    private sealed class ComicBamEffect : MonoBehaviour
    {
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<TextMesh> texts = new List<TextMesh>();
        private float age;

        private void Awake()
        {
            AddShape("BAM_Outline", MakeBurst(1.18f, 0.84f, 14), new Color(0.025f, 0.01f, 0.005f), 0.08f);
            AddShape("BAM_Orange", MakeBurst(1.02f, 0.70f, 14), new Color(1f, 0.42f, 0.015f), 0.05f);
            AddShape("BAM_Yellow", MakeBurst(0.82f, 0.57f, 14), new Color(1f, 0.82f, 0.035f), 0.02f);

            TextMesh shadow = CreateText("BAM!", new Color(0.025f, 0.008f, 0.002f), 0.40f);
            shadow.transform.localPosition = new Vector3(0.055f, -0.055f, -0.05f);

            TextMesh face = CreateText("BAM!", new Color(1f, 0.12f, 0.015f), 0.37f);
            face.transform.localPosition = new Vector3(0f, 0f, -0.08f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                float radius = 1.08f + (i % 2) * 0.14f;
                GameObject spark = AddShape("BAM_Spark", MakeStar(0.14f, 0.055f),
                    i % 2 == 0 ? new Color(1f, 0.9f, 0.04f) : new Color(1f, 0.28f, 0.01f), -0.02f);
                spark.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.04f);
                spark.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }

            transform.localScale = Vector3.one * 0.08f;
        }

        private GameObject AddShape(string name, Vector2[] points, Color color, float z)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);

            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                vertices[i] = new Vector3(points[i].x, points[i].y, 0f);

            int[] triangles = new int[(points.Length - 2) * 3];
            for (int i = 0; i < points.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(shader) { color = color };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderers.Add(renderer);
            return go;
        }

        private Vector2[] MakeBurst(float outer, float inner, int rays)
        {
            Vector2[] points = new Vector2[rays * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = Mathf.PI * 2f * i / points.Length;
                float radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private Vector2[] MakeStar(float radius, float inner)
        {
            Vector2[] points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 2f * i / 10f + Mathf.PI * 0.5f;
                float r = i % 2 == 0 ? radius : inner;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            return points;
        }

        private TextMesh CreateText(string value, Color color, float size)
        {
            GameObject go = new GameObject("BAM_Text");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * size;

            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = value;
            mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            mesh.fontStyle = FontStyle.Bold;
            mesh.fontSize = 64;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = color;
            texts.Add(mesh);
            return mesh;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / BamLifetime);

            float scale = t < 0.14f
                ? Mathf.Lerp(0.08f, 1.16f, t / 0.14f)
                : t < 0.30f
                    ? Mathf.Lerp(1.16f, 0.98f, (t - 0.14f) / 0.16f)
                    : Mathf.Lerp(0.98f, 0.84f, (t - 0.30f) / 0.70f);

            transform.localScale = Vector3.one * scale;
            transform.position += Vector3.up * (0.38f * Time.deltaTime);

            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

            float alpha = t < 0.62f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.62f) / 0.38f);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Color c = renderer.sharedMaterial.color;
                c.a = alpha;
                renderer.sharedMaterial.color = c;
            }

            foreach (TextMesh text in texts)
            {
                if (text == null) continue;
                Color c = text.color;
                c.a = alpha;
                text.color = c;
            }

            if (age >= BamLifetime)
                Destroy(gameObject);
        }
    }

    private sealed class ComicDizzyStars : MonoBehaviour
    {
        private readonly List<Transform> stars = new List<Transform>();
        private float angle;
        private Transform head;

        private void Awake()
        {
            head = FindHead(transform.parent);

            for (int i = 0; i < 3; i++)
            {
                GameObject star = new GameObject("DizzyStar");
                star.transform.SetParent(transform, false);
                star.transform.localScale = Vector3.one * (0.42f + i * 0.08f);

                TextMesh mesh = star.AddComponent<TextMesh>();
                mesh.text = "*";
                mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                mesh.fontStyle = FontStyle.Bold;
                mesh.fontSize = 42;
                mesh.alignment = TextAlignment.Center;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.color = new Color(1f, 0.84f, 0.05f);

                stars.Add(star.transform);
            }
        }

        private void LateUpdate()
        {
            Transform target = transform.parent;
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (head != null)
                transform.position = head.position + Vector3.up * 0.35f;
            else
                transform.position = target.position + Vector3.up * 1.9f;

            angle += 170f * Time.deltaTime;

            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cam.transform.position, Vector3.up);

            for (int i = 0; i < stars.Count; i++)
            {
                float a = (angle + i * 120f) * Mathf.Deg2Rad;
                float radius = 0.48f + Mathf.Sin(Time.time * 3f + i) * 0.06f;

                stars[i].localPosition = new Vector3(
                    Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * 0.16f,
                    0f);

                stars[i].localRotation = Quaternion.Euler(
                    0f, 0f, -angle * (i % 2 == 0 ? 0.8f : 1.1f));
            }
        }

        private static Transform FindHead(Transform root)
        {
            if (root == null) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("head") || name.Contains("kafa"))
                    return t;
            }

            return null;
        }
    }
}
