// Mongle/SkyGradient — soft pastel daytime sky for the village: top blue -> lavender -> pink horizon, gentle sun glow.
Shader "Mongle/SkyGradient"
{
    Properties
    {
        _Top("Top", Color) = (0.62, 0.78, 1, 1)
        _Mid("Middle", Color) = (0.84, 0.8, 1, 1)
        _Horizon("Horizon", Color) = (1, 0.86, 0.9, 1)
        _Bottom("Below Horizon", Color) = (0.95, 0.9, 0.95, 1)
        _MidHeight("Middle Height", Range(0.02, 0.9)) = 0.22
        _SunDir("Sun Direction", Vector) = (0.4, 0.5, -0.75, 0)
        _SunColor("Sun Glow", Color) = (1, 0.95, 0.85, 1)
        _SunSize("Sun Glow Size", Range(1, 64)) = 10
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _Top, _Mid, _Horizon, _Bottom, _SunColor;
            float _MidHeight, _SunSize;
            float4 _SunDir;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 up = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, _MidHeight, y));
                up = lerp(up, _Top.rgb, smoothstep(_MidHeight, 0.85, y));
                half3 c = y >= 0 ? up : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-y * 5.0));
                float sun = pow(saturate(dot(d, normalize(_SunDir.xyz))), _SunSize);
                c += _SunColor.rgb * sun * 0.35;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
