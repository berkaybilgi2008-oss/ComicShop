using UnityEngine;

/// <summary>
/// Compatibility helpers for systems that expect the older BookItem API.
/// Kept separate so the existing BookItem physics/scale behavior is untouched.
/// </summary>
public static class BookItemCompatibilityExtensions
{
    public static void SetHighlight(this BookItem book, bool on)
    {
        // The current BookItem outline system is managed through BeginHeldOutline/EndHeldOutline.
        // Keep the legacy call as a harmless compatibility no-op.
    }

    public static void GetAxisFrame(this BookItem book,
        out Vector3 coverNormal, out Vector3 longAxis, out Vector3 wideAxis,
        out Vector3 halfExtents)
    {
        coverNormal = Vector3.forward;
        longAxis = Vector3.up;
        wideAxis = Vector3.right;
        halfExtents = new Vector3(0.01f, 0.1f, 0.07f);

        if (book == null)
            return;

        Bounds bounds = new Bounds();
        bool found = false;
        Transform root = book.transform;

        foreach (MeshFilter filter in book.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

            Bounds meshBounds = filter.sharedMesh.bounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    meshBounds.center.x + ((i & 1) == 0 ? -meshBounds.extents.x : meshBounds.extents.x),
                    meshBounds.center.y + ((i & 2) == 0 ? -meshBounds.extents.y : meshBounds.extents.y),
                    meshBounds.center.z + ((i & 4) == 0 ? -meshBounds.extents.z : meshBounds.extents.z));

                Vector3 point = toRoot.MultiplyPoint3x4(corner);
                if (!found)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        if (!found)
        {
            Collider collider = book.GetComponentInChildren<Collider>(true);
            if (collider == null)
                return;

            Vector3 localCenter = root.InverseTransformPoint(collider.bounds.center);
            Vector3 localSize = Vector3.Scale(collider.bounds.size, root.lossyScale.magnitude > 0.0001f ? Vector3.one / root.lossyScale.magnitude : Vector3.one);
            bounds = new Bounds(localCenter, localSize);
        }

        Vector3 size = bounds.size;
        int thin = 0;
        int longest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (size[i] < size[thin]) thin = i;
            if (size[i] > size[longest]) longest = i;
        }

        if (thin == longest)
            longest = (thin + 1) % 3;

        int wide = 3 - thin - longest;
        coverNormal = Axis(thin);
        longAxis = Axis(longest);
        wideAxis = Axis(wide);
        halfExtents = new Vector3(size[thin], size[longest], size[wide]) * 0.5f;
    }

    private static Vector3 Axis(int index)
    {
        return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
    }
}
