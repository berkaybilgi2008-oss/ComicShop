using UnityEngine;

public class BookItem : MonoBehaviour
{
    [Header("Kitap Kimligi")]
    [Min(0)] public int bookID;
    [Min(0)] public int brandID;

    public string DisplayName => $"Book {bookID + 1}";

    [Header("Kenar (Outline) Highlight Ayarlari")]
    public Material outlineMaterial;
    public float outlineScale = 1.05f;

    [Header("Kapak Gorseli")]
    public Renderer coverRenderer;

    [Header("Kitap Temel Rotasyonu")]
    [Tooltip("Bu kitabin elde ve rafta kullanilacak temel rotasyonu. FBX'in Unity'de sahneye suruklendigindeki acisindan baslar; buradan elle degistirebilirsin.")]
    public Vector3 baseRotationEuler;

    [HideInInspector] public Quaternion nativeRotation = Quaternion.identity;
    [HideInInspector] public Quaternion orientationCorrection = Quaternion.identity;

    private GameObject[] outlineObjects;
    private Vector3 originalScale;

    public ShelfSlot currentSlot;
    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;
    public Quaternion NativeRotation => Quaternion.Euler(baseRotationEuler);

    [Header("Birakma Fizigi")]
    [Tooltip("Kitap bu hizdan daha yavas oldugunda sabitlenme sayaci baslar.")]
    public float sleepLinearVelocity = 0.08f;
    [Tooltip("Kitap bu acisal hizdan daha yavas oldugunda sabitlenme sayaci baslar.")]
    public float sleepAngularVelocity = 0.08f;
    [Tooltip("Kitap temas halinde bu kadar sure sakin kalirsa tamamen sabitlenir.")]
    public float sleepDelay = 0.35f;
    private float stillTimer;
    private int physicsContactCount;

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObjects();
    }

    void FixedUpdate()
    {
        if (IsHeld)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        bool movingSlowly =
            rb.linearVelocity.sqrMagnitude <= sleepLinearVelocity * sleepLinearVelocity &&
            rb.angularVelocity.sqrMagnitude <= sleepAngularVelocity * sleepAngularVelocity;

        if (physicsContactCount <= 0 || !movingSlowly)
        {
            stillTimer = 0f;
            return;
        }

        stillTimer += Time.fixedDeltaTime;
        if (stillTimer < sleepDelay)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.Sleep();
        stillTimer = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsHeld)
            physicsContactCount++;
    }

    void OnCollisionStay(Collision collision)
    {
        if (!IsHeld && physicsContactCount <= 0)
            physicsContactCount = 1;
    }

    void OnCollisionExit(Collision collision)
    {
        if (physicsContactCount > 0)
            physicsContactCount--;

        if (physicsContactCount <= 0)
        {
            physicsContactCount = 0;
            stillTimer = 0f;
        }
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    void CreateOutlineObjects()
    {
        if (outlineMaterial == null)
            return;

        MeshFilter[] sourceMeshes = GetComponentsInChildren<MeshFilter>(true);
        outlineObjects = new GameObject[sourceMeshes.Length];

        for (int i = 0; i < sourceMeshes.Length; i++)
        {
            MeshFilter source = sourceMeshes[i];
            if (source.sharedMesh == null)
                continue;

            GameObject outline = new GameObject("Outline_" + i);
            outline.transform.SetParent(source.transform, false);
            outline.transform.localScale = Vector3.one * outlineScale;

            MeshFilter mf = outline.AddComponent<MeshFilter>();
            mf.sharedMesh = source.sharedMesh;

            MeshRenderer mr = outline.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMaterial;
            outline.SetActive(false);
            outlineObjects[i] = outline;
        }
    }

    public void SetHighlight(bool on)
    {
        if (currentSlot != null || IsHeld)
            on = false;

        if (outlineObjects == null)
            return;

        foreach (GameObject outline in outlineObjects)
        {
            if (outline != null)
                outline.SetActive(on);
        }
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;

        if (held)
            SetHighlight(false);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = !held;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;
            physicsContactCount = 0;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

            if (!held)
            {
                rb.detectCollisions = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.maxDepenetrationVelocity = 10f;
                rb.solverIterations = 20;
                rb.solverVelocityIterations = 20;
                rb.WakeUp();
            }
        }
    }
}
