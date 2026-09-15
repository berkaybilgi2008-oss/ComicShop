using UnityEngine;

[DisallowMultipleComponent]
public sealed class BookSpawnArea : MonoBehaviour
{
    [Min(0.05f)] public float width = 1f;
    [Min(0.05f)] public float depth = 1f;
    [Min(0f)] public float height = 0.4f;
    public float Area => Mathf.Abs(width * depth * transform.lossyScale.x * transform.lossyScale.z);
    public Vector3 Sample() => transform.TransformPoint(new Vector3(
        Random.Range(-width * 0.5f, width * 0.5f), height, Random.Range(-depth * 0.5f, depth * 0.5f)));

    private void OnDrawGizmos()
    {
        if (!isActiveAndEnabled) return;
        Matrix4x4 previous = Gizmos.matrix;
        Color color = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = isActiveAndEnabled ? new Color(0.1f, 1f, 0.4f, 0.7f) : Color.gray;
        Gizmos.DrawWireCube(new Vector3(0f, height, 0f), new Vector3(width, 0.02f, depth));
        Gizmos.matrix = previous;
        Gizmos.color = color;
    }
}
