using UnityEngine;

// Shared by local targeting and authoritative RPC validation.
public static class GameplayPhysics
{
    private static readonly RaycastHit[] reachHits = new RaycastHit[128];
    private static bool SupportsClosestPoint(Collider collider) => collider is BoxCollider ||
        collider is SphereCollider || collider is CapsuleCollider ||
        (collider is MeshCollider mesh && mesh.convex);

    public static bool TryClosestPoint(Collider collider, Vector3 point, out Vector3 closest)
    {
        closest = point;
        if (collider == null || !collider.enabled) return false;
        if (SupportsClosestPoint(collider)) { closest = collider.ClosestPoint(point); return true; }
        Vector3 direction = collider.bounds.center - point;
        if (direction.sqrMagnitude < 0.000001f) return false;
        if (!collider.Raycast(new Ray(point, direction.normalized), out var hit,
                direction.magnitude + collider.bounds.extents.magnitude + 0.01f)) return false;
        closest = hit.point;
        return true;
    }

    public static bool SurfaceStillTouches(Collider collider, Vector3 point, Vector3 outwardNormal, float tolerance)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        if (SupportsClosestPoint(collider))
            return (collider.ClosestPoint(point) - point).sqrMagnitude <= tolerance * tolerance;
        // ClosestPoint is not supported for a concave MeshCollider. Test the actual
        // triangle/terrain surface, not its AABB (which can bridge holes in a floor).
        Vector3 normal = outwardNormal.normalized;
        return collider.Raycast(new Ray(point + normal * tolerance, -normal), out var hit, tolerance * 2f)
            && (hit.point - point).sqrMagnitude <= tolerance * tolerance;
    }

    // Etkilesim menzili (metre). Prefab'ta daha kucuk kayitli olsa bile bu degerin altina inmez.
    public const float MinInteractRange = 5f;
    // Hedefin hemen onundeki ince parcalar (raf dudagi, cita, cam) engel sayilmaz.
    public const float ObstacleTolerance = 0.25f;

    // Kitaplar, raf gozleri, kitaplik govdesi/tabelasi ve diger oyuncular erisimi engellemez.
    // Duvar, kapi, tezgah gibi gercek engeller engellemeye devam eder.
    public static bool IsSoftObstacle(Collider collider, ShelfSlot targetSlot)
    {
        if (collider == null) return true;
        if (collider.GetComponentInParent<BookItem>() != null) return true;
        if (collider.GetComponentInParent<ShelfSlot>() != null) return true;
        if (collider.GetComponentInParent<ShelfLogoBinding>() != null) return true;
        if (collider.GetComponentInParent<PlayerInteraction>() != null) return true;
        var furniture = targetSlot != null ? targetSlot.transform.parent : null;
        return furniture != null && collider.transform.IsChildOf(furniture);
    }

    public static bool CanReach(Transform actor, Vector3 eye, Collider target, float range)
    {
        if (!TryClosestPoint(target, eye, out var point)) return false;
        float distance = (point - eye).magnitude;
        if (float.IsNaN(distance) || distance > range) return false;
        if (distance < 0.001f) return true;
        var targetBook = target.GetComponentInParent<BookItem>();
        var targetSlot = target.GetComponentInParent<ShelfSlot>();
        if (targetSlot == null && targetBook != null) targetSlot = targetBook.currentSlot;
        if (ClearPath(actor, eye, point, target, targetSlot)) return true;
        // En yakin nokta bir raf tahtasinin kenarina denk gelebilir; hedefin ortasini da dene.
        Vector3 center = target.bounds.center;
        return (center - point).sqrMagnitude > 0.0001f && ClearPath(actor, eye, center, target, targetSlot);
    }

    static bool ClearPath(Transform actor, Vector3 eye, Vector3 point, Collider target, ShelfSlot targetSlot)
    {
        Vector3 delta = point - eye;
        float distance = delta.magnitude;
        if (distance < 0.001f) return true;
        int count = Physics.RaycastNonAlloc(eye, delta / distance, reachHits, distance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == reachHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = reachHits[i].collider;
            if (hit == target || hit.transform.IsChildOf(actor)) continue;
            if (reachHits[i].distance >= distance - ObstacleTolerance) continue;
            if (IsSoftObstacle(hit, targetSlot)) continue;
            return false;
        }
        return true;
    }
}
