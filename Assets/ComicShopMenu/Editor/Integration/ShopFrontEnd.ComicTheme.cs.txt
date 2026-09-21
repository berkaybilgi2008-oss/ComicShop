using UnityEngine;
using UnityEngine.SceneManagement;

// Same front-end, same authoritative settings/session flow; only the visual layer changes.
public sealed partial class ShopFrontEnd
{
    void ApplyComicTheme()
    {
        var backdrop = new GameObject("Comic print frame", typeof(RectTransform), typeof(ComicShop.ComicPanelGraphic));
        backdrop.transform.SetParent(card, false);
        Stretch((RectTransform)backdrop.transform);
        var graphic = backdrop.GetComponent<ComicShop.ComicPanelGraphic>();
        graphic.color = Paper; graphic.raycastTarget = false;
        backdrop.transform.SetAsFirstSibling();
    }
}

// Remove the duplicate generic UI on each gameplay scene load, including reconnects.
// Do not disable player scripts: the existing controllers already gate input on cursor lock.
public static class ComicProjectMenuOwnership
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<BookSpawner>() == null) return;
        foreach (var pause in Object.FindObjectsByType<ComicShop.ComicPauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            pause.gameObject.SetActive(false);
        foreach (var menu in Object.FindObjectsByType<ComicShop.MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var canvas = menu.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas.gameObject.SetActive(false);
        }
        // ShopAudio and ShopFrontEnd apply master gain themselves; do not multiply it twice.
        AudioListener.volume = 1f;
    }
}
