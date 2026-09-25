// Simple gradient skybox for URP with a soft sun disk + halo.
// Colors are driven at runtime by MarsTimeOfDay; the values here are the midday defaults.
Shader "Skybox/Mars Gradient"
{
    Properties
    {
        _ZenithColor ("Zenith (Overhead) Color", Color) = (0.66, 0.52, 0.45, 1)
        _HorizonColor ("Horizon Color", Color) = (0.77, 0.56, 0.43, 1)
        _HorizonBlend ("Horizon Blend (lower = wider horizon band)", Range(0.1, 4)) = 0.6

        [HDR] _SunColor ("Sun Color", Color) = (3.0, 2.8, 2.5, 1)
        _SunSize ("Sun Size (degrees)", Range(0.1, 5)) = 0.35
        _SunHaloColor ("Sun Halo Color", Color) = (0.55, 0.62, 0.72, 1)
        _SunHaloPower ("Sun Halo Tightness", Range(1, 256)) = 24
        _SunHaloStrength ("Sun Halo Strength", Range(0, 2)) = 0.6

        _Exposure ("Exposure", Range(0, 3)) = 1

        [Header(Stars)]
        _StarIntensity ("Star Brightness", Range(0, 5)) = 1.5
        _StarDensity ("Star Density (grid scale)", Range(50, 400)) = 150
        _StarCoverage ("Star Coverage", Range(0, 1)) = 0.35
        _StarSize ("Star Size", Range(0.02, 0.16)) = 0.12
        _StarTwinkle ("Twinkle", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _ZenithColor;
            float4 _HorizonColor;
            float _HorizonBlend;
            float4 _SunColor;
            float _SunSize;
            float4 _SunHaloColor;
            float _SunHaloPower;
            float _SunHaloStrength;
            float _Exposure;
            float4 _MarsSunDir; // set globally by MarsTimeOfDay (w = 1 when valid)
            float4x4 _MarsStarMatrix; // set globally by MarsTimeOfDay: rotates stars with the sky

            float _StarIntensity;
            float _StarDensity;
            float _StarCoverage;
            float _StarSize;
            float _StarTwinkle;

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yxz + 19.19);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float3 Stars(float3 dir, float3 aaDir)
            {
                float3 p = dir * _StarDensity;
                float3 cell = floor(p);
                float3 h = Hash33(cell);
                float3 h2 = Hash33(cell + 17.31);

                float present = step(h2.x, _StarCoverage);
                float3 starPos = cell + 0.25 + h * 0.5;
                float dist = length(p - starPos);

                float size = _StarSize * lerp(0.5, 1.5, h2.z);
                // Derive AA width from the sampling position, not from dist:
                // dist jumps at cell borders and would blow fwidth up there.
                float aa = max(length(fwidth(aaDir * _StarDensity)), 1e-4);
                float shape = 1.0 - smoothstep(size - aa, size + aa, dist);

                // Mostly dim stars, a few bright ones
                float brightness = pow(h2.y, 4.0) * 3.0 + 0.15;
                float twinkle = 1.0 - _StarTwinkle * 0.5 * (1.0 + sin(_Time.y * (2.0 + h.z * 4.0) + h.x * 6.2831));

                float3 tint = lerp(float3(1.0, 0.85, 0.7), float3(0.8, 0.9, 1.0), h.y);
                return tint * (present * shape * brightness * twinkle);
            }

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);

                // Horizon -> zenith gradient. Below the horizon stays horizon color
                // so the edge of a planar world blends into the fog.
                float up = pow(saturate(d.y), _HorizonBlend);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, up);

                // Sun (follows the main directional light)
                float3 L = normalize(_MarsSunDir.w > 0.5 ? _MarsSunDir.xyz : _MainLightPosition.xyz);
                float cosA = dot(d, L);
                // Fade sun + halo out once the sun is well below the horizon (keeps twilight glow)
                float sunVis = saturate((L.y + 0.15) / 0.15);

                // Stars: fade in once the sun is a few degrees below the horizon,
                // and fade out toward the hazy horizon
                float starVis = saturate((-L.y - 0.05) / 0.2) * saturate(d.y * 5.0);
                float3 starDir = _MarsSunDir.w > 0.5 ? mul((float3x3)_MarsStarMatrix, d) : d;
                col += Stars(starDir, d) * starVis * _StarIntensity;

                float halo = pow(saturate(cosA), _SunHaloPower) * _SunHaloStrength * sunVis;
                col += _SunHaloColor.rgb * halo;

                float angleDeg = degrees(acos(clamp(cosA, -1.0, 1.0)));
                float disk = (1.0 - smoothstep(_SunSize * 0.6, _SunSize, angleDeg)) * sunVis;
                col = lerp(col, _SunColor.rgb, disk);

                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}