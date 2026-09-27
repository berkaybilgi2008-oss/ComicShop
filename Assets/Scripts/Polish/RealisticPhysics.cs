using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ayarlar > Goruntu > "Gercekci fizik". Kapaliyken (varsayilan) hicbir ek maliyet yoktur.
/// Acikken:
///  * Firlatilan kitaplar donmus kitap yiginlarini dagitir (BookItem.RealisticImpact).
///  * Tavandan sarkan lambalar kitap carpinca sarkac gibi sallanir (PendantLampSwing).
/// Kitap fizigi sunucuda calisir; odayi kuran oyuncunun ayari gecerlidir. Lamba sallanmasi
/// her oyuncunun kendi ekraninda, kendi ayarina gore hesaplanir.
/// </summary>
public static class RealisticPhysics
{
    public static bool Enabled => ShopSettings.Current.realisticPhysics;
}

[DisallowMultipleComponent]
public sealed class PendantLampSwing : MonoBehaviour
{
    const string PendantLightName = "ComicShop Pendant Spot";
    const string ShaftName = "Lamp Shaft";

    sealed class Lamp
    {
        public Transform pivot;
        public Quaternion rest;
        public float length;      // pivot -> abajur alti
        public float radius;      // abajur yaricapi
        public Vector2 angle;     // derece, dunya X/Z yonunde sapma
        public Vector2 velocity;  // derece/sn
        public Vector3 Bottom => pivot.position + pivot.rotation * (Quaternion.Inverse(rest) * (Vector3.down * length));
    }

    readonly List<Lamp> lamps = new List<Lamp>();
    readonly Dictionary<BookItem, Vector3> lastPositions = new Dictionary<BookItem, Vector3>();
    readonly Dictionary<long, float> recentHits = new Dictionary<long, float>();
    static PendantLampSwing instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

    static void Install()
    {
        if (instance != null) return;
        bool any = false;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (light != null && light.name == PendantLightName) { any = true; break; }
        if (!any) return;
        instance = new GameObject("Pendant Lamp Swing (runtime)").AddComponent<PendantLampSwing>();
    }

    void Start() => BuildLamps();

