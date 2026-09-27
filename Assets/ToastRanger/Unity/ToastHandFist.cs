using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Karakterin elleri tek bir "Hand" kemigine bagli duz el mesh'i. Parmak kemigi olmadigi icin
/// yumruk/gevsek el pozu mesh'e BLEND SHAPE olarak eklenir: her el icin "ToastFist_R/L".
/// Parmaklar uc eklemden (MCP/PIP/DIP) avuc yonune bukulur, basparmak parmaklarin onune kapanir.
/// Mesh kopyalanir (asset degismez) ve ayni kaynak mesh tum oyuncularda paylasilir.
/// Avuc yonu/basparmak tarafi mesh geometrisinden olculur; koordinat sistemi ya da aynalama
/// varsayimi yoktur. Ayni hesap avucun kemik uzayindaki normalini da verir (bilek dondurme icin).
/// </summary>
public static class ToastHandFist
{
    public const string RightShape = "ToastFist_R", LeftShape = "ToastFist_L";

    // Yumruk: eklem basina bukulme (derece) ve basparmak kapanmasi.
    static readonly float[] JointAngles = { 60f, 80f, 50f };
    static readonly float[] JointFractions = { 0f, 0.42f, 0.72f };
    const float KnuckleFraction = 0.5f, ThumbTuck = 30f;

    public struct HandInfo
    {
        public bool valid;
        public Vector3 palmLocal;   // Hand kemiginin yerel uzayinda avuc normali
    }

    sealed class Entry
    {
        public Mesh mesh;
        public Vector3 palmMeshRight, palmMeshLeft;
        public bool right, left;
    }

