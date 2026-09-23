// Orsuun gear and Korstone shader (art direction B).
// Gear upgrade glow, in the manner of classic MMO upgrade shine (owner, 23 Sep 2026): every piece glows by its own
//   level. _Glow 0 = off (+0..+6), 0.35 = +7, 0.65 = +8, 1 = +9. Three layers, all scaled by the level: a soft aura
//   around the silhouette (second pass), light that flows up over the surface, and a sweep of shine running up the
//   piece. Colour steps from pale gold (+7) to gold (+8) to hot ember-gold (+9). The weapon material is hotter.
// Korstone: _CrackAlways = 1 lights warped 3D Voronoi cracks in object space and fades them toward the top.
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
        [HDR] _Color7 ("+7 Colour", Color) = (1.0, 0.92, 0.72, 1)
        [HDR] _Color8 ("+8 Colour", Color) = (1.0, 0.72, 0.28, 1)
        [HDR] _Color9 ("+9 Colour", Color) = (1.0, 0.46, 0.10, 1)
        _FlowScale ("Flow Scale", Float) = 3.5
        _FlowSpeed ("Flow Speed", Float) = 0.6
        _AuraWidth ("Aura Width (object units at +9)", Float) = 0.035
        _AuraStrength ("Aura Strength", Float) = 1.2
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
            float4 _Color7;
            float4 _Color8;
            float4 _Color9;
            float _FlowScale;
            float _FlowSpeed;
            float _AuraWidth;
            float _AuraStrength;
        CBUFFER_END

        // Level colour: pale gold at +7, gold at +8, hot ember-gold at +9.
        float3 LevelColor(float glow)
        {
            return glow < 0.65 ? lerp(_Color7.rgb, _Color8.rgb, saturate((glow - 0.35) / 0.30))
                               : lerp(_Color8.rgb, _Color9.rgb, saturate((glow - 0.65) / 0.35));
        }

        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Smooth 3D value noise, 0..1.
        float Noise3(float3 x)
        {
            float3 i = floor(x), f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x),
                             lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x),
                             lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

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

                // Korstone cracks: warped object-space Voronoi borders, lit only when _CrackAlways is on.
                half crackAmount = 0;
                if (_CrackAlways > 0.01)
                {
                    float3 p = i.positionOS * _CrackScale;
                    p += 0.35 * sin(p.yzx * 1.7 + 1.3);
                    float edge = VoronoiEdge(p);
                    half crack = 1 - smoothstep(_CrackWidth * 0.4, _CrackWidth, edge);
                    half fade = _CrackFadeTop > 0 ? saturate(1.15 - i.positionOS.y / _CrackFadeTop) : 1;
                    crackAmount = crack * _CrackAlways * fade;
                }
                half flicker = 0.86 + 0.14 * sin(_Time.y * 5.0 - i.positionOS.y * 3.0);
                half3 crackLight = lerp(_GlowColor.rgb, _HotColor.rgb, 0.2) * _Intensity * flicker;

                // Gear shine by level: rim light, energy flowing up the surface, and a sweep of shine running upward.
                half glow = saturate(_Glow);
                half3 emission = 0;
                if (glow > 0.001)
                {
                    half3 levelColor = LevelColor(glow);
                    half rim = pow(saturate(1 - dot(N, V)), _RimPower);
                    float3 q = i.positionOS * _FlowScale + float3(0, -_Time.y * _FlowSpeed * (0.6 + glow), 0);
                    half flow = Noise3(q) * 0.65 + Noise3(q * 2.3 + 7.1) * 0.35;
                    half streak = smoothstep(0.52, 0.9, flow);
                    half sweep = pow(saturate(sin(i.positionOS.y * 2.6 - _Time.y * (1.4 + glow * 1.6)) * 0.5 + 0.5), 10);
                    half pulse = 0.9 + 0.1 * sin(_Time.y * 3.0);
                    half body = glow * _BodyGlow;
                    // Kept below full cover so the piece's own detail reads through the shine even at +9.
                    half strength = rim * 1.0 + streak * (0.2 + 0.45 * glow) + sweep * 0.75 * glow + 0.04 + body * 0.5;
                    emission = levelColor * _Intensity * glow * strength * pulse;
                }

                half3 color = lerp(lit + emission, crackLight, crackAmount);
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Upgrade aura: the piece's hull pushed out along its normals, drawn additively from the inside, so a soft glow
        // hugs the silhouette. Collapsed to nothing below +7.
        Pass
        {
            Name "UpgradeAura"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex AuraVert
            #pragma fragment AuraFrag

            struct AuraV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };

            AuraV AuraVert(Attributes v)
            {
                AuraV o;
                half glow = saturate(_Glow);
                float3 pos = v.positionOS.xyz + normalize(v.normalOS) * _AuraWidth * (0.4 + 0.6 * glow);
                o.positionWS = TransformObjectToWorld(pos);
                o.positionCS = glow > 0.001 ? TransformWorldToHClip(o.positionWS) : float4(0, 0, 0, 1);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 AuraFrag(AuraV i) : SV_Target
            {
                half glow = saturate(_Glow);
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half edge = pow(saturate(1 - abs(dot(N, V))), 1.5);
                half flicker = 0.85 + 0.15 * sin(_Time.y * 4.0 + i.positionOS.y * 6.0);
                return half4(LevelColor(glow) * edge * glow * glow * _AuraStrength * flicker, 0);
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
