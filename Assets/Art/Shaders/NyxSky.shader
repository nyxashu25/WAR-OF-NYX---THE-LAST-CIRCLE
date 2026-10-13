// WAR OF NYX - stylized dusk sky (Phase 10): deep indigo zenith, violet horizon with a soft
// magenta / cyan glow band, dark navy below the horizon. Cheap (no scattering), mobile friendly.
Shader "WarOfNyx/NyxSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.08, 0.06, 0.22, 1)
        _HorizonColor ("Horizon", Color) = (0.45, 0.3, 0.66, 1)
        _BottomColor ("Below horizon", Color) = (0.06, 0.05, 0.14, 1)
        _GlowColor ("Glow band", Color) = (1, 0.42, 0.75, 1)
        _GlowColor2 ("Glow band 2", Color) = (0.25, 0.85, 1, 1)
        _GlowHeight ("Glow height", Range(-0.2, 0.5)) = 0.04
        _GlowWidth ("Glow width", Range(0.01, 0.5)) = 0.09
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _BottomColor;
                float4 _GlowColor;
                float4 _GlowColor2;
                float _GlowHeight;
                float _GlowWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float y = d.y;
                half3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0.0, 0.65, y));
                half3 ground = lerp(_HorizonColor.rgb, _BottomColor.rgb, smoothstep(0.0, -0.25, y));
                half3 color = y >= 0.0 ? sky : ground;

                // Glow band near the horizon; colour drifts from magenta to cyan around the compass.
                float band = exp(-pow((y - _GlowHeight) / _GlowWidth, 2.0));
                float around = 0.5 + 0.5 * sin(atan2(d.z, d.x) * 1.5);
                color += lerp(_GlowColor.rgb, _GlowColor2.rgb, around) * band * 0.55;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
