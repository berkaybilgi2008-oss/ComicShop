using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BookEdgeLines : MonoBehaviour
{
    [Header("Yuzey Birlesim Cizgileri")]
    [Range(1f, 80f)] public float creaseAngle = 12f;
    [Min(0.0001f)] public float lineWidth = 0.012f;
    [Min(0f)] public float surfaceOffset = 0.0008f;
    [Min(0.000001f)] public float vertexWeldTolerance = 0.00005f;
    public Color lineColor = Color.black;

    private const string GeneratedName = "__BookCreaseLines";
    private static Material lineMaterial;

    public static void ApplyToBook(GameObject book)
    {
        if (book == null) return;
        BookEdgeLines effect = book.GetComponent<BookEdgeLines>();
        if (effect == null) effect = book.AddComponent<BookEdgeLines>();
        effect.Rebuild();
    }

    private void Awake() => Rebuild();

    private void OnDestroy()
    {
        Transform generated = transform.Find(GeneratedName);
        if (generated != null) Destroy(generated.gameObject);
    }

    [ContextMenu("Rebuild Book Crease Lines")]
    public void Rebuild()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null) Destroy(old.gameObject);

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            BuildForMesh(filter);
        }
    }

    private void BuildForMesh(MeshFilter filter)
    {
        Mesh source = filter.sharedMesh;
        Vector3[] vertices = source.vertices;
        int[] triangles = source.triangles;
        if (vertices == null || triangles == null || triangles.Length < 6) return;

        // FBX meshes commonly split the same geometric corner into several
        // vertex indices because of UVs, normals or material seams. Weld by
        // position before constructing the edge adjacency graph.
        Dictionary<PositionKey, int> welded = new Dictionary<PositionKey, int>();
        int[] weldedVertex = new int[vertices.Length];
        List<Vector3> positions = new List<Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            PositionKey key = new PositionKey(vertices[i], vertexWeldTolerance);
            int id;
            if (!welded.TryGetValue(key, out id))
            {
                id = positions.Count;
                welded.Add(key, id);
                positions.Add(vertices[i]);
            }
            weldedVertex[i] = id;
        }

        Dictionary<EdgeKey, EdgeData> edges = new Dictionary<EdgeKey, EdgeData>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length) continue;

            int wa = weldedVertex[a];
            int wb = weldedVertex[b];
            int wc = weldedVertex[c];
            if (wa == wb || wb == wc || wc == wa) continue;

            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 0.0000001f) continue;
            normal.Normalize();

            AddEdge(edges, wa, wb, normal);
            AddEdge(edges, wb, wc, normal);
            AddEdge(edges, wc, wa, normal);
        }

        List<Vector3> lineVertices = new List<Vector3>();
        List<int> lineIndices = new List<int>();
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;
        Matrix4x4 meshToWorld = filter.transform.localToWorldMatrix;
        float cosLimit = Mathf.Cos(creaseAngle * Mathf.Deg2Rad);

        foreach (KeyValuePair<EdgeKey, EdgeData> pair in edges)
        {
            EdgeData edge = pair.Value;
            if (edge.count != 2) continue;
            if (Vector3.Dot(edge.normalA, edge.normalB) > cosLimit) continue;

            Vector3 p0World = meshToWorld.MultiplyPoint3x4(positions[pair.Key.a]);
            Vector3 p1World = meshToWorld.MultiplyPoint3x4(positions[pair.Key.b]);
            Vector3 nA = meshToWorld.MultiplyVector(edge.normalA).normalized;
            Vector3 nB = meshToWorld.MultiplyVector(edge.normalB).normalized;
            Vector3 tangent = p1World - p0World;
            if (tangent.sqrMagnitude < 0.0000001f) continue;
            tangent.Normalize();

            Vector3 bisector = nA + nB;
            if (bisector.sqrMagnitude < 0.000001f) bisector = nA;
            bisector.Normalize();

            Vector3 side = Vector3.Cross(tangent, bisector).normalized;
            if (side.sqrMagnitude < 0.000001f) continue;

            Vector3 offset = bisector * surfaceOffset;
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

        if (lineVertices.Count == 0) return;

        GameObject holder = GetOrCreateHolder();
        Mesh edgeMesh = new Mesh();
        edgeMesh.name = filter.name + "_CreaseLines";
        edgeMesh.SetVertices(lineVertices);
        edgeMesh.SetTriangles(lineIndices, 0);
        edgeMesh.RecalculateBounds();

        GameObject lineObject = new GameObject(filter.name + "_CreaseLines");
        lineObject.transform.SetParent(holder.transform, false);
        lineObject.AddComponent<MeshFilter>().sharedMesh = edgeMesh;
        MeshRenderer renderer = lineObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetLineMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private GameObject GetOrCreateHolder()
    {
        Transform holder = transform.Find(GeneratedName);
        if (holder != null) return holder.gameObject;
        GameObject go = new GameObject(GeneratedName);
        go.transform.SetParent(transform, false);
        return go;
    }

    private static void AddEdge(Dictionary<EdgeKey, EdgeData> edges, int a, int b, Vector3 normal)
    {
        EdgeKey key = new EdgeKey(a, b);
        EdgeData data;
        if (!edges.TryGetValue(key, out data))
        {
            data = new EdgeData { normalA = normal, count = 1 };
        }
        else if (data.count == 1)
        {
            data.normalB = normal;
            data.count = 2;
        }
        else return;
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

    private struct PositionKey
    {
        private readonly int x, y, z;
        public PositionKey(Vector3 p, float tolerance)
        {
            x = Mathf.RoundToInt(p.x / tolerance);
            y = Mathf.RoundToInt(p.y / tolerance);
            z = Mathf.RoundToInt(p.z / tolerance);
        }
        public override int GetHashCode()
        {
            unchecked { return (x * 73856093) ^ (y * 19349663) ^ (z * 83492791); }
        }
        public override bool Equals(object obj)
        {
            if (!(obj is PositionKey)) return false;
            PositionKey other = (PositionKey)obj;
            return x == other.x && y == other.y && z == other.z;
        }
    }

    private struct EdgeKey
    {
        public int a, b;
        public EdgeKey(int first, int second)
        {
            if (first < second) { a = first; b = second; }
            else { a = second; b = first; }
        }
        public override int GetHashCode()
        {
            unchecked { return (a * 397) ^ b; }
        }
        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey)) return false;
            EdgeKey other = (EdgeKey)obj;
            return a == other.a && b == other.b;
        }
    }

    private struct EdgeData
    {
        public Vector3 normalA, normalB;
        public int count;
    }
}
