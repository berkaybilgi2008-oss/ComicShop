using UnityEngine;
using UnityEngine.SceneManagement;

namespace ComicShopV16
{
    /// <summary>
    /// Dukkanin disina kitap firlatilamasin.
    ///
    /// V16 dukkaninda vitrin/kapi camlarinin collider'i yok ve yan/arka duvarlar ile
    /// tavan SIFIR kalinlikta duzlem MeshCollider. 200 km/s'lik donen Q atisi ince bir
    /// ucgen duzlemden (ozellikle donerken) gecebiliyor, camdan ise hic engelsiz cikiyor.
    ///
    /// Cozum: oda icinin hemen disina, her yuzeye bitisik KALIN (1 m) gorunmez BoxCollider
    /// kabugu eklenir. Ic hacim degismez (duvar yuzeyleriyle ayni hizada baslar), bu yuzden
    /// raflar, spawn koridorlari ve oyuncu hareketi etkilenmez; cam/kapi artik kati.
    /// Tum oyuncularda ayni sahneden ayni sekilde kurulur; ag senkronu gerekmez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopBookContainment : MonoBehaviour
    {
        // shop-v16.shopdata "room" bloku: genislik (X), derinlik (Z), yukseklik (Y).
        // Degerler dukkan kokunun YEREL uzayindadir; sahnedeki olcek otomatik uygulanir.
        public Vector3 interiorSize = new Vector3(22.5f, 4.2f, 36f);
        public Vector3 interiorCenter = new Vector3(0f, 2.1f, 0f);
        [Min(0.2f)] public float shellThickness = 1f;
        public bool includeFloor = true;

        const string ShellName = "Book Containment Shell (runtime)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InstallAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InstallAll();

        static void InstallAll()
        {
            foreach (var shop in FindObjectsByType<ShopV16Appearance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var containment = shop.GetComponent<ShopBookContainment>();
                if (containment == null) containment = shop.gameObject.AddComponent<ShopBookContainment>();
                containment.Build();
            }
        }

        void Build()
        {
            if (!Application.isPlaying) return;
            Transform existing = transform.Find(ShellName);
            if (existing != null) return;

            var shell = new GameObject(ShellName).transform;
            shell.SetParent(transform, false);
            shell.gameObject.layer = gameObject.layer;
            shell.gameObject.isStatic = true;

            float t = Mathf.Max(0.2f, shellThickness);
            Vector3 half = interiorSize * 0.5f;
            Vector3 c = interiorCenter;
            // Kenarlarda bosluk kalmasin diye her levha kalinlik kadar uzatilir.
            float w = interiorSize.x + 2f * t, h = interiorSize.y + 2f * t, d = interiorSize.z + 2f * t;

            AddWall(shell, "Left", c + new Vector3(-half.x - t * 0.5f, 0f, 0f), new Vector3(t, h, d));
            AddWall(shell, "Right", c + new Vector3(half.x + t * 0.5f, 0f, 0f), new Vector3(t, h, d));
            AddWall(shell, "Front (glass/door)", c + new Vector3(0f, 0f, -half.z - t * 0.5f), new Vector3(w, h, t));
            AddWall(shell, "Back", c + new Vector3(0f, 0f, half.z + t * 0.5f), new Vector3(w, h, t));
            AddWall(shell, "Ceiling", c + new Vector3(0f, half.y + t * 0.5f, 0f), new Vector3(w, t, d));
            if (includeFloor)
                AddWall(shell, "Floor", c + new Vector3(0f, -half.y - t * 0.5f, 0f), new Vector3(w, t, d));
        }

        static void AddWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(interiorCenter, interiorSize);
        }
    }
}
