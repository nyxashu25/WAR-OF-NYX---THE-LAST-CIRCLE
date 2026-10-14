// WAR OF NYX - vegetation (grass, flowers, bushes, tree bark and leaves).
// URP forward, mobile friendly: no textures, no alpha test (solid blades and leaf clusters are cheaper
// than cut-out cards on tile-based phone GPUs). Colour comes from vertex colours x _BaseColor.
//  * wind sway (NyxWind.hlsl) in every pass, so shadows and depth move with the plant
//  * soft cel lighting (wider wrap than characters) + main-light shadows + ambient
//  * leaf translucency when the sun is behind the leaves
//  * height gradient (darker roots, lighter tips) and distance tint toward the fog colour
//  * optional inverted-hull outline (trees), off for grass via _OutlineWidth = 0 and on Low tier
// Vertex colour: rgb = tint, a = wind weight.
Shader "WarOfNyx/NyxFoliage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.3, 0.32, 0.5, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = -0.1
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.8)) = 0.25
        _TranslucencyColor ("Translucency", Color) = (0.55, 0.8, 0.35, 1)
        _TranslucencyStrength ("Translucency Strength", Range(0, 1)) = 0.35
        _TipLighten ("Tip Lighten", Range(0, 1)) = 0.25
        _WindAmount ("Wind Amount (m)", Range(0, 1)) = 0.15
        _WindFrequency ("Wind Frequency", Range(0.1, 4)) = 1.6
        [Toggle] _NearFade ("Flatten near camera (grass)", Float) = 0
        _FieldVariation ("Field colour variation", Range(0, 1)) = 0
        _OutlineColor ("Outline Color", Color) = (0.05, 0.04, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "NyxWind.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _ShadeColor;
            float _ShadeThreshold;
            float _ShadeSoftness;
            float4 _TranslucencyColor;
            float _TranslucencyStrength;
            float _TipLighten;
            float _WindAmount;
            float _WindFrequency;
            float _NearFade;
            float _FieldVariation;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _Cull;
        CBUFFER_END

        float _NyxOutlineDisable;

        struct WindAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 WindPositionWS(WindAttributes input)
        {
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            positionWS = NyxApplyWind(positionWS, input.color.a, _WindAmount, _WindFrequency);
            // Grass between the third-person camera and the player lies down instead of filling the view.
            float near = saturate((distance(positionWS, GetCameraPositionWS()) - 0.8) / 1.6);
            positionWS.y -= (1.0 - near) * _NearFade * input.color.a * 0.5;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(WindAttributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = WindPositionWS(input);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                // Two-sided leaves and blades: light the back face with the flipped normal.
                normal = frontFace ? normal : -normal;
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float ndl = dot(normal, light.direction);
                float lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                lit *= lerp(0.2, 1.0, light.shadowAttenuation);

                half3 albedo = input.color.rgb * _BaseColor.rgb;
                // Large soft patches of lusher / drier grass across a field (world-space, no texture).
                float2 p = input.positionWS.xz;
                float patch = sin(p.x * 0.043 + sin(p.y * 0.031) * 2.0) * sin(p.y * 0.037 + sin(p.x * 0.027) * 2.0);
                albedo *= 1.0 + patch * 0.12 * _FieldVariation;
                albedo.r += max(patch, 0.0) * 0.05 * _FieldVariation;
                albedo *= 1.0 + _TipLighten * (input.color.a - 0.35);

                half3 shaded = albedo * _ShadeColor.rgb;
                half3 color = lerp(shaded, albedo * light.color, lit);
                color += albedo * SampleSH(normal) * 0.45;

                // Sunlight shining through leaves (seen from the shadow side).
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float through = pow(saturate(dot(viewDirection, -light.direction)), 4.0);
                color += _TranslucencyColor.rgb * light.color * through * _TranslucencyStrength * input.color.a * light.shadowAttenuation;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(WindAttributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = WindPositionWS(input);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = _OutlineWidth * (1.0 - saturate(_NyxOutlineDisable)) * clamp(distance * 0.08, 1.0, 6.0);
                positionWS += normalWS * width;
                output.positionCS = TransformWorldToHClip(positionWS);
                // Zero width: push behind the far plane so nothing is drawn.
                if (width <= 0.0)
                    output.positionCS = float4(0, 0, -2, 1);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP for the light currently rendering shadows (same as ShadowCasterPass.hlsl).
            float3 _LightDirection;

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(WindAttributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = WindPositionWS(input);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(WindAttributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformWorldToHClip(WindPositionWS(input));
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