    static readonly Dictionary<Mesh, Entry> cache = new Dictionary<Mesh, Entry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); }

    /// <summary>
    /// Renderer'in mesh'ini yumruk blend shape'li kopyasiyla degistirir. Donen bilgiler
    /// avucun kemik-yerel normalidir (sag, sol).
    /// </summary>
    public static void Install(SkinnedMeshRenderer skin, Transform rightHand, Transform leftHand,
        ref HandInfo right, ref HandInfo left)
    {
        if (!skin || !skin.sharedMesh) return;
        Mesh source = skin.sharedMesh;
        if (source.GetBlendShapeIndex(RightShape) >= 0 || source.GetBlendShapeIndex(LeftShape) >= 0)
        {
            // Zaten kurulu (ornegin ayni renderer'da ikinci kez).
            foreach (var pair in cache)
                if (pair.Value.mesh == source) { Fill(pair.Value, skin, rightHand, leftHand, ref right, ref left); return; }
            return;
        }
        if (!source.isReadable) return;
        var bones = skin.bones;
        if (bones == null) return;
        int ri = System.Array.IndexOf(bones, rightHand), li = System.Array.IndexOf(bones, leftHand);
        if (ri < 0 && li < 0) return;

        if (!cache.TryGetValue(source, out var entry))
        {
            entry = Build(source, ri, li);
            cache[source] = entry;
        }
        if (entry == null || !entry.mesh) return;
        skin.sharedMesh = entry.mesh;
        Fill(entry, skin, rightHand, leftHand, ref right, ref left);
    }

    static void Fill(Entry entry, SkinnedMeshRenderer skin, Transform rightHand, Transform leftHand,
        ref HandInfo right, ref HandInfo left)
    {
        var bones = skin.bones;
        var bindposes = entry.mesh.bindposes;
        int ri = System.Array.IndexOf(bones, rightHand), li = System.Array.IndexOf(bones, leftHand);
        if (entry.right && ri >= 0 && ri < bindposes.Length)
            right = new HandInfo { valid = true, palmLocal = bindposes[ri].MultiplyVector(entry.palmMeshRight).normalized };
        if (entry.left && li >= 0 && li < bindposes.Length)
            left = new HandInfo { valid = true, palmLocal = bindposes[li].MultiplyVector(entry.palmMeshLeft).normalized };
    }

    static Entry Build(Mesh source, int rightIndex, int leftIndex)
    {
        var vertices = source.vertices;
        var normals = source.normals;
        var weights = source.boneWeights;
        var bindposes = source.bindposes;
        if (weights == null || weights.Length != vertices.Length) return null;
        var dominant = new int[vertices.Length];
        for (int i = 0; i < weights.Length; i++)
        {
            BoneWeight w = weights[i];
            int index = w.boneIndex0; float largest = w.weight0;
            if (w.weight1 > largest) { index = w.boneIndex1; largest = w.weight1; }
            if (w.weight2 > largest) { index = w.boneIndex2; largest = w.weight2; }
            if (w.weight3 > largest) index = w.boneIndex3;
            dominant[i] = index;
        }
        Vector3 BonePosition(int index) =>
            index >= 0 && index < bindposes.Length ? bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero) : Vector3.zero;

        var entry = new Entry();
        var rightDelta = new Vector3[vertices.Length];
        var rightNormal = new Vector3[vertices.Length];
        var leftDelta = new Vector3[vertices.Length];
        var leftNormal = new Vector3[vertices.Length];
        Vector3 rightPos = BonePosition(rightIndex), leftPos = BonePosition(leftIndex);
        if (rightIndex >= 0)
            entry.right = Curl(vertices, normals, dominant, rightIndex, rightPos, leftIndex >= 0 ? leftPos : Vector3.zero,
                rightDelta, rightNormal, out entry.palmMeshRight);
        if (leftIndex >= 0)
            entry.left = Curl(vertices, normals, dominant, leftIndex, leftPos, rightIndex >= 0 ? rightPos : Vector3.zero,
                leftDelta, leftNormal, out entry.palmMeshLeft);
        if (!entry.right && !entry.left) return null;

        Mesh mesh = Object.Instantiate(source);
        mesh.name = source.name + "_Fist";
        bool hasNormals = normals != null && normals.Length == vertices.Length;
        if (entry.right) mesh.AddBlendShapeFrame(RightShape, 100f, rightDelta, hasNormals ? rightNormal : null, null);
        if (entry.left) mesh.AddBlendShapeFrame(LeftShape, 100f, leftDelta, hasNormals ? leftNormal : null, null);
        mesh.UploadMeshData(false);
        entry.mesh = mesh;
        return entry;
    }

    static bool Curl(Vector3[] vertices, Vector3[] normals, int[] dominant, int bone, Vector3 hp, Vector3 otherHand,
        Vector3[] delta, Vector3[] deltaNormal, out Vector3 palm)
    {
        palm = Vector3.zero;
        var idx = new List<int>();
        Vector3 mean = Vector3.zero;
        for (int i = 0; i < vertices.Length; i++)
            if (dominant[i] == bone) { idx.Add(i); mean += vertices[i]; }
        if (idx.Count < 12) return false;
        mean /= idx.Count;
        Vector3 f = mean - hp;
        if (f.sqrMagnitude < 1e-10f) return false;
        f.Normalize();

        int count = idx.Count;
        var t = new float[count];
        float lh = 0f;
        for (int k = 0; k < count; k++) { t[k] = Vector3.Dot(vertices[idx[k]] - hp, f); lh = Mathf.Max(lh, t[k]); }
        if (lh <= 1e-5f) return false;

        // Elin genis ekseni (f'ye dik duzlemde en buyuk varyans) -> ince eksen n = f x genis.
        Vector3 wide = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            Vector3 next = Vector3.zero;
            for (int k = 0; k < count; k++)
            {
                Vector3 q = Vector3.ProjectOnPlane(vertices[idx[k]] - mean, f);
                next += q * Vector3.Dot(q, wide);
            }
            if (next.sqrMagnitude < 1e-16f) break;
            wide = Vector3.ProjectOnPlane(next, f).normalized;
        }
        Vector3 n = Vector3.Cross(f, wide).normalized;
        Vector3 a = Vector3.Cross(f, n).normalized;
        // Govde ortasina (diger ele) bakan yan = ic taraf; basparmak orada.
        Vector3 toOther = otherHand - hp;
        float medial = Vector3.Dot(toOther, a) >= 0f ? 1f : -1f;

        float thumbReach = 0.18f * lh;
        var thumb = new bool[count];
        float pnTips = 0f, pnMid = 0f, pnThumb = 0f, pnRest = 0f;
        int nTips = 0, nMid = 0, nThumb = 0, nRest = 0;
        for (int k = 0; k < count; k++)
        {
            Vector3 d = vertices[idx[k]] - hp;
            float pa = Vector3.Dot(d, a) * medial;
            thumb[k] = pa > thumbReach && t[k] < 0.65f * lh;
            float pn = Vector3.Dot(d, n);
            if (thumb[k]) { pnThumb += pn; nThumb++; continue; }
            pnRest += pn; nRest++;
            if (t[k] > 0.85f * lh) { pnTips += pn; nTips++; }
            else if (t[k] > 0.4f * lh && t[k] < 0.6f * lh) { pnMid += pn; nMid++; }
        }
        float score = 0f;
        if (nTips > 0 && nMid > 0) score += pnTips / nTips - pnMid / nMid;
        if (nThumb > 0 && nRest > 0) score += pnThumb / nThumb - pnRest / nRest;
        Vector3 c = score >= 0f ? n : -n;  // avuc yonu: parmak ucu kivrimi + basparmak tarafi
        palm = c;
        Vector3 axis = Vector3.Cross(f, c).normalized; // f'yi c'ye dogru dondurur

        var q2 = new Vector3[count];
        var rot = new Quaternion[count];
        for (int k = 0; k < count; k++) { q2[k] = vertices[idx[k]]; rot[k] = Quaternion.identity; }

        float t0 = KnuckleFraction * lh, length = lh - t0, near = 0.055f * lh;
        for (int j = 0; j < JointAngles.Length; j++)
        {
            float jt = t0 + JointFractions[j] * length;
            Vector3 pivot = Vector3.zero; int pivots = 0; bool any = false;
            for (int k = 0; k < count; k++)
            {
                if (thumb[k]) continue;
                if (t[k] > jt) any = true;
                if (Mathf.Abs(t[k] - jt) < near) { pivot += q2[k]; pivots++; }
            }
            if (!any) continue;
            pivot = pivots > 0 ? pivot / pivots : hp + f * jt;
            Quaternion r = Quaternion.AngleAxis(JointAngles[j], axis);
            for (int k = 0; k < count; k++)
            {
                if (thumb[k] || t[k] <= jt) continue;
                q2[k] = pivot + r * (q2[k] - pivot);
                rot[k] = r * rot[k];
            }
        }
        // Basparmak: el ekseni etrafinda avuca dogru kapanir.
        Vector3 medialDir = a * medial;
        float sign = Vector3.Dot(Vector3.Cross(f, medialDir), c) >= 0f ? 1f : -1f;
        Quaternion thumbRotation = Quaternion.AngleAxis(ThumbTuck, f * sign);
        Vector3 thumbBase = hp + f * (0.2f * lh);
        for (int k = 0; k < count; k++)
        {
            if (!thumb[k]) continue;
            q2[k] = thumbBase + thumbRotation * (q2[k] - thumbBase);
            rot[k] = thumbRotation;
        }

        bool hasNormals = normals != null && normals.Length == vertices.Length;
        for (int k = 0; k < count; k++)
        {
            int vi = idx[k];
            delta[vi] = q2[k] - vertices[vi];
            if (hasNormals) deltaNormal[vi] = rot[k] * normals[vi] - normals[vi];
        }
        return true;
    }
}
