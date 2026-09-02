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

    [Header("Model Yonu")]
    [Tooltip("Modelin kendi eksenlerini standart yatay kitap yonune getiren duzeltme. Olcegi degistirmez.")]
    public Quaternion orientationCorrection = Quaternion.identity;

    private GameObject outlineObject;
    private Vector3 originalScale;

    public ShelfSlot currentSlot;
    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;
    public Quaternion OrientationCorrection => orientationCorrection;

    [Header("Birakma Fizigi")]
    public float sleepLinearVelocity = 0.03f;
    public float sleepAngularVelocity = 0.03f;
    public float sleepDelay = 0.25f;
    private float stillTimer;

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObject();
    }

    void Update()
    {
        if (IsHeld)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        if (rb.linearVelocity.sqrMagnitude <= sleepLinearVelocity * sleepLinearVelocity &&
            rb.angularVelocity.sqrMagnitude <= sleepAngularVelocity * sleepAngularVelocity)
        {
            stillTimer += Time.deltaTime;
            if (stillTimer >= sleepDelay)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                stillTimer = 0f;
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    void CreateOutlineObject()
    {
        MeshFilter sourceMeshFilter = GetComponentInChildren<MeshFilter>();
        if (sourceMeshFilter == null || outlineMaterial == null)
            return;

        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(sourceMeshFilter.transform, false);
        outlineObject.transform.localScale = Vector3.one * outlineScale;

        MeshFilter mf = outlineObject.AddComponent<MeshFilter>();
        mf.mesh = sourceMeshFilter.sharedMesh;

        MeshRenderer mr = outlineObject.AddComponent<MeshRenderer>();
        mr.material = outlineMaterial;
        outlineObject.SetActive(false);
    }

    public void SetHighlight(bool on)
    {
        if (outlineObject != null)
            outlineObject.SetActive(on);
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = !held;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
