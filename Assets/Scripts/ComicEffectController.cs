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
        private readonly List<TextMesh> texts = new List<TextMesh>();
        private readonly List<Transform> stars = new List<Transform>();
        private float age;

        private void Awake()
        {
            TextMesh back = CreateText("BAM!", new Color(0.05f, 0.01f, 0.005f), 0.36f, 52);
            back.transform.localPosition = new Vector3(0.035f, -0.035f, 0.02f);

            TextMesh front = CreateText("BAM!", new Color(1f, 0.72f, 0.02f), 0.32f, 52);
            front.transform.localPosition = Vector3.zero;

            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                float radius = 0.72f + (i % 2) * 0.12f;

                GameObject star = new GameObject("BamStar");
                star.transform.SetParent(transform, false);
                star.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
                star.transform.localScale = Vector3.one * (0.45f + (i % 3) * 0.12f);

                TextMesh mesh = star.AddComponent<TextMesh>();
                mesh.text = "*";
                mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                mesh.fontStyle = FontStyle.Bold;
                mesh.fontSize = 42;
                mesh.alignment = TextAlignment.Center;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.color = i % 2 == 0
                    ? new Color(1f, 0.84f, 0.05f)
                    : new Color(1f, 0.35f, 0.05f);

                stars.Add(star.transform);
                texts.Add(mesh);
            }

            texts.Add(back);
            texts.Add(front);
        }

        private TextMesh CreateText(string value, Color color, float size, int fontSize)
        {
            GameObject go = new GameObject("ComicText");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * size;

            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = value;
            mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            mesh.fontStyle = FontStyle.Bold;
            mesh.fontSize = fontSize;
            mesh.alignment = TextAlignment.Center;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.richText = false;
            mesh.color = color;
            return mesh;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / BamLifetime);

            float punch = t < 0.18f
                ? Mathf.Lerp(0.15f, 1.18f, t / 0.18f)
                : Mathf.Lerp(1.18f, 0.92f, (t - 0.18f) / 0.82f);

            transform.localScale = Vector3.one * punch;
            transform.position += Vector3.up * (0.55f * Time.deltaTime);

            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cam.transform.position, Vector3.up);

            float alpha = 1f - t;
            for (int i = 0; i < texts.Count; i++)
            {
                TextMesh mesh = texts[i];
                if (mesh == null) continue;
                Color color = mesh.color;
                color.a = alpha;
                mesh.color = color;
            }

            for (int i = 0; i < stars.Count; i++)
            {
                Transform star = stars[i];
                if (star == null) continue;
                star.Rotate(0f, 0f, (i % 2 == 0 ? 260f : -220f) * Time.deltaTime);
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
