using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight UI shape used by the shop loading screen.
/// The original project treats this as a Graphic-like component, so keep the
/// public API stable while using Unity's Image renderer underneath.
/// </summary>
public sealed class HudShape : Image
{
    public static readonly Color Ink = new Color32(20, 10, 12, 255);

    public enum Frame
    {
        None,
        Menu
    }

    public void Set(Color color, Frame frame, float cut, bool dots, bool shadow)
    {
        this.color = color;
        raycastTarget = false;
    }
}
