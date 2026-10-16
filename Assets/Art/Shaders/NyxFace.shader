// WAR OF NYX - painted anime face decals (eyes, brows, nose and mouth) for the skinned characters.
// The face atlas holds 4 x 4 expression tiles (FacePainter / FaceTiles). Each decal vertex carries its part in uv2.x:
// 0 right eye, 1 left eye (mirrored), 2 right brow, 3 left brow (mirrored), 4 nose and mouth. The tiles shown are chosen
// per renderer (_EyeTile, _BrowTile, _MouthTile via a MaterialPropertyBlock from CharacterFacialAnimator), so blinking
// and expressions cost nothing but a property change. Lit softly (faces stay readable in dark scenes), no outline.
Shader "WarOfNyx/NyxFace"
{
    Properties
    {
        _FaceAtlas ("Face Atlas", 2D) = "white" {}
        _EyeTile ("Eye tile (0-7)", Float) = 0
        _BrowTile ("Brow tile (8-11)", Float) = 8
        _MouthTile ("Mouth tile (12-15)", Float) = 12
        _ShadeColor ("Shade Color", Color) = (0.62, 0.55, 0.72, 1)
        _EyeGlow ("Eye glow", Range(0, 2)) = 0
        _GlowColor ("Glow colour", Color) = (0.62, 0.25, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_FaceAtlas);
            SAMPLER(sampler_FaceAtlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _FaceAtlas_ST;
                float _EyeTile;
                float _BrowTile;
                float _MouthTile;
                float4 _ShadeColor;
                float _EyeGlow;
                float4 _GlowColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 part : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                nointerpolation float part : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.part = input.part.x;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int part = (int)(input.part + 0.5);
                float tile = part <= 1 ? _EyeTile : part <= 3 ? _BrowTile : _MouthTile;
                float2 uv = saturate(input.uv);
                if (part == 1 || part == 3)
                    uv.x = 1.0 - uv.x; // the left eye and brow mirror the right
                float col = fmod(tile, 4.0);
                float row = floor(tile / 4.0);
                float2 atlasUv = float2((col + uv.x) * 0.25, (3.0 - row + uv.y) * 0.25);
                half4 c = SAMPLE_TEXTURE2D(_FaceAtlas, sampler_FaceAtlas, atlasUv);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndl = saturate(dot(normalize(input.normalWS), light.direction) * 0.5 + 0.5);
                float lit = lerp(0.72, 1.0, smoothstep(0.35, 0.6, ndl) * (0.6 + 0.4 * light.shadowAttenuation));
                half3 color = c.rgb * lerp(_ShadeColor.rgb, light.color, lit) + c.rgb * SampleSH(input.normalWS) * 0.25;
                if (part <= 1)
                {
                    // Eyes catch a little extra light, and can glow (special moments).
                    float sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
                    color += c.rgb * 0.12 + _GlowColor.rgb * _EyeGlow * sat * 2.0;
                }
                color = MixFog(color, input.fogFactor);
                return half4(color, c.a);
            }
            ENDHLSL
        }
    }
}
