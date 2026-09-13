using UnityEngine;

// Generic rig two-bone correction, applied after animation. No leg/root changes.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class ToastBookCarry : MonoBehaviour
{
    public Animator animator;
    public Transform upperArm, forearm, hand, bookSocket;
    public bool carryingBook;
    [Tooltip("Offset in model metres. Uses the visual root's uniform scale.")]
    [Range(-0.10f,0.16f)] public float handHeight;
    [Min(0.01f)] public float transitionSeconds=0.15f;
    public GameObject previewBook;
    PlayerInteraction inventory;
    NetworkPlayerSetup networkPlayer;

    [Header("Q Atis Kolu")]
    public Transform leftUpperArm, leftForearm, leftHand;

    [Header("Q Atis Pozu - Cerceveleme")]
    [Tooltip("Elin kameraya gore hedefi. Degerler KOL UZUNLUGUNUN katidir, " +
             "boylece karakter olcegi degisse de cerceve bozulmaz. " +
             "X = yan (isaret Throw Hand'den gelir), Y = yukari, Z = ileri.")]
    public Vector3 throwHandView = new Vector3(0.26f, -0.34f, 0.45f);
    [Tooltip("Sarj yeni basladiginda kolun acisi. Eksi deger = geriye yuklenme.")]
    public float throwWindupAngle = 2f;
    [Tooltip("Bar tamamen dolunca varilan aci. Cok eksi yaparsan kitap ekrandan cikar.")]
    public float throwChargedAngle = -10f;
    [Tooltip("Savurmanin bittigi aci. Kitap burada elden cikar.")]
    public float throwReleaseAngle = 40f;
    [Tooltip("Dirsegin kacacagi yon: X = disa (isaret ele gore), Y = yukari, Z = ileri. " +
             "Referans videodaki gibi dirsek asagida ve onde kalir, on kol yukari bakar.")]
    public Vector3 elbowPoleBias = new Vector3(0.45f, -0.75f, 0.45f);
    [Tooltip("Elin omuzdan uzanabilecegi mesafe (kol uzunlugunun orani). " +
             "1'e yaklastikca kol dumduz uzanir, dirsek bukumu kaybolur.")]
    [Range(0.6f, 0.99f)] public float armReachFraction = 0.93f;
    [Tooltip("Kitabin mercege yaklasabilecegi en kisa mesafe (kol uzunlugunun orani).")]
    [Range(0.05f, 1f)] public float lensClearance = 0.26f;

    [Header("Q Atis Pozu - Tutus")]
    [Tooltip("Kitabin on koldan geriye yatma acisi. Buyuk deger = kitap daha cok arkaya yatar.")]
    public float bookLeanAngle = 10f;
    [Tooltip("Kapagin kameraya donme acisi. 0 = kapak tam karsiya bakar, " +
             "90 = kitabi ince kenarindan goruruz.")]
    public float bookFaceAngle = 46f;
    [Tooltip("Avucun bilek ekseninden kaymasi (model metresi). Parmaklarin kitaba " +
             "oturdugu nokta burasi.")]
    public Vector3 throwGripOffset = new Vector3(0f, -0.03f, 0.04f);
    [Tooltip("El kitabin boyunun ne kadar asagisindan tutar. 1 = tam alt kose.")]
    [Range(0.4f, 1f)] public float gripAlongFraction = 0.82f;
    [Tooltip("Kitabin ele gore yan kaymasi. Eksi = kitap ekranin disina dogru uzanir " +
             "(nisangah acik kalir), arti = ekranin ortasina dogru uzanir.")]
    [Range(-1f, 1f)] public float gripAcrossFraction = -0.3f;
    [Tooltip("Bilegin on koldan sapabilecegi en buyuk aci. Kucultursen el daha dogal " +
             "durur ama kitaba tam oturmaz.")]
    [Range(0f, 120f)] public float wristBendLimit = 72f;
    [Tooltip("El kemiginin ekseni modelden modele degisir. El ters/yamuk duruyorsa " +
             "burayi 90'ar derece cevirerek duzelt.")]
    public Vector3 wristTwistEuler = Vector3.zero;

    private Quaternion leftWristRest, rightWristRest;
    private Transform throwUpper, throwLower, throwHand;
    private Quaternion throwUpperBase, throwLowerBase, throwHandBase;
    private Quaternion lastUpperPose, lastLowerPose, lastHandPose;
    private bool throwApplied;
    private float throwBlend;
    private Vector3 throwWristTarget;
    private Quaternion throwWristRotation = Quaternion.identity;
    private int throwPoseFrame = -1;
    float blend;
    bool applied;
    Quaternion upperBase, lowerBase, handBase;
    static readonly int Carry=Animator.StringToHash("Carry");

    void Awake()
    {
        inventory = GetComponentInParent<PlayerInteraction>();
        networkPlayer = GetComponentInParent<NetworkPlayerSetup>();
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "LeftUpperArm" && !leftUpperArm) leftUpperArm = bone;
            if (bone.name == "LeftForearm" && !leftForearm) leftForearm = bone;
            if (bone.name == "LeftHand" && !leftHand) leftHand = bone;
        }
        leftWristRest = leftHand ? leftHand.localRotation : Quaternion.identity;
        rightWristRest = hand ? hand.localRotation : Quaternion.identity;
        // Procedural arms can leave the bounds baked into the idle clip. Keep
        // skinning/bounds updated when the torso itself is outside the camera.
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;
    }

    void ReadInventory()
    {
        // Visuals can be instantiated before being parented under Player.
        if (!inventory)
        {
            inventory = GetComponentInParent<PlayerInteraction>();
            networkPlayer = GetComponentInParent<NetworkPlayerSetup>();
        }
        if (!inventory) return; // Standalone model previews keep their manual toggle.
        if (networkPlayer && networkPlayer.IsSpawned && !networkPlayer.IsOwner)
            carryingBook = NetworkBook.CountHeldBy(networkPlayer.OwnerClientId) >
                (NetworkBook.FindThrowingBook(networkPlayer.OwnerClientId, out _) != null ? 1 : 0);
        else
            carryingBook = inventory.HeldBooksList.Count > (inventory.IsThrowPoseActive ? 1 : 0);
    }

    void Update()
    {
        RestoreThrow();
        Restore();
        ReadInventory();
        if (!animator) return;
        blend=Mathf.MoveTowards(blend,carryingBook?1f:0f,Time.deltaTime/Mathf.Max(.01f,transitionSeconds));
        animator.SetFloat(Carry,blend);
        if (previewBook) previewBook.SetActive(carryingBook && blend>.8f);
    }
    void LateUpdate()
    {
        ApplyCarryPose();
        ApplyThrowArm();
    }

    void ApplyCarryPose()
    {
        // ThrowArc can finish after Update. Clear the Carry parameter that same
        // frame, independent of socket binding, and skip the old hand correction.
        ReadInventory();
        if (inventory && !carryingBook)
        {
            Restore();
            blend = 0f;
            if (animator) animator.SetFloat(Carry, 0f);
            if (previewBook) previewBook.SetActive(false);
            return;
        }
        if (!animator || !animator.enabled || !upperArm || !forearm || !hand || blend<=0f) return;
        float scale=Mathf.Abs(transform.lossyScale.y);
        float offset=Mathf.Clamp(handHeight,-.10f,.16f)*scale*blend;
        if (Mathf.Abs(offset)<.00001f) return;
        upperBase=upperArm.localRotation;lowerBase=forearm.localRotation;handBase=hand.localRotation;
        Quaternion wristRotation=hand.rotation;
        Vector3 a=upperArm.position,b=forearm.position,c=hand.position;
        Vector3 target=c+transform.up*offset;
        float l1=Vector3.Distance(a,b),l2=Vector3.Distance(b,c);
        if(l1<.0001f||l2<.0001f) return;
        Vector3 delta=target-a;float distance=delta.magnitude;
        if(distance<.0001f)return;
        Vector3 direction=delta/distance;
        float reach=Mathf.Clamp(distance,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
        target=a+direction*reach;
        Vector3 bend=Vector3.ProjectOnPlane(b-a,direction);
        if(bend.sqrMagnitude<.0000001f) bend=Vector3.ProjectOnPlane(-transform.forward,direction);
        if(bend.sqrMagnitude<.0000001f) bend=Vector3.ProjectOnPlane(transform.right,direction);
        bend.Normalize();
        float along=(l1*l1+reach*reach-l2*l2)/(2f*reach);
        float across=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        Vector3 elbow=a+direction*along+bend*across;
        upperArm.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upperArm.rotation;
        forearm.rotation=Quaternion.FromToRotation(hand.position-forearm.position,target-forearm.position)*forearm.rotation;
        hand.rotation=wristRotation;
        applied=true;
    }

    private bool SelectThrowArm(bool left, out Transform a, out Transform b, out Transform c)
    {
        a = left ? leftUpperArm : upperArm;
        b = left ? leftForearm : forearm;
        c = left ? leftHand : hand;
        return a && b && c;
    }

    /// <summary>Kol kemikleri ve kamera hazirsa poz bu scriptten uretilir.</summary>
    public bool HasThrowArm(bool left)
    {
        return SelectThrowArm(left, out _, out _, out _) && inventory && inventory.playerCamera;
    }

    /// <summary>Sarj ve savurma ilerlemesinden kolun yay acisi.</summary>
    public float ThrowSwingAngle(float charge, float release)
    {
        float windup = Mathf.Lerp(throwWindupAngle, throwChargedAngle, Mathf.Clamp01(charge));
        return Mathf.Lerp(windup, throwReleaseAngle, Mathf.Clamp01(release));
    }

    private float ArmLength(Transform a, Transform b, Transform c)
    {
        return Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
    }

    private void GetBookFrame(BookItem book, Quaternion rotation, Vector3 scale,
        out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half)
    {
        book.GetAxisFrame(out Vector3 localCover, out Vector3 localAlong, out Vector3 localWide, out half);
        cover = (rotation * localCover).normalized;
        along = (rotation * localAlong).normalized;
        wide = (rotation * localWide).normalized;
        // Olculer OriginalScale'e gore alindi; sarj sirasinda kitap buyuyebiliyor.
        float reference = book.OriginalScale.magnitude;
        if (reference > 0.0001f) half *= scale.magnitude / reference;
    }

    /// <summary>
    /// Q pozunun tamami: once elin nerede duracagi, sonra kitabin o ele gore yeri.
    /// Kitap eli takip eder -- ters yon (kitap cekip kolu zorlamak) artik yok.
    /// </summary>
    public bool GetThrowPose(BookItem book, float angle, bool left, Vector3 scale,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!book || !SelectThrowArm(left, out var upper, out var lower, out var wrist)) return false;
        Camera camera = inventory ? inventory.playerCamera : null;
        if (!camera) return false;

        Transform lens = camera.transform;
        float armLength = ArmLength(upper, lower, wrist);
        if (armLength < 0.0001f) return false;
        float side = left ? -1f : 1f;
        Vector3 shoulder = upper.position;

        // 1) Elin kameradaki yeri. Referans videodaki gibi alt kenardan giren bir
        //    on kol: el ekranin alt kosesinde, kitap yukari dogru uzaniyor.
        Vector3 framed = lens.position
            + lens.right * (throwHandView.x * side * armLength)
            + lens.up * (throwHandView.y * armLength)
            + lens.forward * (throwHandView.z * armLength);

        // 2) Savurma omuz etrafinda doner; boylece her acida kol erisir durumda kalir.
        Vector3 rest = framed - shoulder;
        if (rest.sqrMagnitude < 0.000001f) return false;
        float radius = Mathf.Min(rest.magnitude, armLength * armReachFraction);
        Vector3 direction = (Quaternion.AngleAxis(angle, lens.right) * rest.normalized).normalized;
        Vector3 palm = shoulder + direction * radius;

        // 3) Kitabin ekseni: on koldan geriye yatik, kapak kameraya donuk.
        Vector3 longAxis = (Quaternion.AngleAxis(-bookLeanAngle, lens.right) * direction).normalized;
        Vector3 face = Quaternion.AngleAxis(bookFaceAngle * side, longAxis) * -lens.forward;
        Vector3 coverNormal = Vector3.ProjectOnPlane(face, longAxis);
        if (coverNormal.sqrMagnitude < 0.000001f) coverNormal = Vector3.ProjectOnPlane(lens.right, longAxis);
        if (coverNormal.sqrMagnitude < 0.000001f) return false;
        coverNormal.Normalize();
        rotation = book.GetAlignedRotation(coverNormal, longAxis);

        // 4) Kitabin merkezini avuca gore kur: avuc alt kosede, kitap yukari ve
        //    ekranin ortasina dogru uzanir.
        GetBookFrame(book, rotation, scale, out _, out _, out Vector3 wide, out Vector3 half);
        Vector3 inward = lens.right * -side;
        if (Vector3.Dot(wide, inward) < 0f) wide = -wide;
        Vector3 bookOffset = longAxis * (half.y * gripAlongFraction) + wide * (half.z * gripAcrossFraction);

        // 5) Mercek payi: kitap lensin dibine girerse tum poz ileri itilir, el de
        //    onunla beraber gider.
        float clearance = lensClearance * armLength;
        float depth = Vector3.Dot(palm + bookOffset - lens.position, lens.forward);
        if (depth < clearance) palm += lens.forward * (clearance - depth);

        // 6) Bilek: avuc noktasindan bilek eklemine geri say, sonra omzun erisimine kis.
        Quaternion grip = Quaternion.LookRotation(-coverNormal, -longAxis) * Quaternion.Euler(wristTwistEuler);
        Vector3 gripOffset = grip * Vector3.Scale(throwGripOffset, wrist.lossyScale);
        Vector3 wristTarget = palm - gripOffset;
        Vector3 delta = wristTarget - shoulder;
        float maximum = armLength * armReachFraction;
        if (delta.magnitude > maximum)
        {
            Vector3 clamped = shoulder + delta.normalized * maximum;
            palm += clamped - wristTarget;
            wristTarget = clamped;
        }

        position = palm + bookOffset;
        throwWristTarget = wristTarget;
        throwWristRotation = grip;
        throwPoseFrame = Time.frameCount;
        return true;
    }

    /// <summary>Duvar/oda sinirlamasi kitabi kaydirinca el de ayni kadar kayar.</summary>
    public void OffsetThrowWrist(Vector3 offset)
    {
        if (throwPoseFrame == Time.frameCount) throwWristTarget += offset;
    }

    /// <summary>
    /// Uzak oyuncularda sadece kitabin dunya pozu gelir. Ayni tutustan geri
    /// hesaplayarak bilegi buluruz, boylece herkeste ayni gorunur.
    /// </summary>
    private bool DeriveWristFromBook(BookItem book, bool left, out Vector3 wristTarget, out Quaternion wristRotation)
    {
        wristTarget = Vector3.zero;
        wristRotation = Quaternion.identity;
        if (!book || !SelectThrowArm(left, out var upper, out _, out var wrist)) return false;

        Vector3 bookPosition = book.transform.position;
        GetBookFrame(book, book.transform.rotation, book.transform.lossyScale,
            out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half);

        Vector3 head = upper.position + transform.up * (Vector3.Distance(upper.position, wrist.position) * 0.25f);
        if (Vector3.Dot(along, bookPosition - upper.position) < 0f) along = -along;
        if (Vector3.Dot(cover, head - bookPosition) < 0f) cover = -cover;
        float side = left ? -1f : 1f;
        if (Vector3.Dot(wide, transform.right * -side) < 0f) wide = -wide;

        Vector3 palm = bookPosition - along * (half.y * gripAlongFraction) - wide * (half.z * gripAcrossFraction);
        wristRotation = Quaternion.LookRotation(-cover, -along) * Quaternion.Euler(wristTwistEuler);
        wristTarget = palm - wristRotation * Vector3.Scale(throwGripOffset, wrist.lossyScale);
        return true;
    }

    private void ApplyThrowArm()
    {
        if (!animator || !animator.enabled || !inventory) return;
        bool left = inventory.throwHand == PlayerInteraction.ThrowHand.Left;
        BookItem book = inventory.IsThrowPoseActive ? inventory.ActiveHeldBook : null;
        if (networkPlayer && networkPlayer.IsSpawned && !networkPlayer.IsOwner)
            book = NetworkBook.FindThrowingBook(networkPlayer.OwnerClientId, out left);
        throwBlend = Mathf.MoveTowards(throwBlend, book != null ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));
        if (throwBlend <= 0f) return;
        if (book != null)
        {
            if (!SelectThrowArm(left, out throwUpper, out throwLower, out throwHand)) return;
        }
        if (!throwUpper || !throwLower || !throwHand) return;
        throwUpperBase = throwUpper.localRotation;
        throwLowerBase = throwLower.localRotation;
        throwHandBase = throwHand.localRotation;
        if (book != null)
        {
            Vector3 target;
            Quaternion wristRotation;
            if (throwPoseFrame == Time.frameCount)
            {
                target = throwWristTarget;
                wristRotation = throwWristRotation;
            }
            else if (!DeriveWristFromBook(book, left, out target, out wristRotation)) return;
            if (!SolveThrowElbow(target, left)) return;
            // Bilek on koldan kopmasin: kitaba dogru donerken sinirli sapma.
            Quaternion rest = left ? leftWristRest : rightWristRest;
            throwHand.rotation = Quaternion.RotateTowards(throwLower.rotation * rest, wristRotation, wristBendLimit);
            lastUpperPose = throwUpper.localRotation;
            lastLowerPose = throwLower.localRotation;
            lastHandPose = throwHand.localRotation;
        }
        throwUpper.localRotation = Quaternion.Slerp(throwUpperBase,lastUpperPose,throwBlend);
        throwLower.localRotation = Quaternion.Slerp(throwLowerBase,lastLowerPose,throwBlend);
        throwHand.localRotation = Quaternion.Slerp(throwHandBase,lastHandPose,throwBlend);
        throwApplied = true;
    }

    private bool SolveThrowElbow(Vector3 target, bool left)
    {
        Vector3 a = throwUpper.position, b = throwLower.position, c = throwHand.position;
        float l1 = Vector3.Distance(a,b), l2 = Vector3.Distance(b,c);
        if (l1 < 0.0001f || l2 < 0.0001f || (target-a).sqrMagnitude < 0.000001f) return false;
        Vector3 direction = (target-a).normalized;
        float reach = Mathf.Clamp(Vector3.Distance(a,target), Mathf.Abs(l1-l2)+0.0001f, l1+l2-0.0001f);
        target = a + direction * reach;
        // Dirsek asagida ve onde kalir, on kol yukari bakar. Kol yukari uzanirken
        // duz "asagi" kutup dejenere oluyordu; sirali yedekler onu engelliyor.
        Vector3 outward = left ? -transform.right : transform.right;
        Vector3 pole = Vector3.ProjectOnPlane(
            outward * elbowPoleBias.x + transform.up * elbowPoleBias.y + transform.forward * elbowPoleBias.z,
            direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(outward, direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(-transform.forward, direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(-transform.up, direction);
        if (pole.sqrMagnitude < 0.000001f) return false;
        float along = (l1*l1+reach*reach-l2*l2)/(2f*reach);
        Vector3 elbow = a + direction*along + pole.normalized*Mathf.Sqrt(Mathf.Max(0f,l1*l1-along*along));
        throwUpper.rotation = Quaternion.FromToRotation(b-a,elbow-a)*throwUpper.rotation;
        throwLower.rotation = Quaternion.FromToRotation(throwHand.position-throwLower.position,target-throwLower.position)*throwLower.rotation;
        return true;
    }

    private void RestoreThrow()
    {
        if (!throwApplied) return;
        if (throwUpper) throwUpper.localRotation = throwUpperBase;
        if (throwLower) throwLower.localRotation = throwLowerBase;
        if (throwHand) throwHand.localRotation = throwHandBase;
        throwApplied = false;
    }

    void Restore()
    {
        if(!applied)return;
        if(upperArm)upperArm.localRotation=upperBase;
        if(forearm)forearm.localRotation=lowerBase;
        if(hand)hand.localRotation=handBase;
        applied=false;
    }
    void OnDisable() { RestoreThrow(); throwBlend=0f; Restore(); if(animator)animator.SetFloat(Carry,0);blend=0; if(previewBook)previewBook.SetActive(false); }
    public void SetCarrying(bool value) { carryingBook=value; }
}
