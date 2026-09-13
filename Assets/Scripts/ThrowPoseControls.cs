using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThrowPoseControls : MonoBehaviour
{
    [Tooltip("Q tutusunu bu hedeflerle yonet. Kapaliysa otomatik poz kullanilir.")]
    public bool manualPose = true;
    [Tooltip("Play'de bir kitap al, bunu ac, Scene penceresinde hedefleri ayarla. Q'ya basili tutmak gerekmez.")]
    public bool previewPose;
    public ThrowPoseSettings settings;
    public Transform handTarget, elbowTarget, bookTarget;
    private Transform targetRoot;

    public bool IsLocal
    {
        get
        {
            var player = GetComponent<NetworkPlayerSetup>();
            return player == null || !player.IsSpawned || player.IsOwner;
        }
    }
    public bool IsManual => enabled && manualPose && handTarget && elbowTarget && bookTarget;
    public bool IsPreview => IsManual && previewPose && IsLocal;

    void Awake() { EnsureTargets(); }

    public void EnsureTargets()
    {
        if (handTarget && elbowTarget && bookTarget) return;
        if (!settings) settings = Resources.Load<ThrowPoseSettings>("ComicShopThrowPoseSettings");
        targetRoot = new GameObject("QPoseTargets").transform;
        targetRoot.SetParent(transform, false);
        handTarget = new GameObject("ElHedefi").transform;
        handTarget.SetParent(targetRoot, false);
        elbowTarget = new GameObject("DirsekHedefi").transform;
        elbowTarget.SetParent(targetRoot, false);
        bookTarget = new GameObject("KitapHedefi").transform;
        bookTarget.SetParent(handTarget, false);
        ApplySaved();
    }

    [ContextMenu("Kayitli Ayarlari Yukle")]
    public void ApplySaved()
    {
        if (!handTarget || !elbowTarget || !bookTarget) return;
        handTarget.localPosition = settings ? settings.handPosition : new Vector3(-0.28f, 1.45f, 0.5f);
        handTarget.localRotation = Quaternion.Euler(settings ? settings.handEuler : new Vector3(-65f, -20f, 0f));
        elbowTarget.localPosition = settings ? settings.elbowPosition : new Vector3(-0.45f, 1.1f, 0.2f);
        bookTarget.localPosition = settings ? settings.bookPosition : new Vector3(0f, -0.06f, 0.025f);
        bookTarget.localRotation = Quaternion.Euler(settings ? settings.bookEuler : Vector3.zero);
    }

    public bool GetBookPose(BookItem book, Transform upper, Transform lower, Transform wrist,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero; rotation = Quaternion.identity;
        if (!IsManual || !book || !upper || !lower || !wrist) return false;
        float first = Vector3.Distance(upper.position, lower.position);
        float second = Vector3.Distance(lower.position, wrist.position);
        Vector3 delta = handTarget.position - upper.position;
        float reach = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + 0.001f,
            Mathf.Max(0.002f, first + second - 0.001f));
        Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : transform.forward;
        Vector3 reachableHand = upper.position + direction * reach;
        // No shoulder translation and no bone stretching. A remote/wall-constrained
        // pose can recover this same grip relation from the book's world pose.
        position = reachableHand + bookTarget.position - handTarget.position;
        rotation = bookTarget.rotation * book.NativeRotation;
        return true;
    }

    public void GetWristFromBook(BookItem book, out Vector3 position, out Quaternion rotation)
    {
        rotation = book.transform.rotation * Quaternion.Inverse(book.NativeRotation) *
            Quaternion.Inverse(bookTarget.localRotation);
        position = book.transform.position - rotation *
            Vector3.Scale(bookTarget.localPosition, handTarget.lossyScale);
    }

    void OnDrawGizmos()
    {
        if (!handTarget || !elbowTarget || !bookTarget) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(handTarget.position, 0.035f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(elbowTarget.position, 0.04f);
        Gizmos.DrawLine(elbowTarget.position, handTarget.position);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(bookTarget.position, 0.025f);
        Gizmos.DrawLine(handTarget.position, bookTarget.position);
    }
}
