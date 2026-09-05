using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BookEdgeLines : MonoBehaviour
{
    [Header("Yuzey Birlesim Cizgileri")]
    [Range(1f, 80f)] public float creaseAngle = 18f;
    [Min(0.0001f)] public float lineWidth = 0.012f;
    [Min(0f)] public float surfaceOffset = 0.0015f;
    public Color lineColor = Color.black;

    private const string GeneratedName = "__BookCreaseLines";
    private static Material lineMaterial;

    public static void ApplyToBook(GameObject book)
    {
        if (book == null)
            return;

        BookEdgeLines effect = book.GetComponent<BookEdgeLines>();
        if (effect == null)
            effect = book.AddComponent<BookEdgeLines>();

        effect.Rebuild();
    }

    private void Awake()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        Transform generated = transform.Find(GeneratedName);
        if (generated != null)
            Destroy(generated.gameObject);
    }

    [ContextMenu("Rebuild Book Crease Lines")]
    public void Rebuild()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null)
            Destroy(old.gameObject);

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;
            if (filter.transform == transform && filter.gameObject.name == GeneratedName)
                continue;
            BuildForMesh(filter);
        }
    }

    private void BuildForMesh(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        Vector3[] vertices = source.vertices;
        int[] triangles = source.triangles;
        if (vertices == null || triangles == null || triangles.Length < 6)
            return;

        Dictionary<EdgeKey, EdgeData> edges = new Dictionary<EdgeKey, EdgeData>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
                continue;

            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 0.0000001f)
                continue;
            normal.Normalize();

            AddEdge(edges, vertices[a], vertices[b], normal);
            AddEdge(edges, vertices[b], vertices[c], normal);
            AddEdge(edges, vertices[c], vertices[a], normal);
        }

        List<Vector3> lineVertices = new List<Vector3>();
        List<int> lineIndices = new List<int>();
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;
        Matrix4x4 meshToWorld = filter.transform.localToWorldMatrix;
        float cosLimit = Mathf.Cos(creaseAngle * Mathf.Deg2Rad);

        foreach (KeyValuePair<EdgeKey, EdgeData> pair in edges)
        {
            EdgeData edge = pair.Value;
            if (edge.count != 2)
                continue;

            if (Vector3.Dot(edge.normalA, edge.normalB) > cosLimit)
                continue;

            Vector3 p0World = meshToWorld.MultiplyPoint3x4(edge.pointA);
            Vector3 p1World = meshToWorld.MultiplyPoint3x4(edge.pointB);
            Vector3 nWorld = meshToWorld.MultiplyVector(edge.normalA + edge.normalB).normalized;
            Vector3 tangent = (p1World - p0World).normalized;
            if (tangent.sqrMagnitude < 0.000001f || nWorld.sqrMagnitude < 0.000001f)
                continue;

            Vector3 side = Vector3.Cross(tangent, nWorld).normalized;
            if (side.sqrMagnitude < 0.000001f)
                continue;

            Vector3 offset = nWorld * surfaceOffset;
            Vector3 half = side * (lineWidth * 0.5f);
            int start = lineVertices.Count;

            lineVertices.Add(worldToRoot.MultiplyPoint3x4(p0World + offset - half));
            lineVertices.Add(worldToRoot.MultiplyPoint3x4(p0World + offset + half));
            lineVertices.Add(worldToRoot.MultiplyPoint3x4(p1World + offset + half));
            lineVertices.Add(worldToRoot.MultiplyPoint3x4(p1World + offset - half));

            lineIndices.Add(start);
            lineIndices.Add(start + 1);
            lineIndices.Add(start + 2);
            lineIndices.Add(start);
            lineIndices.Add(start + 2);
            lineIndices.Add(start + 3);
        }

        if (lineVertices.Count == 0)
            return;

        GameObject holder = GetOrCreateHolder();
        Mesh edgeMesh = new Mesh();
        edgeMesh.name = filter.name + "_CreaseLines";
        edgeMesh.SetVertices(lineVertices);
        edgeMesh.SetTriangles(lineIndices, 0);
        edgeMesh.RecalculateBounds();

        GameObject lineObject = new GameObject(filter.name + "_CreaseLines");
        lineObject.transform.SetParent(holder.transform, false);
        MeshFilter lineFilter = lineObject.AddComponent<MeshFilter>();
        lineFilter.sharedMesh = edgeMesh;
        MeshRenderer renderer = lineObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetLineMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private GameObject GetOrCreateHolder()
    {
        Transform holder = transform.Find(GeneratedName);
        if (holder != null)
            return holder.gameObject;

        GameObject go = new GameObject(GeneratedName);
        go.transform.SetParent(transform, false);
        return go;
    }

    private static void AddEdge(Dictionary<EdgeKey, EdgeData> edges, Vector3 p0, Vector3 p1, Vector3 normal)
    {
        EdgeKey key = new EdgeKey(p0, p1);
        EdgeData data;
        if (!edges.TryGetValue(key, out data))
        {
            data = new EdgeData
            {
                pointA = p0,
                pointB = p1,
                normalA = normal,
                count = 1
            };
        }
        else if (data.count == 1)
        {
            data.normalB = normal;
            data.count = 2;
        }
        else
        {
            return;
        }

        edges[key] = data;
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Custom/BookEdgeLine");
            if (shader == null)
            {
                Debug.LogWarning("BookEdgeLines: Custom/BookEdgeLine shader bulunamadi.");
                return null;
            }
            lineMaterial = new Material(shader);
            lineMaterial.name = "BookEdgeLine_Runtime";
        }

        lineMaterial.SetColor("_Color", lineColor);
        return lineMaterial;
    }

    private struct EdgeKey
    {
        private readonly int ax, ay, az, bx, by, bz;

        public EdgeKey(Vector3 first, Vector3 second)
        {
            int fx = Mathf.RoundToInt(first.x * 100000f);
            int fy = Mathf.RoundToInt(first.y * 100000f);
            int fz = Mathf.RoundToInt(first.z * 100000f);
            int sx = Mathf.RoundToInt(second.x * 100000f);
            int sy = Mathf.RoundToInt(second.y * 100000f);
            int sz = Mathf.RoundToInt(second.z * 100000f);

            if (Compare(fx, fy, fz, sx, sy, sz) <= 0)
            {
                ax = fx; ay = fy; az = fz;
                bx = sx; by = sy; bz = sz;
            }
            else
            {
                ax = sx; ay = sy; az = sz;
                bx = fx; by = fy; bz = fz;
            }
        }

        private static int Compare(int ax, int ay, int az, int bx, int by, int bz)
        {
            if (ax != bx) return ax.CompareTo(bx);
            if (ay != by) return ay.CompareTo(by);
            return az.CompareTo(bz);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ax;
                hash = hash * 31 + ay;
                hash = hash * 31 + az;
                hash = hash * 31 + bx;
                hash = hash * 31 + by;
                hash = hash * 31 + bz;
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey)) return false;
            EdgeKey other = (EdgeKey)obj;
            return ax == other.ax && ay == other.ay && az == other.az &&
                   bx == other.bx && by == other.by && bz == other.bz;
        }
    }

    private struct EdgeData
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public Vector3 normalA;
        public Vector3 normalB;
        public int count;
    }
}
