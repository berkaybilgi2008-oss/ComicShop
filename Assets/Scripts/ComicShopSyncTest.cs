using UnityEngine;

public static class ComicShopSyncTest
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void ConfirmProjectSync()
    {
        Debug.Log("COMICSHOP_SYNC_TEST_2026_09_07 — YENI KOD UNITY'YE AKTARILDI");
    }
}
