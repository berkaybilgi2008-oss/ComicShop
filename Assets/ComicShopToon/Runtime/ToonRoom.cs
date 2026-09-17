using UnityEngine;
namespace ComicShop.Rendering
{
    [DisallowMultipleComponent]
    public sealed class ToonRoom : MonoBehaviour
    {
        public LayerMask architectureLayers;
        public string architectureTag = "Architecture";
        public bool includeArchitectureNames = true;
        public Bounds roomBounds = new Bounds(new Vector3(0, 2, 0), new Vector3(20, 4, 12));
        [Min(0.001f)] public float shellOffset = 0.02f;
        [Min(0.05f)] public float shellThickness = 0.2f;
        public enum Facade { NegativeZ, PositiveZ, NegativeX, PositiveX }
        public Facade shopfront = Facade.NegativeZ;
        public bool preserveShopfrontOpening = true;
        [Tooltip("Horizontal offset from room center; vertical offset above room floor, in meters.")]
        public Vector2 openingCenter = new Vector2(0, 2);
        public Vector2 openingSize = new Vector2(4, 3);
        public bool blockFloor = true;
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(roomBounds.center, roomBounds.size);
            Vector3 p = roomBounds.center;
            bool alongX = shopfront == Facade.NegativeZ || shopfront == Facade.PositiveZ;
            bool positive = shopfront == Facade.PositiveZ || shopfront == Facade.PositiveX;
            p.y = roomBounds.min.y + openingCenter.y;
            if (alongX) { p.x += openingCenter.x; p.z = positive ? roomBounds.max.z : roomBounds.min.z; }
            else { p.z += openingCenter.x; p.x = positive ? roomBounds.max.x : roomBounds.min.x; }
            Gizmos.color = Color.yellow;
            if (preserveShopfrontOpening) Gizmos.DrawWireCube(p, alongX ? new Vector3(openingSize.x, openingSize.y, 0.01f) : new Vector3(0.01f, openingSize.y, openingSize.x));
        }
    }
}
