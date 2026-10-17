// WAR OF NYX - cel shading for imported character models (VRM / MToon, artist FBX).
// Same look as NyxToon (two-band lighting with a tinted shade, main-light shadows, ambient, rim, emission, fog,
// inverted-hull outline), plus what character art needs: a shade texture (MToon "shade multiply"), alpha cut-out
// (hair, lashes) and alpha blending (eye highlights, lenses), per-material culling and outline width.
// The importer sets the render state (_SrcBlend / _DstBlend / _ZWrite / _Cull) and the _ALPHATEST_ON keyword, and
// turns the outline pass off for transparent materials. Outlines follow the global _NyxOutlineDisable (Low tier).
Shader "WarOfNyx/NyxCharacterToon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Base Map", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.62, 0.55, 0.72, 1)
        _ShadeMap ("Shade Map (multiplied by Shade Color)", 2D) = "white" {}
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.0
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.05
        _RimColor ("Rim Color", Color) = (0.55, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.05, 0.03, 0.1, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.01)) = 0.0015
        _Cutoff ("Alpha Cut-off", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
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
            float _Cutoff;
        CBUFFER_END

        // Global, set by the quality tier. 0 = outlines on (default), 1 = off.
        float _NyxOutlineDisable;

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ShadeMap);
            SAMPLER(sampler_ShadeMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
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
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                    clip(albedo.a - _Cutoff);
                #endif
                // Two-sided parts (hair cards, coat insides) are lit from the side the camera sees.
                float3 normal = normalize(input.normalWS) * (facing >= 0 ? 1.0 : -1.0);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float ndl = dot(normal, light.direction);
                float lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                lit *= step(0.5, light.shadowAttenuation) * 0.85 + 0.15 * light.shadowAttenuation;

                half3 shade = SAMPLE_TEXTURE2D(_ShadeMap, sampler_ShadeMap, input.uv).rgb * _ShadeColor.rgb;
                half3 color = lerp(shade, albedo.rgb * light.color, lit);
                color += albedo.rgb * SampleSH(normal) * 0.3;

                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Width grows slowly with distance so far characters keep a readable silhouette.
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = _OutlineWidth * (1.0 - saturate(_NyxOutlineDisable)) * clamp(distance * 0.08, 1.0, 6.0);
                positionWS += normalWS * width;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