    void BuildLamps()
    {
        var renderers = new List<Renderer>();
        foreach (var shop in Object.FindObjectsByType<ComicShopV16.ShopV16Appearance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            renderers.AddRange(shop.GetComponentsInChildren<Renderer>(true));
        var shafts = new List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (t.name == ShaftName) shafts.Add(t);
        var used = new HashSet<Transform>();

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light == null || light.name != PendantLightName) continue;
            Vector3 at = light.transform.position;
            // Lambanin parcalari: isigin hemen ustunde/cevresinde, dar bir dikey sutundaki mesh'ler.
            var parts = new List<Renderer>();
            float top = at.y, bottom = at.y, radius = 0.15f;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                if (b.size.x > 1.4f || b.size.z > 1.4f) continue;
                Vector2 d = new Vector2(b.center.x - at.x, b.center.z - at.z);
                if (d.magnitude > 0.45f || b.center.y < at.y - 0.8f || b.center.y > at.y + 3f) continue;
                parts.Add(r);
                top = Mathf.Max(top, b.max.y); bottom = Mathf.Min(bottom, b.min.y);
                radius = Mathf.Max(radius, Mathf.Max(b.extents.x, b.extents.z));
            }
            if (parts.Count == 0) continue;
            Transform group = CommonAncestor(parts);
            // Ortak ata dukkanin tamami ise (lamba ayri bir grup degil) parcalar tek tek tasinir.
            bool groupIsLamp = group != null && group.GetComponent<ComicShopV16.ShopV16Appearance>() == null &&
                               GroupOnlyContains(group, parts);
            var pivot = new GameObject("Lamp Swing Pivot").transform;
            pivot.position = new Vector3(at.x, top, at.z);
            pivot.rotation = Quaternion.identity;
            Transform parentForPivot = groupIsLamp ? group.parent : parts[0].transform.parent;
            pivot.SetParent(parentForPivot, true);
            if (groupIsLamp && !used.Contains(group)) { group.SetParent(pivot, true); used.Add(group); }
            else foreach (var r in parts) if (!used.Contains(r.transform)) { r.transform.SetParent(pivot, true); used.Add(r.transform); }
            light.transform.SetParent(pivot, true);
            foreach (var shaft in shafts)
            {
                if (shaft == null || used.Contains(shaft)) continue;
                Vector3 p = shaft.position;
                if (new Vector2(p.x - at.x, p.z - at.z).magnitude < 0.3f && Mathf.Abs(p.y - at.y) < 1.5f)
                { shaft.SetParent(pivot, true); used.Add(shaft); }
            }
            lamps.Add(new Lamp { pivot = pivot, rest = pivot.rotation, length = Mathf.Max(0.3f, top - bottom), radius = Mathf.Clamp(radius, 0.12f, 0.6f) });
        }
    }

    static Transform CommonAncestor(List<Renderer> parts)
    {
        Transform candidate = parts[0].transform.parent;
        while (candidate != null)
        {
            bool all = true;
            foreach (var r in parts) if (!r.transform.IsChildOf(candidate)) { all = false; break; }
            if (all) return candidate;
            candidate = candidate.parent;
        }
        return null;
    }

    static bool GroupOnlyContains(Transform group, List<Renderer> parts)
    {
        var set = new HashSet<Renderer>(parts);
        foreach (var r in group.GetComponentsInChildren<Renderer>(true)) if (!set.Contains(r)) return false;
        return true;
    }

    void FixedUpdate()
    {
        if (lamps.Count == 0) return;
        float dt = Time.fixedDeltaTime;
        if (RealisticPhysics.Enabled) DetectHits(dt);
        else if (lastPositions.Count > 0) lastPositions.Clear();

        foreach (var lamp in lamps)
        {
            if (lamp.pivot == null) continue;
            if (lamp.angle.sqrMagnitude < 0.0001f && lamp.velocity.sqrMagnitude < 0.0001f) continue;
            // Sonumlu sarkac: w'' = -(g/L) * aci - c * w'
            float stiffness = 9.81f / lamp.length;
            lamp.velocity += (-stiffness * lamp.angle - 0.9f * lamp.velocity) * dt;
            lamp.angle += lamp.velocity * dt;
            if (lamp.angle.magnitude > 38f) { lamp.angle = lamp.angle.normalized * 38f; lamp.velocity *= 0.5f; }
            if (lamp.angle.sqrMagnitude < 0.0004f && lamp.velocity.sqrMagnitude < 0.0004f)
            { lamp.angle = Vector2.zero; lamp.velocity = Vector2.zero; lamp.pivot.rotation = lamp.rest; continue; }
            Vector3 lean = new Vector3(lamp.angle.x, 0f, lamp.angle.y);
            Quaternion swing = Quaternion.AngleAxis(lean.magnitude, Vector3.Cross(Vector3.down, lean.normalized));
            lamp.pivot.rotation = swing * lamp.rest;
        }
    }

    void DetectHits(float dt)
    {
        foreach (var book in BookItem.Active)
        {
            if (book == null) continue;
            Vector3 now = book.transform.position;
            if (!lastPositions.TryGetValue(book, out Vector3 before)) { lastPositions[book] = now; continue; }
            lastPositions[book] = now;
            Vector3 move = now - before;
            float speed = move.magnitude / Mathf.Max(0.0001f, dt);
            if (speed < 3f || move.sqrMagnitude > 25f) continue; // duran kitap ya da isinlanma
            for (int i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                if (lamp.pivot == null) continue;
                float distance = SegmentDistance(before, now, lamp.pivot.position, lamp.Bottom);
                if (distance > lamp.radius + 0.18f) continue;
                long key = ((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(book) << 32) ^ (uint)i;
                if (recentHits.TryGetValue(key, out float last) && Time.time - last < 0.4f) continue;
                recentHits[key] = Time.time;
                Vector2 push = new Vector2(move.x, move.z);
                if (push.sqrMagnitude < 0.000001f) continue;
                lamp.velocity += push.normalized * Mathf.Min(speed * 7f, 200f);
            }
        }
        if (recentHits.Count > 512) recentHits.Clear();
    }

    // Iki dogru parcasi arasindaki en kisa mesafe.
    static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-8f && e <= 1e-8f) return r.magnitude;
        if (a <= 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
    }

    void OnDestroy() { if (instance == this) instance = null; }
}
