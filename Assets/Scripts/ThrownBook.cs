using UnityEngine;

/// <summary>
/// Sarjli atisla firlatilan kitaba GECICI olarak eklenir.
///
/// Iki isi var:
///   1) Kisa cikis hareketinden sonra kendi duzleminde donerek ucar.
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

    [Tooltip("Kisa cikistan sonra kitabi tek duzleme hizalar ve bu duzlemde surekli dondurur. " +
             "Ilk carpismadan sonra normal fizik geri gelir.")]
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
    private float originalMaxAngularVelocity;
    private float flightSpin;
    private Vector3 spinAxis = Vector3.right;
    private BookItem item;
    private Quaternion launchRotation;
    private float alignmentElapsed;
    private const float alignmentDuration = 0.065f;
    private bool hasHit;
    private bool hitPlayer;
    public bool HasImpacted => hasHit;
    private Transform thrower;
    private Vector3 incomingVelocity;
    private Vector3 previousPosition;
    private readonly RaycastHit[] obstructionHits = new RaycastHit[32];

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        item = GetComponent<BookItem>();
        spawnTime = Time.time;
        previousPosition = transform.position;

        if (body != null)
        {
            originalLinearDamping = body.linearDamping;
            originalAngularDamping = body.angularDamping;
            originalMaxAngularVelocity = body.maxAngularVelocity;

            body.linearDamping = flightLinearDamping;
            body.angularDamping = flightAngularDamping;
        }
    }

    /// <summary>Firlatan taraf donme eksenini bildirir.</summary>
    public void Configure(Vector3 axis, Transform source = null)
    {
        thrower = source;
        hasHit = false;
        hitPlayer = false;
        spawnTime = Time.time;
        previousPosition = transform.position;
        alignmentElapsed = 0f;
        launchRotation = body != null ? body.rotation : transform.rotation;
        if (axis.sqrMagnitude > 0.0001f)
            spinAxis = axis.normalized;
        if (body != null)
        {
            incomingVelocity = body.linearVelocity;
            // Capture the charge-dependent spin before the short alignment bridge.
            flightSpin = Vector3.Dot(body.angularVelocity, spinAxis);
            body.maxAngularVelocity = Mathf.Max(originalMaxAngularVelocity, Mathf.Abs(flightSpin));
            body.linearDamping = flightLinearDamping;
            body.angularDamping = flightAngularDamping;
            if (lockSpinAxis) body.angularVelocity = Vector3.zero;
        }
    }

    void FixedUpdate()
    {
        if (body != null && !hitPlayer && !body.isKinematic)
        {
            incomingVelocity = body.linearVelocity;
            CheckPlayerHit();
        }
        if (body == null || body.isKinematic || hasHit || !lockSpinAxis)
            return;

        // Only the authoritative Rigidbody steers the flight. Gravity and all
        // translational motion remain physical; aiming does not bend toward a target.
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer) return;
        if (alignmentElapsed >= alignmentDuration)
        {
            // Keep spinning around one fixed axis; do not aim the long edge forward
            // again each frame, which would cancel the rotation. Collision unlocks it.
            body.angularVelocity = spinAxis * flightSpin;
            return;
        }
        if (item == null || body.linearVelocity.sqrMagnitude < 0.01f) return;
        Vector3 direction = body.linearVelocity.normalized;
        Vector3 normal = spinAxis;
        if (Vector3.ProjectOnPlane(direction, normal).sqrMagnitude < 0.0001f) return;
        Quaternion aligned = item.GetAlignedRotation(normal.normalized, direction);
        alignmentElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(alignmentElapsed / alignmentDuration);
        float blend = 1f - Mathf.Pow(1f - t, 3f);
        body.angularVelocity = Vector3.zero;
        Quaternion spin = Quaternion.AngleAxis(flightSpin * alignmentElapsed * Mathf.Rad2Deg, normal.normalized);
        body.MoveRotation(spin * Quaternion.Slerp(launchRotation, aligned, blend));
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
            var collider = GetComponent<Collider>();
            Vector3 half = collider != null ? collider.bounds.extents : Vector3.one * 0.06f;
            bounds.Expand(Vector3.Min(half, Vector3.one * 0.3f) * 2f);
            if (bounds.IntersectRay(ray, out float distance) && distance <= closest)
            { closest = distance; target = player; }
        }
        if (target == null) return;
        // Remote CharacterControllers are disabled: use their calibrated bounds
        // for hits, and a non-alloc physics sweep to reject hits through walls.
        int count = Physics.SphereCastNonAlloc(ray, 0.06f, obstructionHits, closest,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        // Dense piles can fill the small reusable buffer; do not silently drop a valid hit.
        RaycastHit[] hits = obstructionHits;
        if (count == obstructionHits.Length)
        {
            hits = Physics.SphereCastAll(ray, 0.06f, closest, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].collider.transform;
            if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform) ||
                (thrower != null && hit.IsChildOf(thrower))) continue;
            return;
        }
        Vector3 point = ray.GetPoint(closest);
        bool head = target.IsHeadPoint(point);
        target.Hit(body.linearVelocity, head);
        hitPlayer = true;
        RegisterHit(); // One player hit per throw, including a fast ricochet.
        body.linearVelocity *= 0.25f;
    }

    void Update()
    {
        if (body == null || body.isKinematic || Time.time - spawnTime > maxLifeTime)
            Destroy(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        var authority = Unity.Netcode.NetworkManager.Singleton;
        if (authority != null && authority.IsListening && !authority.IsServer) return;
        var target = collision.collider.GetComponentInParent<PlayerKnockdown>();
        if (!hitPlayer && target != null && target.transform != thrower && !target.IsDown && incomingVelocity.sqrMagnitude > 4f)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            target.Hit(incomingVelocity, target.IsHeadPoint(point));
            hitPlayer = true;
        }
        // Ignore residual contacts with the thrower's own rig.
        if (thrower != null && collision.collider.transform.IsChildOf(thrower)) return;
        if (!hasHit && collision.relativeVelocity.sqrMagnitude > 4f)
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) ShopAudio.Play(ShopCue.Impact, transform.position);
            else if (manager.IsServer && thrower != null && thrower.TryGetComponent<NetworkPlayerSetup>(out var player))
                player.PlayCueRpc((int)ShopCue.Impact, transform.position);
        }
        RegisterHit();

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
            body.maxAngularVelocity = originalMaxAngularVelocity;
        }
    }
}
