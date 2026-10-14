// WAR OF NYX - stylized sky (Phase 10, weather update): gradient sky with a horizon glow band,
// drifting procedural clouds, sun / moon disc with a halo, stars at night and lightning flashes.
// Colours are material properties (WeatherSystem drives an instance per time of day); clouds,
// stars, sun and flashes come from globals so the same sky works with or without weather.
// No textures; 3 noise octaves for clouds: fine on phones.
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
        _CloudLit ("Cloud lit colour", Color) = (0.95, 0.75, 0.9, 1)
        _CloudShade ("Cloud shade colour", Color) = (0.32, 0.25, 0.48, 1)
        _SunSize ("Sun / moon size", Range(0.005, 0.1)) = 0.03
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
                float4 _CloudLit;
                float4 _CloudShade;
                float _SunSize;
            CBUFFER_END

            // Globals (WeatherSystem). Defaults of 0 give the original clear dusk sky.
            float _NyxCloudCover;   // 0..1
            float _NyxNight;        // 0..1 stars
            float4 _NyxSunDir;      // xyz = direction TO the sun / moon, w = 1 when set
            float4 _NyxSunColor;
            float _NyxFlash;        // 0..1 lightning
            float4 _NyxWind;
            float _NyxWindTime;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Clouds(float2 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.1 + 3.7) * 0.3 + Noise(p * 4.3 + 7.1) * 0.15;
            }

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
                float around = 0.5 + 0.5 * sin(atan2(d.z, d.x) * 2.0); // integer frequency: no seam where the angle wraps
                float cover = saturate(_NyxCloudCover);
                color += lerp(_GlowColor.rgb, _GlowColor2.rgb, around) * band * 0.55 * (1.0 - cover * 0.6);

                // Stars: sparse hashed cells, twinkling, hidden by clouds and near the horizon.
                float night = saturate(_NyxNight);
                if (night > 0.01 && y > 0.0)
                {
                    float2 cell = floor(d.xz / (y + 0.35) * 220.0);
                    float star = step(0.9965, Hash(cell));
                    float twinkle = 0.6 + 0.4 * sin(_NyxWindTime * 3.0 + Hash(cell + 1.7) * 30.0);
                    color += star * twinkle * night * smoothstep(0.05, 0.3, y) * (1.0 - cover);
                }

                // Sun / moon disc and halo.
                float sunSet = _NyxSunDir.w;
                float3 sunDirection = normalize(_NyxSunDir.xyz + float3(0, 1e-4, 0));
                float toSun = dot(d, sunDirection);
                float disc = smoothstep(1.0 - _SunSize * 0.02, 1.0 - _SunSize * 0.015, toSun);
                float halo = pow(saturate(toSun), 64.0) * 0.45 + pow(saturate(toSun), 8.0) * 0.12;
                color += _NyxSunColor.rgb * (disc * 2.0 + halo) * sunSet * (1.0 - cover * 0.85);

                // Clouds on a flat dome above the island, drifting with the wind.
                if (y > -0.02)
                {
                    float2 uv = d.xz / (y + 0.12) * 1.6;
                    float2 wind = _NyxWind.xz;
                    if (dot(wind, wind) < 1e-4) wind = float2(0.8, 0.6);
                    uv += wind * _NyxWindTime * (0.006 + 0.012 * saturate(_NyxWind.w));
                    float n = Clouds(uv);
                    float threshold = lerp(0.72, 0.28, cover);
                    float cloud = smoothstep(threshold, threshold + 0.18, n);
                    // Lit from the sun side, darker and heavier with more cover.
                    float lit = saturate(Clouds(uv + sunDirection.xz * 0.08) - n + 0.55);
                    half3 cloudColor = lerp(_CloudShade.rgb, _CloudLit.rgb, lit) * (1.0 - cover * 0.45);
                    cloudColor += _NyxSunColor.rgb * halo * 0.6 * sunSet;
                    float fade = smoothstep(-0.02, 0.12, y);
                    color = lerp(color, cloudColor, cloud * fade * (0.55 + 0.45 * cover));
                    // Overcast: the whole sky greys toward the cloud shade colour.
                    color = lerp(color, _CloudShade.rgb * (1.0 - cover * 0.3), cover * cover * 0.55 * fade);
                }

                color += float3(0.75, 0.7, 1.0) * saturate(_NyxFlash) * 1.2;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
