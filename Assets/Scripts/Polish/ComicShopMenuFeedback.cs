using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One EventSystem owns clicks; presentation never invokes a Button a second time.
[RequireComponent(typeof(RectTransform), typeof(Button))]
public sealed class ComicShopMenuFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    RectTransform rect;
    Button button;
    Graphic graphic;
    Vector3 baseScale;
    Quaternion baseRotation;
    Color baseColor;
    bool hovered, selected, pressed;
    void Awake()
    {
        rect = (RectTransform)transform; button = GetComponent<Button>(); graphic = button.targetGraphic;
        baseScale = rect.localScale; baseRotation = rect.localRotation;
        baseColor = graphic != null ? graphic.color : Color.white;
    }
    void Update()
    {
        bool active = button.IsInteractable() && (hovered || selected);
        float scale = ComicInterfaceOptions.ReducedMotion ? 1f : pressed && active ? .96f : active ? 1.035f : 1f;
        float blend = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        rect.localScale = Vector3.Lerp(rect.localScale, baseScale * scale, blend);
        rect.localRotation = Quaternion.Slerp(rect.localRotation,
            baseRotation * Quaternion.Euler(0, 0, active && !ComicInterfaceOptions.ReducedMotion ? -1f : 0f), blend);
        if (graphic != null) graphic.color = Color.Lerp(graphic.color,
            active ? Color.Lerp(baseColor, Color.white, .13f) : baseColor, blend);
    }
    void OnDisable()
    {
        hovered = selected = pressed = false;
        if (rect == null) return;
        rect.localScale = baseScale; rect.localRotation = baseRotation;
        if (graphic != null) graphic.color = baseColor;
    }
    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
}
