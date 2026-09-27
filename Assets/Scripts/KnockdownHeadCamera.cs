using UnityEngine;

/// <summary>
/// Yere dusunce (Q ile vurulma) oyuncunun kendi kamerasi karakterin KAFASINA kilitlenir:
/// dusus ve ragdoll boyunca gozlerinden bakar (sirt ustu yatinca tavani gorur), kendi
/// yatan govdesini disaridan gormez. Kalkinca kamera normal yerine yumusakca doner.
/// Yalnizca sahibinin kamerasini etkiler; ag/fizik davranisi degismez.
/// </summary>
[DefaultExecutionOrder(1100)] // HeadHitKnockdownAnimation (1000) pozu uyguladiktan sonra.
[DisallowMultipleComponent]
public sealed class KnockdownHeadCamera : MonoBehaviour
{
    [Tooltip("Kalkistan sonra kameranin normal yerine donus suresi (saniye).")]
    [Min(0.01f)] public float returnDuration = 0.25f;

    PlayerKnockdown knockdown;
    HeadHitKnockdownAnimation headHit;
    NetworkPlayerSetup network;
    Transform view, head;
    bool attached;
    Vector3 eyeInHead;
    Quaternion viewInHead;
    Vector3 standingLocalPosition;
    Quaternion standingLocalRotation;
    float releaseTimer = -1f;
    Vector3 releasePosition;
    Quaternion releaseRotation;

    void Awake()
    {
        knockdown = GetComponent<PlayerKnockdown>();
        headHit = GetComponent<HeadHitKnockdownAnimation>();
        network = GetComponent<NetworkPlayerSetup>();
        var camera = GetComponentInChildren<Camera>(true);
        view = camera != null ? camera.transform : null;
    }

    bool Local => network == null || !network.IsSpawned || network.IsOwner;

    Transform FindHead()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if ((t.name == "Head" || t.name.EndsWith(":Head")) && t.GetComponentInParent<Animator>() != null
                && t.gameObject.activeInHierarchy)
                return t;
        return null;
    }

    void LateUpdate()
    {
        if (view == null || knockdown == null || !Local) { attached = false; return; }
        if (headHit == null) headHit = GetComponent<HeadHitKnockdownAnimation>();
        bool down = knockdown.IsDown || (headHit != null && headHit.IsAnimating);

        if (down && !attached)
        {
            if (head == null || !head.gameObject.activeInHierarchy) head = FindHead();
            if (head == null) return;
            // Kameranin o anki (goz) pozu kafaya rijit baglanir.
            standingLocalPosition = view.localPosition;
            standingLocalRotation = view.localRotation;
            eyeInHead = head.InverseTransformPoint(view.position);
            viewInHead = Quaternion.Inverse(head.rotation) * view.rotation;
            attached = true;
            releaseTimer = -1f;
        }

        if (attached && down && head != null)
        {
            view.SetPositionAndRotation(head.TransformPoint(eyeInHead), head.rotation * viewInHead);
            return;
        }

        if (attached && !down)
        {
            attached = false;
            releaseTimer = 0f;
            releasePosition = view.localPosition;
            releaseRotation = view.localRotation;
        }

        if (releaseTimer >= 0f)
        {
            releaseTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(releaseTimer / returnDuration));
            view.localPosition = Vector3.Lerp(releasePosition, standingLocalPosition, t);
            view.localRotation = Quaternion.Slerp(releaseRotation, standingLocalRotation, t);
            if (t >= 1f) releaseTimer = -1f;
        }
    }
}
