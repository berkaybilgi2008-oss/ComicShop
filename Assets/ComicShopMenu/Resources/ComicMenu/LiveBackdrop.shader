Shader "ComicShop/UI/LiveBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MotionAmount ("Motion", Range(0,1)) = 1
        _MenuTime ("Unscaled time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float _MenuTime, _MotionAmount;
            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            float spot(float2 p, float2 center, float2 size)
            {
                float2 d = (p - center) / size;
                return pow(saturate(1 - dot(d,d)), 2);
            }
            // Door wings retain their outer hinges and frame; only their interior
            // perspective shifts. All sampling stays inside the existing painting.
            float2 door(float2 p, float4 bounds, float phase, float t)
            {
                float2 q = (p - bounds.xy) / (bounds.zw - bounds.xy);
                float mask = smoothstep(0, .09, q.y) * (1 - smoothstep(.91, 1, q.y));
                mask *= step(0,q.x) * step(q.x,1) * step(0,q.y) * step(q.y,1);
                float swing = sin(t * .65 + phase) * .065 * _MotionAmount;
                p.x += mask * sin(q.x * 3.14159265) * swing * (bounds.z - bounds.x);
                p.y += mask * sin(q.x * 3.14159265) * swing * .018;
                return p;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Pixel coordinates are normalized from the artwork's top-left.
                float2 p = float2(i.uv.x, 1-i.uv.y);
                float t = _MenuTime;
                float2 samplePoint = p;
                // Roofline polygon leaves buildings, title lettering and halftone stationary.
                float roof = p.x < .07 ? lerp(.155,.045,saturate(p.x/.07)) : lerp(.045,.232,saturate((p.x-.07)/.535));
                float sky = (1-smoothstep(roof-.025,roof-.008,p.y)) * (1-smoothstep(.58,.615,p.x));
                sky *= smoothstep(0,.025,p.x) * smoothstep(0,.018,p.y);
                samplePoint.x += sky * sin(t*.10 + p.y*2) * .012 * _MotionAmount;
                samplePoint.y += sky * sin(t*.075 + p.x*3) * .002 * _MotionAmount;
                samplePoint = door(samplePoint,float4(.104,.526,.171,.847),0,t);
                samplePoint = door(samplePoint,float4(.179,.534,.244,.839),1.6,t);
                fixed4 c = tex2D(_MainTex,float2(samplePoint.x,1-samplePoint.y)) + _TextureSampleAdd;
                float pulse = .5+.5*sin(t*1.4)*sin(t*.83+1.3);
                float lamps = spot(p,float2(.633,.372),float2(.037,.079));
                lamps += spot(p,float2(.230,.164),float2(.021,.024));
                lamps += spot(p,float2(.307,.189),float2(.022,.024));
                lamps += spot(p,float2(.378,.213),float2(.022,.024));
                // Low amplitude warmth, never an on/off strobe.
                c.rgb += lamps * (.035+.045*pulse) * float3(1,.64,.19) * _MotionAmount;
                float neon = spot(p,float2(.566,.546),float2(.024,.034));
                c.rgb += neon * (.025+.035*sin(t*2.1)*sin(t*1.3)) * float3(1,.13,.16) * _MotionAmount;
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
