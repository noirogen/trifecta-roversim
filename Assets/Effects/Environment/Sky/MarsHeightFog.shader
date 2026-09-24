// Mars-style height fog for URP (Unity 2022.2+ / Unity 6)
// Use with a Full Screen Pass Renderer Feature. Requires "Depth Texture" enabled on the URP asset.
Shader "Custom/Mars Height Fog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.79, 0.60, 0.46, 1)
        _SunGlowColor ("Sun Glow Color", Color) = (1.0, 0.85, 0.65, 1)
        _Density ("Density", Range(0, 0.2)) = 0.02
        _HeightFalloff ("Height Falloff", Range(0.001, 1)) = 0.08
        _FogBaseHeight ("Base Height (ground Y)", Float) = 0
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.95
        _SunGlowPower ("Sun Glow Tightness", Range(1, 64)) = 8
        _SunGlowStrength ("Sun Glow Strength", Range(0, 1)) = 0.5
        _SkyHorizonFog ("Horizon Haze On Sky", Range(0, 1)) = 0.85
        _HorizonSharpness ("Horizon Haze Sharpness", Range(1, 32)) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "MarsHeightFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _SunGlowColor;
                float _Density;
                float _HeightFalloff;
                float _FogBaseHeight;
                float _MaxOpacity;
                float _SunGlowPower;
                float _SunGlowStrength;
                float _SkyHorizonFog;
                float _HorizonSharpness;
            CBUFFER_END

            float4 _MarsSunDir;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Reconstruct world position from depth
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 1e-6;
                #else
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                    bool isSky = rawDepth >= 1.0 - 1e-6;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 ray = worldPos - _WorldSpaceCameraPos;
                float dist = length(ray);
                float3 viewDir = ray / max(dist, 1e-5);

                // Dust scatters sunlight forward: tint the fog toward the sun.
                // Set Sun Glow Color to a pale blue for a Martian sunset.
                float3 sunDir = normalize(_MarsSunDir.w > 0.5 ? _MarsSunDir.xyz : _MainLightPosition.xyz);
                float sunAmount = pow(saturate(dot(viewDir, sunDir)), _SunGlowPower);
                // Fade the glow out once the sun is well below the horizon (keeps twilight glow)
                sunAmount *= saturate((sunDir.y + 0.15) / 0.15);
                float3 fogCol = lerp(_FogColor.rgb, _SunGlowColor.rgb, sunAmount * _SunGlowStrength);

                float fog;
                if (isSky)
                {
                    // Haze the sky near the horizon so the edge of the plane disappears
                    fog = pow(1.0 - saturate(viewDir.y), _HorizonSharpness) * _SkyHorizonFog;
                }
                else
                {
                    // Analytic exponential height fog integrated along the view ray
                    float falloff = max(_HeightFalloff, 1e-4);
                    float camH = _WorldSpaceCameraPos.y - _FogBaseHeight;
                    float t = max(falloff * ray.y, -80.0);
                    float lineInt = abs(t) > 1e-4 ? (1.0 - exp(-t)) / t : 1.0;
                    float opticalDepth = _Density * exp(-falloff * camH) * dist * lineInt;
                    fog = min(1.0 - exp(-opticalDepth), _MaxOpacity);
                }

                scene.rgb = lerp(scene.rgb, fogCol, fog);
                return scene;
            }
            ENDHLSL
        }
    }
}
