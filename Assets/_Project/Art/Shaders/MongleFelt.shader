// Mongle/Felt — soft felt/fuzz look for URP without needing UVs.
// Procedural 3D fiber noise perturbs the normal and tone, a warm rim gives the fuzzy halo,
// and a soft terminator fill keeps shaded sides from going flat or muddy.
// Optional patchwork texture (D-009, uv0): RGB = fabric colour (sRGB), A = stitch height, bumped with screen-space
// derivatives (Mikkelsen surface gradient) so no tangents are needed.
Shader "Mongle/Felt"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (0.7, 0.5, 0.35, 1)
        [MainTexture] _BaseMap("Patch Texture (RGB colour, A stitch height)", 2D) = "white" {}
        [MaterialToggle] _UseBaseMap("Use Patch Texture", Float) = 0
        _StitchBump("Stitch Bump", Range(0, 2)) = 0
        [HideInInspector] _Cutoff("Cutoff", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.06
        [Header(Fuzz)]
        _FuzzScale("Fuzz Scale (cells per unit)", Float) = 110
        _FuzzNormal("Fuzz Normal Strength", Range(0, 1.5)) = 0.45
        _FuzzTone("Fuzz Tone Variation", Range(0, 0.5)) = 0.12
        _FuzzSpeck("Fiber Speckle", Range(0, 0.5)) = 0.10
        [Toggle] _WorldSpace("Use World Space (static props)", Float) = 0
        [Header(Soft light)]
        _RimColor("Rim Color", Color) = (1, 0.9, 0.78, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.4
        _RimStrength("Rim Strength", Range(0, 2)) = 0.35
        _Subsurface("Soft Shade Fill", Range(0, 1)) = 0.32
        _SubsurfaceColor("Soft Shade Color", Color) = (1, 0.72, 0.6, 1)
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex FeltVertex
            #pragma fragment FeltFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _Smoothness;
                float _FuzzScale;
                half _FuzzNormal;
                half _FuzzTone;
                half _FuzzSpeck;
                half _WorldSpace;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Subsurface;
                half4 _SubsurfaceColor;
                half4 _EmissionColor;
                half _UseBaseMap;
                half _StitchBump;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 fuzzPos : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float2 uv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float VNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), f.x),
                               lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), f.x),
                               lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            float Fuzz(float3 p)
            {
                return VNoise(p) * 0.6 + VNoise(p * 2.31 + 7.1) * 0.4;
            }

            Varyings FeltVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.fuzzPos = (_WorldSpace > 0.5) ? vp.positionWS : input.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 FeltFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 n0 = normalize(input.normalWS);
                float3 p = input.fuzzPos * _FuzzScale;

                // patchwork texture: colour + stitch bump from the alpha height (surface gradient, no tangents)
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half useTex = step(0.5, _UseBaseMap);
                half3 baseColor = _BaseColor.rgb * lerp(half3(1, 1, 1), tex.rgb, useTex);
                float height = tex.a * _StitchBump * useTex * 0.002; // metres: ~1-2 mm raised stitches
                float3 dpdx = ddx(input.positionWS), dpdy = ddy(input.positionWS);
                float3 r1 = cross(dpdy, n0), r2 = cross(n0, dpdx);
                float det = dot(dpdx, r1);
                float3 surfGrad = sign(det) * (ddx(height) * r1 + ddy(height) * r2);
                float3 nb = abs(det) * n0 - surfGrad;
                n0 = (useTex > 0.5 && _StitchBump > 0 && dot(nb, nb) > 1e-20) ? normalize(nb) : n0;

                // fiber height field and its gradient (finite differences)
                const float e = 0.4;
                float h = Fuzz(p);
                float3 g = float3(Fuzz(p + float3(e, 0, 0)) - h, Fuzz(p + float3(0, e, 0)) - h, Fuzz(p + float3(0, 0, e)) - h) / e;
                g -= dot(g, n0) * n0;
                float3 n = normalize(n0 - g * _FuzzNormal);

                half3 albedo = baseColor * (1.0 + (h - 0.5) * 2.0 * _FuzzTone);
                float speck = smoothstep(0.74, 0.96, VNoise(p * 5.7 + 3.7));
                albedo += _RimColor.rgb * speck * _FuzzSpeck;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.emission = _EmissionColor.rgb;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, s);

                // soft terminator fill + fuzzy rim, both driven by the main light and ambient
                Light main = GetMainLight(inputData.shadowCoord);
                float ndl = dot(n0, main.direction);
                float soft = saturate(1.0 - ndl) * saturate(ndl + 0.6);
                half shadow = lerp(main.shadowAttenuation, 1.0, 0.5);
                half3 fill = albedo * _SubsurfaceColor.rgb * main.color * (soft * _Subsurface * shadow);

                float ndv = saturate(dot(n, inputData.viewDirectionWS));
                float fres = pow(1.0 - ndv, _RimPower);
                half3 lightIn = main.color * saturate(ndl * 0.5 + 0.5) * shadow + SampleSH(n0);
                half3 rim = _RimColor.rgb * lightIn * (fres * _RimStrength) * albedo * 1.6;

                color.rgb += fill + rim;
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack "Universal Render Pipeline/Lit"
}
