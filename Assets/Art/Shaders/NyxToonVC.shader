// WAR OF NYX - NyxToon with VERTEX COLOURS (detailed procedural characters): albedo = vertex rgb x _BaseColor,
// glow = vertex rgb x (1 - vertex alpha) x _VertexGlow (alpha 1 = not glowing). One combined mesh per body part
// carries many colours, so a detailed character stays at ~16 draw calls.
// Original header:
// WAR OF NYX - cel-shaded material for characters, weapons and the world (Phase 10).
// URP forward: two-band toon lighting with a tinted shade colour, main-light shadows, ambient,
// rim light, emission (neon), fog, and an inverted-hull outline pass.
// The outline is disabled on the Low quality tier through the global _NyxOutlineDisable (1 = off).
Shader "WarOfNyx/NyxToonVC"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.32, 0.27, 0.52, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.04
        _RimColor ("Rim Color", Color) = (0.55, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.3
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.05, 0.03, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.012
        _VertexGlow ("Vertex glow strength", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float4 _ShadeColor;
            float _ShadeThreshold;
            float _ShadeSoftness;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float4 _EmissionColor;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _VertexGlow;
        CBUFFER_END

        // Global, set by the quality tier (VisualQuality). 0 = outlines on (default), 1 = off.
        float _NyxOutlineDisable;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                half4 color : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Two hard bands: lit and shade, with a thin soft edge (anime cel look).
                float ndl = dot(normal, light.direction);
                float lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                lit *= step(0.5, light.shadowAttenuation) * 0.85 + 0.15 * light.shadowAttenuation;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                albedo.rgb *= input.color.rgb;
                half3 shaded = albedo.rgb * _ShadeColor.rgb;
                half3 color = lerp(shaded, albedo.rgb * light.color, lit);
                color += albedo.rgb * SampleSH(normal) * 0.3;

                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim;
                color += _EmissionColor.rgb + input.color.rgb * (1.0 - input.color.a) * _VertexGlow;

                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Width grows slowly with distance so far objects keep a readable silhouette.
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = _OutlineWidth * (1.0 - saturate(_NyxOutlineDisable)) * clamp(distance * 0.08, 1.0, 6.0);
                positionWS += normalWS * width;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
