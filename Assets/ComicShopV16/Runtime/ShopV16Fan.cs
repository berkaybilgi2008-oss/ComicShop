using UnityEngine;
namespace ComicShopV16 {
public sealed class ShopV16Fan : MonoBehaviour {
    public float degreesPerSecond;
    void Update() { transform.Rotate(0, degreesPerSecond*Time.deltaTime, 0, Space.Self); }
}
}
