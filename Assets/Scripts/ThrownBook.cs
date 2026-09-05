using UnityEngine;

/// <summary>
/// Sarjli atisla firlatilan kitaba GECICI olarak eklenir.
///
/// Iki isi var:
///   1) Kitap ucarken kendi duzleminde temiz doner (yalpalamaz).
///   2) Firlatilan kitap yerdeki diger kitaplara CARPAR ama onlari SAVURMAZ,
///      boylece yigin dagilmaz ve ayni anda onlarca rigidbody uyanmaz.
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
    [Min(0f)] public float impactAngularDamping = 5f;

    [Header("Temizlik")]
    [Tooltip("Kitap bu sure icinde durmazsa bilesen yine de kendini kaldirir.")]
    [Min(1f)] public float maxLifeTime = 20f;

    private Rigidbody body;
    private float spawnTime;
    private float originalLinearDamping;
    private float originalAngularDamping;
    private Vector3 spinAxis = Vector3.right;
    private bool hasHit;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        spawnTime = Time.time;

        if (body != null)
        {
            originalLinearDamping = body.linearDamping;
            originalAngularDamping = body.angularDamping;

            body.linearDamping = flightLinearDamping;
            body.angularDamping = flightAngularDamping;
        }
    }

    /// <summary>Firlatan taraf donme eksenini bildirir.</summary>
    public void Configure(Vector3 axis)
    {
        if (axis.sqrMagnitude > 0.0001f)
            spinAxis = axis.normalized;
    }

    void FixedUpdate()
    {
        if (body == null || hasHit || !lockSpinAxis)
            return;

        // Acisal hizin eksen disi bileseni atilir -> yalpalama olmaz.
        body.angularVelocity = spinAxis * Vector3.Dot(body.angularVelocity, spinAxis);
    }

    void Update()
    {
        if (body == null || body.isKinematic || Time.time - spawnTime > maxLifeTime)
            Destroy(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        RegisterHit();
        DampenHitBook(collision);

        // ILERIDE: buraya oyuncuya carpma / bayiltma kontrolu gelecek.
        // PlayerController hit = collision.collider.GetComponentInParent<PlayerController>();
        // if (hit != null) { ... }
    }

    void OnCollisionStay(Collision collision)
    {
        DampenHitBook(collision);
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

    /// <summary>Carpilan kitabin hizini sifirlar: temas olur, savrulma olmaz.</summary>
    private void DampenHitBook(Collision collision)
    {
        if (collision.collider == null)
            return;

        BookItem other = collision.collider.GetComponentInParent<BookItem>();
        if (other == null || other.gameObject == gameObject || other.IsHeld)
            return;

        Rigidbody otherBody = other.GetComponent<Rigidbody>();
        if (otherBody == null || otherBody.isKinematic)
            return;

        otherBody.linearVelocity = Vector3.zero;
        otherBody.angularVelocity = Vector3.zero;
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