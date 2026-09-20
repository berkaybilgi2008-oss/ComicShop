using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(20000)]
public sealed class ComicProjectCursor : MonoBehaviour
{
    RawImage graphic; RectTransform rect;
    bool ownsSystemCursor;
    void Awake() {graphic=GetComponent<RawImage>();rect=(RectTransform)transform;if(graphic)graphic.raycastTarget=false;}
    void LateUpdate()
    {
        if(!graphic || !rect) { ReleaseSystemCursor(); return; }
        var player=NetworkPlayerSetup.LocalPlayer;
        // Owner-only lookup; re-evaluated after respawn or session changes.
        if(player && player.IsOwner) {
            var crosshair=player.GetComponent<Crosshair>();
            if(crosshair)crosshair.enabled=ComicInterfaceOptions.CrosshairVisible && Cursor.lockState==CursorLockMode.Locked;
        }
        bool visible=Application.isFocused && Cursor.lockState!=CursorLockMode.Locked && graphic.texture;
        graphic.enabled=visible;
        if(!visible) { if(ownsSystemCursor)Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;ownsSystemCursor=false;return; }
        Vector2 pos=ComicShop.InputCompat.MousePosition();
        bool inside=pos.x>=0 && pos.y>=0 && pos.x<=Screen.width && pos.y<=Screen.height;
        graphic.enabled=inside;Cursor.visible=!inside;ownsSystemCursor=inside;
        if(!inside)return;
        transform.SetAsLastSibling();rect.position=pos;
        // EventSystem already owns UI hit testing. Never raycast again from LateUpdate:
        // settings pages can have destroyed their old Graphics earlier in this frame.
        bool pressed=ComicShop.InputCompat.MouseDown();
        float scale=ComicInterfaceOptions.LargeCursor?1.45f:1f;
        if(!ComicInterfaceOptions.ReducedMotion && pressed)scale*=.9f;
        rect.localScale=Vector3.one*scale;
        graphic.color=pressed?new Color(1,.78f,.35f):Color.white;
    }
    void ReleaseSystemCursor() {if(ownsSystemCursor)Cursor.visible=Cursor.lockState!=CursorLockMode.Locked;ownsSystemCursor=false;}
    void OnDisable() {ReleaseSystemCursor();}
}

