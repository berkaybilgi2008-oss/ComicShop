Shader "Custom/OutlineOnly"
{
    Properties
    {
        _Color ("Outline Color", Color) = (1, 0.86, 0, 1)
        _OffsetFactor ("Offset Factor (dengeyi bozan egim payi)", Range(0, 50)) = 1
        _OffsetUnits ("Offset Units (ana ayar, bunu oyna)", Range(0, 200)) = 20
    }
    SubShader
    {
        // Geometry-1: normal objelerden ONCE ciziliyor, boylece asil kitap
        // ustune cizilince onu kapatabiliyor (asagidaki Offset ile birlikte calisiyor)
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            // URP'nin bu pass'i normal opak cizim akisinin bir parcasi olarak
            // tanimasi icin gerekli etiket.
            Tags { "LightMode" = "UniversalForward" }

            // Artik bu degerler Inspector'dan (Material ayarlarindan) canli
            // olarak degistirilebilir, shader dosyasini tekrar duzenlemene gerek yok.
            Offset [_OffsetFactor], [_OffsetUnits]
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
