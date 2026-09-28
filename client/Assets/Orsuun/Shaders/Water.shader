// Old Nergui's river (owner, 28 Sep 2026: "the background of fishing map so bad, dont make it like a background. make it
// a reel place"). The water of the river scene (RiverScene): opaque, lit by the scene's own colours rather than URP's
// lights. Two scrolling ripple normal maps (made in code, RiverScene.RippleNormals) wobble a planar reflection of the
// painted horizon (the quad behind the river: _SkyRect gives its centre x, bottom y, width and height, _SkyZ its depth),
// so the sunset and the mountains lie mirrored in the water; a sharp sun glint and a softer sun path sparkle under the
// bloom; the water body darkens with depth of view and fades into the horizon's colour far away. Up to two ripple rings
// (_Ripple, _Ripple2: world xyz and the time they began) spread from the float when it lands and when a fish bites.
Shader "Orsuun/Water"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.04, 0.06, 0.08, 1)
        _NearColor ("Near Water", Color) = (0.12, 0.11, 0.09, 1)
        _HorizonColor ("Horizon", Color) = (0.95, 0.58, 0.32, 1)
        _SkyColor ("High Sky", Color) = (0.36, 0.28, 0.42, 1)
        [HDR] _SunColor ("Sun", Color) = (2.2, 1.3, 0.6, 1)
        _SunDir ("Toward The Sun (world)", Vector) = (-0.25, 0.14, 1, 0)
        [NoScaleOffset] _SkyMap ("Horizon Painting", 2D) = "black" {}
        _SkyRect ("Painting (centre x, bottom y, width, height)", Vector) = (0, -1, 120, 50)
        _SkyZ ("Painting Depth (world z)", Float) = 120
        [NoScaleOffset] _NormalMap ("Ripple Normals", 2D) = "bump" {}
        _WaveScale ("Ripple Tiles Per Metre", Float) = 0.22
        _WaveSpeed ("Ripple Speed", Float) = 0.05
        _WaveStrength ("Ripple Strength", Float) = 0.28
        _Reflect ("Reflection Share", Range(0, 1)) = 0.8
        _FarFade ("Far Fade Distance", Float) = 90
        _Origin ("Camera Ground (world xz)", Vector) = (0, 0, 0, 0)
        _Ripple ("Ripple (xyz, start time)", Vector) = (0, 0, 0, -100)
        _Ripple2 ("Ripple 2 (xyz, start time)", Vector) = (0, 0, 0, -100)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SkyMap); SAMPLER(sampler_SkyMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _NearColor;
                float4 _HorizonColor;
                float4 _SkyColor;
                float4 _SunColor;
                float4 _SunDir;
                float4 _SkyRect;
                float _SkyZ;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _Reflect;
                float _FarFade;
                float4 _Origin;
                float4 _Ripple;
                float4 _Ripple2;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            // A ring spreading from a point: its tilt of the surface (xz), fading as it widens.
            float2 Ring(float3 p, float4 ripple)
            {
                float age = _Time.y - ripple.w;
                if (age < 0 || age > 3.5) return 0;
                float2 d = p.xz - ripple.xz;
                float r = length(d) + 1e-4;
                float front = age * 0.9;
                float band = saturate(1 - abs(r - front) * 2.2);
                float wave = sin((r - front) * 26) * band * exp(-age * 1.1) * 0.9;
                return d / r * wave;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                float2 uv = (p.xz - _Origin.xz) * _WaveScale;
                float t = _Time.y * _WaveSpeed;
                // Two ripple layers drifting across each other; far away the ripples fade to a calm mirror.
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + float2(t, t * 0.6)));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * 1.9 + float2(-t * 0.7, t * 1.1)));
                float dist = length(p.xz - _Origin.xz);
                float calm = saturate(1 - dist / (_FarFade * 1.4));
                float2 tilt = (n1.xy + n2.xy * 0.6) * _WaveStrength * (0.35 + 0.65 * calm);
                tilt += Ring(p, _Ripple) + Ring(p, _Ripple2);
                float3 n = normalize(float3(tilt.x, 1, tilt.y));

                float3 v = normalize(GetCameraPositionWS() - p);
                float3 r = reflect(-v, n);
                r.y = max(r.y, 0.002);

                // The painting mirrored: where the reflected ray meets the painting's plane.
                float3 sky = lerp(_HorizonColor.rgb, _SkyColor.rgb, saturate(r.y * 2.5));
                float along = (_SkyZ - p.z) / max(r.z, 0.05);
                float3 hit = p + r * along;
                float2 suv = float2((hit.x - _SkyRect.x) / _SkyRect.z + 0.5, (hit.y - _SkyRect.y) / _SkyRect.w);
                if (r.z > 0.05 && suv.x > 0 && suv.x < 1 && suv.y > 0 && suv.y < 1)
                    sky = SAMPLE_TEXTURE2D(_SkyMap, sampler_SkyMap, suv).rgb;

                float fresnel = 0.04 + 0.96 * pow(1 - saturate(dot(n, v)), 5);
                float3 body = lerp(_NearColor.rgb, _DeepColor.rgb, saturate(dist / 25));
                float3 color = lerp(body, sky * 0.92, saturate(fresnel * 1.15) * _Reflect + 0.12);

                // The sun on the water: a sharp glint and the broad path under it.
                float3 sun = normalize(_SunDir.xyz);
                float facing = saturate(dot(r, sun));
                color += _SunColor.rgb * (pow(facing, 900) * 5 + pow(facing, 60) * 0.35);

                // Far off, the river melts into the horizon's haze.
                float haze = saturate((dist - _FarFade * 0.45) / _FarFade);
                color = lerp(color, _HorizonColor.rgb * 0.9, haze * 0.7);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
