using System.Reflection;
using UnityEngine;

/// <summary>
/// Optional adapter for the separately imported ToastRanger package. The package
/// is not in this repository, so its serialized public fields are resolved once.
/// Inventory and NetworkBook remain the source of truth; no new ownership RPCs.
/// </summary>
[DefaultExecutionOrder(50)] // Interaction (0), inventory bridge (50), ToastBookCarry (100).
[DisallowMultipleComponent]
public sealed class CharacterBookCarryBridge : MonoBehaviour
{
    [Tooltip("Small adjustment in BookSocket coordinates; Hand Height stays on ToastBookCarry.")]
    public Vector3 socketOffset;
    [Tooltip("Toast's socket +Z is the palm normal. Existing stacks use local +Y.")]
    public Vector3 stackRotation = new Vector3(90f, 0f, 0f);
    public bool IsBound => carry != null && carry.isActiveAndEnabled && socket != null && anchor != null && anchor.parent == socket;
    public int CarriedCount { get; private set; }

    private PlayerInteraction interaction;
    private NetworkPlayerSetup networkPlayer;
    private MonoBehaviour carry;
    private FieldInfo carryingField;
    private FieldInfo previewField;
    private GameObject preview;
    private bool previewWasActive;
    private Transform socket, anchor, previousParent;
    private Vector3 previousPosition, previousScale, heldWorldScale;
    private Quaternion previousRotation;
    private bool savedAnchor;
    private float nextResolve;

    private void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        networkPlayer = GetComponent<NetworkPlayerSetup>();
        TryBind();
    }

    private void Update()
    {
        if (!IsBound)
        {
            if (Time.unscaledTime < nextResolve) return;
            nextResolve = Time.unscaledTime + 0.5f;
            Restore();
            if (!TryBind()) return;
        }

        CarriedCount = CountInventory();
        carryingField.SetValue(carry, CarriedCount > 0);
        ApplyAnchorPose();
    }

    private int CountInventory()
    {
        if (networkPlayer != null && networkPlayer.IsSpawned && !networkPlayer.IsOwner)
            return NetworkBook.CountHeldBy(networkPlayer.OwnerClientId);
        int count = 0;
        if (interaction != null)
            foreach (var book in interaction.HeldBooksList)
                if (book != null && book.IsHeld) count++;
        return count;
    }

    private bool TryBind()
    {
        if (interaction == null || interaction.rightHandPoint == null) return false;
        MonoBehaviour candidate = null;
        Transform candidateSocket = null;
        FieldInfo candidateState = null;
        foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || !component.isActiveAndEnabled || component.GetType().Name != "ToastBookCarry") continue;
            var type = component.GetType();
            var socketField = type.GetField("bookSocket", BindingFlags.Public | BindingFlags.Instance);
            var stateField = type.GetField("carryingBook", BindingFlags.Public | BindingFlags.Instance);
            if (socketField == null || stateField == null || stateField.FieldType != typeof(bool)) continue;
            var foundSocket = socketField.GetValue(component) as Transform;
            if (foundSocket == null || !foundSocket.IsChildOf(transform) ||
                foundSocket == interaction.rightHandPoint || foundSocket.IsChildOf(interaction.rightHandPoint)) continue;
            if (candidate != null)
            {
                Debug.LogWarning("Two active ToastBookCarry visuals under Player. Disable the old visual before binding books.", this);
                return false;
            }
            candidate = component; candidateSocket = foundSocket; candidateState = stateField;
        }
        if (candidate == null) return false;

        carry = candidate; socket = candidateSocket; carryingField = candidateState;
        anchor = interaction.rightHandPoint;
        previousParent = anchor.parent; previousPosition = anchor.localPosition;
        previousRotation = anchor.localRotation; previousScale = anchor.localScale;
        heldWorldScale = anchor.lossyScale; savedAnchor = true;
        anchor.SetParent(socket, false);
        ApplyAnchorPose();

        previewField = carry.GetType().GetField("previewBook", BindingFlags.Public | BindingFlags.Instance);
        preview = previewField != null ? previewField.GetValue(carry) as GameObject : null;
        if (preview != null)
        {
            previewWasActive = preview.activeSelf;
            preview.SetActive(false);
            // ToastBookCarry otherwise turns the demo prop back on every frame.
            previewField.SetValue(carry, null);
        }
        CarriedCount = CountInventory();
        carryingField.SetValue(carry, CarriedCount > 0);
        return true;
    }

    private void ApplyAnchorPose()
    {
        anchor.localPosition = socketOffset;
        anchor.localRotation = Quaternion.Euler(stackRotation);
        // Existing held book sizes and stackSpacing were authored on a unit-scale
        // camera anchor. Do not shrink them again with the 0.8 character visual.
        anchor.localScale = Vector3.one;
        Vector3 inherited = anchor.lossyScale;
        anchor.localScale = new Vector3(SafeRatio(heldWorldScale.x, inherited.x),
            SafeRatio(heldWorldScale.y, inherited.y), SafeRatio(heldWorldScale.z, inherited.z));
    }

    private static float SafeRatio(float value, float divisor) => Mathf.Abs(divisor) > 0.00001f ? value / divisor : 1f;

    private void OnDisable() => Restore();

    private void Restore()
    {
        if (savedAnchor && anchor != null)
        {
            anchor.SetParent(previousParent, false);
            anchor.localPosition = previousPosition; anchor.localRotation = previousRotation; anchor.localScale = previousScale;
        }
        if (carry != null)
        {
            carryingField?.SetValue(carry, false);
            if (previewField != null && preview != null)
            {
                previewField.SetValue(carry, preview);
                preview.SetActive(previewWasActive);
            }
        }
        carry = null; socket = null; anchor = null; carryingField = null;
        preview = null; previewField = null; savedAnchor = false; CarriedCount = 0;
    }
}
