using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BookWalkSurface : MonoBehaviour
{
    public const int BookLayer = 8;

    [Header("Yurume Yuzeyi")]
    [Min(0.01f)] public float rampWidth = 0.12f;
    [Min(0.001f)] public float topInset = 0.02f;
    [Range(0.1f, 0.99f)] public float minimumUpDot = 0.78f;

    private static readonly HashSet<BookWalkSurface> activeSurfaces = new HashSet<BookWalkSurface>();
    private static readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    private BookItem book;
    private BoxCollider physicsBox;
    private MeshCollider walkCollider;
    private Mesh walkMesh;
    private bool playerCollisionIgnored;
    private bool lastUsable;
    private float nextCollisionRefresh;

    public bool IsUsable
    {
        get
        {
            if (book == null || physicsBox == null || walkCollider == null || !walkCollider.enabled)
                return false;

            if (book.IsHeld || book.currentSlot != null || !book.IsFrozenAtRest)
                return false;

            return Vector3.Dot(transform.up, Vector3.up) >= minimumUpDot;
        }
    }

    void Awake()
    {
        book = GetComponent<BookItem>();
        physicsBox = GetComponent<BoxCollider>();
        if (book == null || physicsBox == null)
            return;

        BuildWalkCollider();
    }

    void OnEnable()
    {
        activeSurfaces.Add(this);
        RefreshPlayerCollision(true);
    }

    void OnDisable()
    {
        RestorePlayerCollision();
        activeSurfaces.Remove(this);
    }

    void FixedUpdate()
    {
        if (Time.time < nextCollisionRefresh)
            return;

        nextCollisionRefresh = Time.time + 0.10f;
        bool usable = IsUsable;

        if (usable != lastUsable)
        {
            RefreshPlayerCollision(true);
            lastUsable = usable;
        }
    }

    private void BuildWalkCollider()
    {
        Transform child = transform.Find("BookWalkSurface");
        GameObject go;

        if (child != null)
        {
            go = child.gameObject;
            walkCollider = go.GetComponent<MeshCollider>();
            if (walkCollider == null)
                walkCollider = go.AddComponent<MeshCollider>();
        }
        else
        {
            go = new GameObject("BookWalkSurface");
            go.transform.SetParent(transform, false);
            walkCollider = go.AddComponent<MeshCollider>();
        }

        go.layer = BookLayer;
        walkCollider.convex = true;
        walkCollider.isTrigger = true;

        walkMesh = BuildFrustumMesh(physicsBox);
        walkMesh.name = "BookWalkSurfaceMesh";
        walkCollider.sharedMesh = walkMesh;
        walkCollider.enabled = true;
    }

    private Mesh BuildFrustumMesh(BoxCollider box)
    {
        Vector3 half = box.size * 0.5f;
        Vector3 center = box.center;

        float insetX = Mathf.Min(Mathf.Max(topInset, 0.005f), half.x * 0.45f);
        float insetZ = Mathf.Min(Mathf.Max(topInset, 0.005f), half.z * 0.45f);
        float rampX = Mathf.Min(rampWidth, Mathf.Max(0.005f, half.x - insetX));
        float rampZ = Mathf.Min(rampWidth, Mathf.Max(0.005f, half.z - insetZ));

        Vector3[] vertices =
        {
            center + new Vector3(-half.x, -half.y, -half.z),
            center + new Vector3(-half.x, -half.y,  half.z),
            center + new Vector3( half.x, -half.y,  half.z),
            center + new Vector3( half.x, -half.y, -half.z),

            center + new Vector3(-half.x + rampX, half.y, -half.z + rampZ),
            center + new Vector3(-half.x + rampX, half.y,  half.z - rampZ),
            center + new Vector3( half.x - rampX, half.y,  half.z - rampZ),
            center + new Vector3( half.x - rampX, half.y, -half.z + rampZ)
        };

        int[] triangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5,
            0, 1, 5, 0, 5, 4,
            3, 7, 6, 3, 6, 2
        };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void RefreshPlayerCollision(bool force)
    {
        bool shouldIgnore = IsUsable;
        if (!force && shouldIgnore == playerCollisionIgnored)
            return;

        if (physicsBox == null)
            return;

        foreach (Collider player in playerColliders)
        {
            if (player != null)
                Physics.IgnoreCollision(physicsBox, player, shouldIgnore);
        }

        playerCollisionIgnored = shouldIgnore;
    }

    private void RestorePlayerCollision()
    {
        if (physicsBox == null)
            return;

        foreach (Collider player in playerColliders)
        {
            if (player != null)
                Physics.IgnoreCollision(physicsBox, player, false);
        }

        playerCollisionIgnored = false;
    }

    public static void RegisterPlayer(Collider player)
    {
        if (player == null)
            return;

        playerColliders.Add(player);

        foreach (BookWalkSurface surface in activeSurfaces)
        {
            if (surface != null && surface.physicsBox != null)
                Physics.IgnoreCollision(surface.physicsBox, player, surface.IsUsable);
        }
    }

    public static void UnregisterPlayer(Collider player)
    {
        if (player != null)
            playerColliders.Remove(player);
    }

    public static bool TryGetTopSurface(
        Vector3 playerPosition,
        CharacterController controller,
        float maxRise,
        float maxDrop,
        out float topY)
    {
        topY = 0f;
        if (controller == null)
            return false;

        float feetY = playerPosition.y + controller.center.y - controller.height * 0.5f;
        float originY = feetY + Mathf.Max(0.15f, maxRise + 0.12f);
        float distance = Mathf.Max(0.5f, maxRise + maxDrop + 0.35f);

        Vector3 origin = new Vector3(playerPosition.x, originY, playerPosition.z);
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            distance,
            1 << BookLayer,
            QueryTriggerInteraction.Collide);

        bool found = false;
        float highest = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            BookWalkSurface surface = hit.collider != null
                ? hit.collider.GetComponent<BookWalkSurface>()
                : null;

            if (surface == null || !surface.IsUsable)
                continue;

            if (Vector3.Dot(hit.normal, Vector3.up) < surface.minimumUpDot)
                continue;

            float candidate = hit.point.y;
            float delta = candidate - feetY;

            if (delta > maxRise || delta < -maxDrop)
                continue;

            if (!found || candidate > highest)
            {
                highest = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        topY = highest;
        return true;
    }
}
