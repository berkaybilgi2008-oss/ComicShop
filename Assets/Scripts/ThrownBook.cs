using UnityEngine;

/// <summary>
/// Sarjli atisla firlatilan kitaba GECICI olarak eklenir.
///
/// Iki isi var:
///   1) Kitap ucarken kendi duzleminde temiz doner (yalpalamaz).
///   2) Temastan sonra kendi donusunu yavaslatir. Diger dinamik kitaplarin
///      hizini sifirlamaz; aksi halde dusme ve yerlesme engellenir.
///
/// Fizik normal calisir: yercekimi hep aciktir, hiz hava surtunmesiyle
/// yavas yavas duser. Hicbir yerde "sure doldu, dur" gibi yapay bir mudahale yok.
/// Kitap yerine oturunca bilesen kendini kaldirir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ThrownBook : MonoBehaviour
{
    [Header("Ucus")]
    [Tooltip("Havadayken surtunme. Kitap ince kenariyla gittigi icin dusuk olmali, " +
             "ama SIFIR olmamali -- sifir olursa hic yavaslamaz.")]
    [Min(0f)] public float flightLinearDamping = 0.08f;
    [Min(0f)] public float flightAngularDamping = 0.05f;

    [Tooltip("Donme eksenini her fizik adiminda hizalar. Kitabin havada yalpalayip " +
             "yamuk donmesini engeller. Ilk carpismadan sonra devre disi kalir.")]
    public bool lockSpinAxis = true;

    [Header("Carpma Sonrasi")]
    [Tooltip("Bir yere carptiktan sonraki surtunme. Kitabin donerek kaymaya " +
             "devam etmesini engeller.")]
    [Min(0f)] public float impactLinearDamping = 0.9f;
    [Min(0f)] public float impactAngularDamping = 0.75f;

    [Header("Temizlik")]
    [Tooltip("Kitap bu sure icinde durmazsa bilesen yine de kendini kaldirir.")]
    [Min(1f)] public float maxLifeTime = 20f;

    private Rigidbody body;
    private float spawnTime;
    private float originalLinearDamping;
    private float originalAngularDamping;
    private Vector3 spinAxis = Vector3.right;
    private bool hasHit;
    private Transform thrower;
    private Vector3 previousPosition;
    private readonly RaycastHit[] obstructionHits = new RaycastHit[32];

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        spawnTime = Time.time;
        previousPosition = transform.position;

        if (body != null)
        {
            originalLinearDamping = body.linearDamping;
            originalAngularDamping = body.angularDamping;

            body.linearDamping = flightLinearDamping;
            body.angularDamping = flightAngularDamping;
        }
    }

    /// <summary>Firlatan taraf donme eksenini bildirir.</summary>
    public void Configure(Vector3 axis, Transform source = null)
    {
        thrower = source;
        if (axis.sqrMagnitude > 0.0001f)
            spinAxis = axis.normalized;
    }

    void FixedUpdate()
    {
        if (body != null && !hasHit && !body.isKinematic) CheckPlayerHit();
        if (body == null || hasHit || !lockSpinAxis)
            return;

        // Acisal hizin eksen disi bileseni atilir -> yalpalama olmaz.
        body.angularVelocity = spinAxis * Vector3.Dot(body.angularVelocity, spinAxis);
    }

    private void CheckPlayerHit()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer) return;
        Vector3 end = body.position + body.linearVelocity * Time.fixedDeltaTime;
        Vector3 travel = end - previousPosition;
        Vector3 start = previousPosition;
        previousPosition = body.position;
        if (travel.sqrMagnitude < 0.000001f || body.linearVelocity.sqrMagnitude < 4f) return;
        Ray ray = new Ray(start, travel.normalized);
        float closest = travel.magnitude;
        PlayerKnockdown target = null;
        foreach (var player in PlayerKnockdown.Players)
        {
            if (player == null || player.IsDown || player.transform == thrower) continue;
            Bounds bounds = player.HitBounds;
            bounds.Expand(0.12f);
            if (bounds.IntersectRay(ray, out float distance) && distance <= closest)
            { closest = distance; target = player; }
        }
        if (target == null) return;
        // Remote CharacterControllers are disabled: use their calibrated bounds
        // for hits, and a non-alloc physics sweep to reject hits through walls.
        int count = Physics.SphereCastNonAlloc(ray, 0.06f, obstructionHits, closest,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == obstructionHits.Length) return;
        for (int i = 0; i < count; i++)
        {
            Transform hit = obstructionHits[i].collider.transform;
            if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform) ||
                (thrower != null && hit.IsChildOf(thrower))) continue;
            return;
        }
        Vector3 point = ray.GetPoint(closest);
        Bounds hitBounds = target.HitBounds;
        bool head = point.y >= hitBounds.max.y - hitBounds.size.y * 0.22f;
        target.Hit(body.linearVelocity, head);
        RegisterHit(); // One knockdown per throw; floor-bounced books do not hit again.
        body.linearVelocity *= 0.25f;
    }

    void Update()
    {
        if (body == null || body.isKinematic || Time.time - spawnTime > maxLifeTime)
            Destroy(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        RegisterHit();

        // ILERIDE: buraya oyuncuya carpma / bayiltma kontrolu gelecek.
        // PlayerController hit = collision.collider.GetComponentInParent<PlayerController>();
        // if (hit != null) { ... }
    }

    /// <summary>Ilk temas: artik normal bir kitap gibi davransin.</summary>
    private void RegisterHit()
    {
        if (hasHit || body == null)
            return;

        hasHit = true;
        body.linearDamping = impactLinearDamping;
        body.angularDamping = impactAngularDamping;
    }

    void OnDestroy()
    {
        if (body != null)
        {
            body.linearDamping = originalLinearDamping;
            body.angularDamping = originalAngularDamping;
        }
    }
}
