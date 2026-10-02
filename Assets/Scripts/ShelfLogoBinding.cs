using UnityEngine;

/// <summary>
/// Optional publisher/logo metadata attached to a shelf slot.
/// The visual logo is intentionally optional; ShelfSlot only requires the
/// stable publisher identifier.
/// </summary>
public sealed class ShelfLogoBinding : MonoBehaviour
{
    [Min(0)]
    public int PublisherID;
}
