// Korstone ember for Orsuun (art direction B, glow sheet 1).
// One shader for two jobs:
//   Gear upgrade glow: _Glow 0 = off (+0..+6), 0.35 = +7, 0.65 = +8, 1 = +9. Thin ember lines in the seams plus an
//   edge rim; at +9 the lines run white-gold. The weapon uses a hotter material than the armor.
//   Korstone: _CrackAlways = 1 lights the cracks regardless of _Glow and fades them toward the top.
// Cracks are a warped 3D Voronoi edge in object space, so any mesh gets them without authored masks.
Shader "Orsuun/EmberGlow"
{
    Properties
    {
        [MainColor] _Tint ("Base Color", Color) = (0.5, 0.5, 0.5, 1)
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 0.36, 0.06, 1)
        [HDR] _HotColor ("Hot Core Color (+9)", Color) = (1.0, 0.86, 0.55, 1)
        _Glow ("Glow (0 off, 0.35 +7, 0.65 +8, 1 +9)", Range(0, 1)) = 0
        _Intensity ("Emission Intensity", Float) = 3.5
        _CrackScale ("Crack Scale", Float) = 6
        _CrackWidth ("Crack Width", Range(0.002, 0.2)) = 0.025
        _CrackAlways ("Cracks Always On (Korstone)", Range(0, 1)) = 0
        _CrackFadeTop ("Crack Fade Height (object Y, 0 = none)", Float) = 0
        _RimPower ("Rim Power", Float) = 3
        _Roughness ("Diffuse Softness", Range(0, 1)) = 0.5
        _BodyGlow ("Whole-Surface Glow Share (weapon)", Range(0, 1)) = 0
        [HideInInspector] _Debug ("Debug output (0 off)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Shared by every pass: one UnityPerMaterial layout keeps per-material values from crossing between materials
        // (borrowing URP Lit's shadow/depth passes mixed in Lit's buffer layout and did exactly that).
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Tint;
            float4 _GlowColor;
            float4 _HotColor;
            float _Glow;
            float _Intensity;
            float _CrackScale;
            float _CrackWidth;
            float _CrackAlways;
            float _CrackFadeTop;
            float _RimPower;
            float _Roughness;
            float _Debug;
            float _BodyGlow;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float3 positionOS : TEXCOORD3;
            half fog : TEXCOORD4;
        };

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float3 Hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            // Distance to the nearest Voronoi cell border (two-pass edge distance).
            float VoronoiEdge(float3 x)
            {
                float3 n = floor(x), f = frac(x);
                float3 mg = 0, mr = 0; float md = 8.0;
                [unroll] for (int k = -1; k <= 1; k++)
                [unroll] for (int j = -1; j <= 1; j++)
                [unroll] for (int i = -1; i <= 1; i++)
                {
                    float3 g = float3(i, j, k);
                    float3 r = g + Hash33(n + g) - f;
                    float d = dot(r, r);
                    if (d < md) { md = d; mr = r; mg = g; }
                }
                md = 8.0;
                [unroll] for (int k2 = -1; k2 <= 1; k2++)
                [unroll] for (int j2 = -1; j2 <= 1; j2++)
                [unroll] for (int i2 = -1; i2 <= 1; i2++)
                {
                    float3 g = mg + float3(i2, j2, k2);
                    float3 r = g + Hash33(n + g) - f;
                    float3 dr = r - mr;
                    if (dot(dr, dr) > 1e-5) md = min(md, dot(0.5 * (mr + r), normalize(dr)));
                }
                return md;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Lit: main light (with shadows) plus ambient probes, softened wrap diffuse.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                if (_Debug > 0.5 && _Debug < 1.5) return half4(albedo.rgb, 1);
                if (_Debug > 1.5 && _Debug < 2.5) return half4(_Glow.xxx, 1);
                if (_Debug > 2.5 && _Debug < 3.5) return half4(SampleSH(N), 1);
                if (_Debug > 3.5) return half4(mainLight.color * mainLight.shadowAttenuation * 0.5, 1);
                half ndl = dot(N, mainLight.direction);
                half wrap = saturate((ndl + _Roughness * 0.5) / (1 + _Roughness * 0.5));
                half3 lit = albedo.rgb * (mainLight.color * wrap * mainLight.shadowAttenuation * mainLight.distanceAttenuation + SampleSH(N));

                // Cracks: warped object-space Voronoi borders; thicker as the glow climbs.
                float3 p = i.positionOS * _CrackScale;
                p += 0.35 * sin(p.yzx * 1.7 + 1.3);
                float edge = VoronoiEdge(p);
                half glow = saturate(_Glow);
                half width = _CrackWidth * lerp(1.0, 2.2, glow);
                half crack = 1 - smoothstep(width * 0.4, width, edge);
                half fade = _CrackFadeTop > 0 ? saturate(1.15 - i.positionOS.y / _CrackFadeTop) : 1;
                half crackAmount = crack * max(_CrackAlways * fade, glow);

                // Rim hugs the silhouette of upgraded gear (not the Korstone).
                half rim = pow(saturate(1 - dot(N, V)), _RimPower) * glow * 0.8;

                // Gentle ember breathing, offset along the height so it rolls upward.
                half flicker = 0.86 + 0.14 * sin(_Time.y * 5.0 - i.positionOS.y * 3.0);

                // +9 lines run gold, never white: the hot mix stops short of the full hot colour.
                half hot = saturate((glow - 0.75) * 3.0) * 0.75;
                half3 lineColor = lerp(_GlowColor.rgb, _HotColor.rgb, hot);
                // Weapon: part of the glow covers the whole surface so the blade is the brightest piece.
                half body = glow * _BodyGlow;
                half3 emission = (_GlowColor.rgb * (rim + body * 0.6)) * _Intensity * flicker;

                // Crack pixels become ember light (replace, not add), so lines stay orange on any base colour.
                half3 color = lerp(lit + emission, lineColor * _Intensity * flicker, crackAmount);
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment NullFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 NullFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
