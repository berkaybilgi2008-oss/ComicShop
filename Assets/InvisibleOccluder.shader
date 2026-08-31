Shader "Custom/InvisibleOccluder"
{
    // Bu shader HICBIR RENK CIZMEZ (tamamen gorunmez) ama derinlik (depth)
    // degerini normal sekilde yazar. Bunu, gercek kitabin duracagi yere
    // "gorunmez bir blok" koymak icin kullaniyoruz -- boylece onun etrafina
    // sardigimiz buyutulmus anahat mesh'i, bu gorunmez blogun disina tasan
    // kismiyla SADECE kenar cizgisi olarak gorunur kalir.
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(0,0,0,0);
            }
            ENDCG
        }
    }
}
