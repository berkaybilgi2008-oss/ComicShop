using UnityEngine;
namespace ComicShopV16 {
[ExecuteAlways] public sealed class ShopV16Appearance : MonoBehaviour {
    public Material[] materials;
    public Transform[] sources;
    public bool[] directional;
    public Vector4[] colors;
    public Vector4[] parameters;
    public Vector4 ambient;
    readonly Vector4[] positions = new Vector4[32];
    void OnEnable() {
        foreach(var r in GetComponentsInChildren<MeshRenderer>(true)) {
            var p=new MaterialPropertyBlock();r.GetPropertyBlock(p);
            p.SetFloat("_Receive",r.receiveShadows?1:0);r.SetPropertyBlock(p);
        }
        Apply();
    }
    void LateUpdate() { Apply(); }
    void Apply() {
        if (materials == null || sources == null) return;
        int count = Mathf.Min(sources.Length, 32);
        for (int i=0;i<count;i++) {
            if (!sources[i]) continue;
            Vector3 p = directional[i] ? -sources[i].forward : sources[i].position;
            positions[i] = new Vector4(p.x,p.y,p.z,directional[i]?0:1);
        }
        foreach(var m in materials) if(m) {
            m.SetInt("_ShopLightCount",count); m.SetVectorArray("_ShopLightPositions",positions);
            m.SetVectorArray("_ShopLightColors",colors); m.SetVectorArray("_ShopLightParams",parameters);
            m.SetVector("_ShopAmbient",ambient);
        }
    }
}
}
