using UnityEngine;

/// <summary>
/// Kitap spawn icin dairesel alan.
/// Circle'i sahneye yerlestirip yaricapini Inspector'dan ayarlarsin.
/// Merkeze yaklastikca spawn olasiligi artar.
/// </summary>
[DisallowMultipleComponent]
public sealed class BookSpawnCircle : MonoBehaviour
{
    [Header("Spawn Alani")]
    [Min(0.01f)]
    [Tooltip("Dairenin yaricapi. X/Z duzleminde kullanilir.")]
    public float radius = 1f;

    [Min(1)]
    [Tooltip("Bu spawn alaninin alabilecegi maksimum kitap sayisi. Dolunca siradaki spawn alanina gecilir.")]
    public int maxBooks = 10;

    [Min(0.5f)]
    [Tooltip("Merkez yogunlugu. 1 = merkez bias yok; buyudukce merkezde spawn olasiligi artar.")]
    public float centerBias = 1f;

    [Tooltip("Kitabin spawn yuksekligi. BookSpawner'in global Spawn Height degeri bunun ustune eklenir.")]
    public bool useSpawnerHeight = true;

    [Min(0f)]
    public float localHeight = 0f;

    public float Weight
    {
        get
        {
            float r = Mathf.Abs(transform.lossyScale.x) * radius;
            return Mathf.PI * r * r;
        }
    }

    public Vector3 Sample(float globalHeight)
    {
        float safeRadius = Mathf.Max(0.01f, radius);

        // Uniform alan dagilimi icin sqrt gerekir.
        // centerBias > 1 yaptikca yaricap merkeze dogru sikisir.
        float exponent = Mathf.Max(0.5f, centerBias);
        float normalizedRadius = Mathf.Lerp(1f, Random.value, 1f / exponent);

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * safeRadius * normalizedRadius;

        Vector3 local = new Vector3(offset.x, 0f, offset.y);
        float height = useSpawnerHeight ? globalHeight : localHeight;

        return transform.TransformPoint(local) + Vector3.up * height;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        maxBooks = Mathf.Max(1, maxBooks);
        centerBias = Mathf.Max(0.5f, centerBias);
        localHeight = Mathf.Max(0f, localHeight);
    }

    private void OnDrawGizmos()
    {
        float worldRadius = radius * Mathf.Abs(transform.lossyScale.x);
        Vector3 center = transform.position + Vector3.up * (useSpawnerHeight ? 0f : localHeight);

        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.18f);
        Gizmos.DrawSphere(center, worldRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, worldRadius);

        Gizmos.DrawLine(center, center + transform.forward * worldRadius);
        Gizmos.DrawLine(center, center + transform.right * worldRadius);
    }
#endif
}
