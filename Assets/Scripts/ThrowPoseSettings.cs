using UnityEngine;

[CreateAssetMenu(menuName = "ComicShop/Q Throw Pose", fileName = "ComicShopThrowPoseSettings")]
public sealed class ThrowPoseSettings : ScriptableObject
{
    public Vector3 handPosition = new Vector3(-0.28f, 1.45f, 0.5f);
    public Vector3 handEuler = new Vector3(-65f, -20f, 0f);
    public Vector3 elbowPosition = new Vector3(-0.45f, 1.1f, 0.2f);
    public Vector3 bookPosition = new Vector3(0f, -0.06f, 0.025f);
    public Vector3 bookEuler = Vector3.zero;
}
