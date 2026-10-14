// WAR OF NYX - additive streaks for rain drops and lightning bolts. Vertex colour x _Color,
// soft edges from the UVs (across: u, along: v). No texture, no depth write.
Shader "WarOfNyx/NyxAdditive"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fogFactor : TEXCOORD1; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                float along = sin(saturate(input.uv.y) * 3.14159);
                half3 color = input.color.rgb * input.color.a * across * along;
                // Additive: fade toward black (nothing) in fog instead of toward the fog colour.
                color *= saturate(input.fogFactor * 4.0 + 0.25);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
