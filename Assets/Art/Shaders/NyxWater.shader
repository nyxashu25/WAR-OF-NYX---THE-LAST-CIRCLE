// WAR OF NYX - stylized sea. Opaque (no refraction / depth texture: cheap on phones).
//  * moving wave normals from two scrolling procedural noise layers (no textures)
//  * deep / shallow colour by view angle, fresnel reflection of the sky colours
//  * sharp anime sun / moon glints, stronger wind = choppier water
//  * rain roughens the surface (_NyxRain global, 0..1)
Shader "WarOfNyx/NyxWater"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.05, 0.12, 0.3, 1)
        _ShallowColor ("Shallow", Color) = (0.12, 0.42, 0.55, 1)
        _ReflectColor ("Sky reflection", Color) = (0.5, 0.45, 0.8, 1)
        _GlintColor ("Glint", Color) = (1, 0.92, 0.8, 1)
        _WaveScale ("Wave scale", Float) = 0.08
        _WaveSpeed ("Wave speed", Float) = 0.6
        _WaveStrength ("Wave strength", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _ReflectColor;
                float4 _GlintColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
            CBUFFER_END

            float4 _NyxWind;
            float _NyxWindTime;
            float _NyxRain;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Height field of two scrolling layers; the normal comes from finite differences.
            float Waves(float2 p, float t, float2 wind)
            {
                return Noise(p + wind * t) * 0.6 + Noise(p * 2.3 - wind.yx * t * 1.3) * 0.3 + Noise(p * 5.1 + t * 0.7) * 0.1;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 wind = _NyxWind.xz;
                if (dot(wind, wind) < 1e-4) wind = float2(0.8, 0.6);
                float t = _NyxWindTime * _WaveSpeed;
                float2 p = input.positionWS.xz * _WaveScale;
                float choppy = _WaveStrength * (0.6 + saturate(_NyxWind.w) * 0.8 + saturate(_NyxRain) * 0.6);

                const float e = 0.15;
                float h = Waves(p, t, wind);
                float hx = Waves(p + float2(e, 0), t, wind);
                float hz = Waves(p + float2(0, e), t, wind);
                float3 normal = normalize(float3((h - hx) * choppy * 6.0, 1.0, (h - hz) * choppy * 6.0));

                // Rain: fine high-frequency roughness.
                float rain = saturate(_NyxRain);
                if (rain > 0.01)
                {
                    float r = Noise(input.positionWS.xz * 3.7 + _NyxWindTime * 5.0) - 0.5;
                    normal = normalize(normal + float3(r, 0, -r) * rain * 0.6);
                }

                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = saturate(dot(normal, viewDirection));
                float fresnel = pow(1.0 - facing, 4.0);

                Light light = GetMainLight();
                half3 water = lerp(_DeepColor.rgb, _ShallowColor.rgb, facing * 0.6 + h * 0.25);
                water *= 0.55 + 0.45 * saturate(dot(normal, light.direction)) * light.color;
                water += SampleSH(normal) * _DeepColor.rgb * 0.6;
                half3 color = lerp(water, _ReflectColor.rgb * (0.6 + 0.4 * light.color), saturate(fresnel * 0.85));

                // Anime glint: a hard-edged highlight instead of a soft specular lobe.
                float3 halfVector = normalize(light.direction + viewDirection);
                float spec = pow(saturate(dot(normal, halfVector)), 220.0);
                color += _GlintColor.rgb * light.color * smoothstep(0.35, 0.6, spec) * 1.4;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
