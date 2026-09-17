using UnityEngine;
<<<<<<< ours

namespace ComicShopV16
{
    // Serialized import metadata. Lighting is now evaluated by URP Light components.
    // Keep these fields so existing prefabs and the importer remain compatible.
    [ExecuteAlways]
    public sealed class ShopV16Appearance : MonoBehaviour
    {
        public Material[] materials;
        public Transform[] sources;
        public bool[] directional;
        public Vector4[] colors;
        public Vector4[] parameters;
        public Vector4 ambient;

        public static void ConfigureLight(Light light, bool sun, bool pendant, bool neon, bool shadows)
        {
            light.enabled = true;
            light.type = sun ? LightType.Directional : pendant ? LightType.Spot : LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.useColorTemperature = false;
            light.color = neon ? new Color(1f, .3f, .5f) : sun
                ? new Color(1f, .96f, .86f) : new Color(1f, .81f, .60f);
            light.intensity = sun ? 1.15f : pendant ? 1.35f : neon ? .35f : .55f;
            light.range = pendant ? 6f : neon ? 2.5f : 3.5f;
            light.spotAngle = 115f;
            light.innerSpotAngle = 85f;
            light.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            light.shadowStrength = 1f;
            light.shadowBias = .05f;
            light.shadowNormalBias = .08f;
            if (pendant) light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void OnEnable()
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                block.Clear();
                renderer.GetPropertyBlock(block);
                block.SetFloat("_Receive", renderer.receiveShadows ? 1f : 0f);
                renderer.SetPropertyBlock(block);
            }
=======
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
            if (!r.sharedMaterial || r.sharedMaterial.shader.name != "ComicShop/V16 Source Toon") continue;
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
        foreach(var m in materials) if(m && m.shader.name == "ComicShop/V16 Source Toon") {
            m.SetInt("_ShopLightCount",count); m.SetVectorArray("_ShopLightPositions",positions);
            m.SetVectorArray("_ShopLightColors",colors); m.SetVectorArray("_ShopLightParams",parameters);
            m.SetVector("_ShopAmbient",ambient);
>>>>>>> theirs
        }
    }
}
