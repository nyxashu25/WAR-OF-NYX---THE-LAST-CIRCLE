// WAR OF NYX - procedural surface detail for the world (cel-shaded like NyxToon, no textures):
//   FACADE      buildings: window grid sized to each wall, frames, sills, floor ledges, shopfront with door and
//               neon strip on the ground floor, parapet; glass reflects the sky by day, random windows light at night
//   CORRUGATED  shipping containers: ribs, end doors with lock bars, top / bottom rails, grime
//   PANELS      metal walls: plate seams, rivets, per-plate tint
//   STONE       brick courses (running bond) with mortar, per-brick tint; flagstones on top faces
//   CONCRETE    formwork panels, tie holes, rain streaks
//   WOOD        planks with gaps and grain
// FACADE and CORRUGATED work in object space (so windows fit each wall exactly): their renderers must not be
// static-batched. The others work in world space. Global _NyxNight (WeatherSystem) lights windows at night.
Shader "WarOfNyx/NyxSurface"
{
    Properties
    {
        [KeywordEnum(Facade, Corrugated, Panels, Stone, Concrete, Wood)] _Pattern ("Pattern", Float) = 4
        _BaseColor ("Base Color", Color) = (0.5, 0.5, 0.55, 1)
        _SecondaryColor ("Secondary (mortar / seams / frames)", Color) = (0.2, 0.2, 0.25, 1)
        _TrimColor ("Trim (ledges / rails)", Color) = (0.3, 0.3, 0.36, 1)
        _GlassColor ("Glass", Color) = (0.25, 0.4, 0.6, 1)
        _LitColor ("Lit window", Color) = (1, 0.8, 0.5, 1)
        _AccentColor ("Accent (neon strip)", Color) = (1, 0.3, 0.8, 1)
        _PatternScale ("Pattern scale", Float) = 1
        _CellWidth ("Window spacing (m)", Float) = 2.4
        _FloorHeight ("Floor height (m)", Float) = 3.2
        _ShadeColor ("Shade Color", Color) = (0.32, 0.27, 0.52, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001, 0.5)) = 0.04
        _RimColor ("Rim Color", Color) = (0.55, 0.8, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.05, 0.03, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.01
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _SecondaryColor;
            float4 _TrimColor;
            float4 _GlassColor;
            float4 _LitColor;
            float4 _AccentColor;
            float _PatternScale;
            float _CellWidth;
            float _FloorHeight;
            float4 _ShadeColor;
            float _ShadeThreshold;
            float _ShadeSoftness;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;
            float4 _EmissionColor;
            float4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END

        float _NyxOutlineDisable;
        float _NyxNight;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _PATTERN_FACADE _PATTERN_CORRUGATED _PATTERN_PANELS _PATTERN_STONE _PATTERN_CONCRETE _PATTERN_WOOD
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // 1 inside [a, b], with a soft edge of width w.
            float Band(float x, float a, float b, float w) { return smoothstep(a - w, a + w, x) * (1.0 - smoothstep(b - w, b + w, x)); }

            // Face coordinates: uv in metres on the dominant-axis plane, top = face points up.
            void FaceCoords(float3 p, float3 n, out float2 uv, out float top, out float axisX)
            {
                float3 a = abs(n);
                top = a.y > 0.5 ? sign(n.y) : 0.0;
                axisX = a.x > a.z ? 1.0 : 0.0;
                uv = a.y > 0.5 ? p.xz : (a.x > a.z ? p.zy : p.xy);
            }

            void Facade(float3 positionOS, float3 normalOS, float3 positionWS, out half3 albedo, out half3 emission)
            {
                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                float3 p = positionOS * scale;
                float2 uv; float top; float axisX;
                FaceCoords(p, normalOS, uv, top, axisX);
                float2 buildingId = floor(unity_ObjectToWorld._m03_m23 * 0.37);
                emission = 0;
                if (top != 0.0)
                {
                    // Roof: concrete slabs, darker seams, gravel noise.
                    float2 g = frac(uv / 3.0);
                    float seam = 1.0 - Band(g.x, 0.02, 0.98, 0.01) * Band(g.y, 0.02, 0.98, 0.01);
                    albedo = _TrimColor.rgb * (0.85 + Noise(uv * 4.0) * 0.15) * (1.0 - seam * 0.3);
                    return;
                }
                float width = axisX > 0.5 ? scale.z : scale.x;
                float height = scale.y;
                float u = uv.x + width * 0.5;           // 0 .. width
                float v = uv.y + height * 0.5;          // 0 .. height (from the ground)
                float cell = max(_CellWidth, 1.0);
                float columns = max(1.0, floor((width - 1.2) / cell));
                float margin = (width - columns * cell) * 0.5;
                float cu = (u - margin) / cell;
                float column = floor(cu);
                float fu = frac(cu);
                bool inColumns = cu >= 0.0 && cu < columns;

                float wallNoise = 0.9 + Noise(float2(u, v) * 1.7 + buildingId) * 0.1;
                albedo = _BaseColor.rgb * wallNoise;
                // Vertical grime under windows and from the roof edge.
                albedo *= 1.0 - Noise(float2(u * 3.0, v * 0.25) + buildingId * 3.1) * 0.12;

                const float groundFloor = 3.6;
                const float parapet = 0.7;
                if (v < groundFloor)
                {
                    // Shopfront: big panes, a door in the middle column, neon strip above.
                    float door = (inColumns && abs(column - floor(columns * 0.5)) < 0.5) ? Band(fu, 0.28, 0.72, 0.005) * Band(v, 0.0, 2.4, 0.005) : 0.0;
                    float pane = inColumns ? Band(fu, 0.07, 0.93, 0.004) * Band(v, 0.45, 2.85, 0.004) : 0.0;
                    float frame = inColumns ? Band(fu, 0.05, 0.95, 0.004) * Band(v, 0.38, 2.92, 0.004) - pane : 0.0;
                    float strip = Band(v, 3.05, 3.3, 0.01);
                    albedo = lerp(albedo, _SecondaryColor.rgb, saturate(frame));
                    float shopLit = Hash(float2(column, 7.0) + buildingId) > 0.35 ? 1.0 : 0.25;
                    half3 shopGlass = lerp(_GlassColor.rgb * 0.7 * lerp(1.0, 0.3, saturate(_NyxNight)), _LitColor.rgb, 0.35 * shopLit);
                    albedo = lerp(albedo, shopGlass, pane * (1.0 - door));
                    emission += _LitColor.rgb * pane * (1.0 - door) * shopLit * lerp(0.15, 0.9, _NyxNight);
                    albedo = lerp(albedo, _SecondaryColor.rgb * 0.5, door);
                    albedo = lerp(albedo, _AccentColor.rgb, strip);
                    emission += _AccentColor.rgb * strip * lerp(0.6, 1.6, _NyxNight);
                    return;
                }
                if (v > height - parapet)
                {
                    // Parapet with a coping line.
                    albedo = lerp(albedo, _TrimColor.rgb, Band(v, height - 0.2, height + 1.0, 0.01));
                    return;
                }
                float fv = (v - groundFloor) / max(_FloorHeight, 2.0);
                float floorIndex = floor(fv);
                float f = frac(fv);
                // Floor ledge between storeys.
                albedo = lerp(albedo, _TrimColor.rgb, Band(f, -0.1, 0.05, 0.005));
                if (!inColumns || v > height - parapet - 0.6)
                    return;
                float window = Band(fu, 0.22, 0.78, 0.004) * Band(f, 0.28, 0.86, 0.004);
                float frame = Band(fu, 0.19, 0.81, 0.004) * Band(f, 0.24, 0.9, 0.004) - window;
                float sill = Band(fu, 0.16, 0.84, 0.004) * Band(f, 0.2, 0.25, 0.004);
                albedo = lerp(albedo, _SecondaryColor.rgb, saturate(frame));
                albedo = lerp(albedo, _TrimColor.rgb * 1.15, sill);

                float2 id = float2(column, floorIndex) + buildingId * 13.0 + axisX * 57.0;
                float lit = Hash(id) < lerp(0.08, 0.62, _NyxNight) ? 1.0 : 0.0;
                float blinds = Hash(id + 3.3) > 0.65 ? Band(f, 0.6, 0.86, 0.004) : 0.0;
                // Day: glass reflects a sky gradient; night: dark glass or a lit room (warm or cool).
                half3 sky = lerp(_GlassColor.rgb * 0.75, _GlassColor.rgb * 1.35 + 0.08, saturate((f - 0.28) / 0.58));
                sky *= lerp(1.0, 0.22, saturate(_NyxNight)); // dark glass at night: lit rooms stand out
                half3 room = lerp(_LitColor.rgb, _LitColor.bgr * 0.9 + 0.1, step(0.7, Hash(id + 9.1)));
                half3 glass = lerp(sky, room * 0.8, lit * 0.6);
                glass = lerp(glass, _BaseColor.rgb * 1.2, blinds);
                albedo = lerp(albedo, glass, window);
                emission += room * window * lit * (1.0 - blinds * 0.5) * lerp(0.2, 1.1, _NyxNight);
            }

            void Corrugated(float3 positionOS, float3 normalOS, out half3 albedo)
            {
                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                float3 p = positionOS * scale;
                float2 uv; float top; float axisX;
                FaceCoords(p, normalOS, uv, top, axisX);
                bool longX = scale.x > scale.z;
                bool endFace = top == 0.0 && ((axisX > 0.5) == longX);
                float width = axisX > 0.5 ? scale.z : scale.x;
                float h = uv.y + scale.y * 0.5;
                float rib = top != 0.0 ? 0.0 : pow(abs(sin(uv.x * 3.14159 / 0.16)), 2.0);
                albedo = _BaseColor.rgb * (0.8 + 0.2 * rib);
                // Top and bottom rails, corner posts.
                float rail = top != 0.0 ? 0.0 : 1.0 - Band(h, 0.12, scale.y - 0.12, 0.01);
                float post = top != 0.0 ? 0.0 : 1.0 - Band(uv.x, -width * 0.5 + 0.1, width * 0.5 - 0.1, 0.01);
                albedo = lerp(albedo, _TrimColor.rgb, saturate(rail + post));
                if (endFace)
                {
                    // Double doors: centre seam, four lock bars, hinges.
                    float x = uv.x / max(width, 0.1);
                    float seam = 1.0 - Band(x, -0.008, 0.008, 0.002);
                    float bars = 0.0;
                    for (int i = 0; i < 4; i++)
                    {
                        float c = -0.36 + i * 0.24;
                        bars += Band(x, c - 0.012, c + 0.012, 0.002);
                    }
                    albedo = lerp(albedo, _TrimColor.rgb * 0.7, saturate(bars) * Band(h, 0.15, scale.y - 0.15, 0.01));
                    albedo *= seam > 0.5 ? 1.0 : 0.4;
                }
                // Rust and dirt near the bottom.
                float grime = (1.0 - saturate(h / 1.2)) * Noise(uv * 2.3) * 0.5 + Noise(uv * 6.0) * 0.08;
                albedo = lerp(albedo, half3(0.35, 0.22, 0.14), grime);
            }

            void WorldPattern(float3 positionWS, float3 normalWS, out half3 albedo)
            {
                float2 uv; float top; float axisX;
                FaceCoords(positionWS / max(_PatternScale, 0.01), normalWS, uv, top, axisX);
                albedo = _BaseColor.rgb;
            #if defined(_PATTERN_PANELS)
                float2 size = float2(2.0, 1.2);
                float2 cell = floor(uv / size);
                float2 f = frac(uv / size);
                float seam = 1.0 - Band(f.x, 0.012, 0.988, 0.004) * Band(f.y, 0.02, 0.98, 0.004);
                float2 r = frac(uv / size * float2(8.0, 4.0));
                float rivet = (f.x < 0.04 || f.x > 0.96 || f.y < 0.07 || f.y > 0.93) ? step(length(r - 0.5), 0.18) : 0.0;
                albedo *= 0.88 + Hash(cell) * 0.2;
                albedo = lerp(albedo, _SecondaryColor.rgb, seam);
                albedo = lerp(albedo, _TrimColor.rgb * 1.3, rivet * 0.7);
                albedo *= 1.0 - Noise(uv * float2(1.5, 0.3)) * 0.1;
            #elif defined(_PATTERN_STONE)
                float2 size = top != 0.0 ? float2(1.0, 1.0) : float2(0.8, 0.36);
                float row = floor(uv.y / size.y);
                float2 b = uv / size;
                b.x += top != 0.0 ? 0.0 : (fmod(abs(row), 2.0) * 0.5);
                float2 cell = floor(b);
                float2 f = frac(b);
                float mortar = 1.0 - Band(f.x, 0.04, 0.96, 0.02) * Band(f.y, 0.07, 0.93, 0.03);
                albedo *= 0.82 + Hash(cell) * 0.3;
                albedo *= 0.92 + Noise(uv * 7.0) * 0.12;
                albedo = lerp(albedo, _SecondaryColor.rgb, mortar);
                // Moss / weathering low on walls.
                albedo = lerp(albedo, half3(0.3, 0.42, 0.28), saturate(Noise(uv * 0.8) - 0.6) * (top != 0.0 ? 0.3 : 0.6));
            #elif defined(_PATTERN_CONCRETE)
                float2 size = float2(2.4, 1.2);
                float2 f = frac(uv / size);
                float seam = 1.0 - Band(f.x, 0.006, 0.994, 0.003) * Band(f.y, 0.01, 0.99, 0.003);
                float2 holes = frac(uv / size * float2(3.0, 2.0)) - 0.5;
                float tie = top != 0.0 ? 0.0 : step(length(holes * size / float2(3.0, 2.0)), 0.025);
                float streak = top != 0.0 ? 0.0 : Noise(float2(uv.x * 6.0, uv.y * 0.15)) * 0.18;
                albedo *= 0.9 + Noise(uv * 3.0) * 0.12 - streak;
                albedo = lerp(albedo, _SecondaryColor.rgb, seam * 0.6 + tie * 0.8);
            #elif defined(_PATTERN_WOOD)
                float plank = 0.22;
                float along = top != 0.0 ? uv.x : uv.y;
                float across = top != 0.0 ? uv.y : uv.x;
                float id = floor(across / plank);
                float f = frac(across / plank);
                float gap = 1.0 - Band(f, 0.05, 0.95, 0.02);
                float grain = sin((along + Hash(float2(id, 3.0)) * 10.0) * 9.0 + Noise(float2(along * 0.6, id)) * 6.0) * 0.5 + 0.5;
                albedo *= 0.8 + Hash(float2(id, 1.0)) * 0.25;
                albedo *= 0.92 + grain * 0.1;
                albedo = lerp(albedo, _SecondaryColor.rgb, gap);
            #endif
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo;
                half3 emission = _EmissionColor.rgb;
            #if defined(_PATTERN_FACADE)
                half3 windowLight;
                Facade(input.positionOS, input.normalOS, input.positionWS, albedo, windowLight);
                emission += windowLight;
            #elif defined(_PATTERN_CORRUGATED)
                Corrugated(input.positionOS, input.normalOS, albedo);
            #else
                WorldPattern(input.positionWS, input.normalWS, albedo);
            #endif

                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndl = dot(normal, light.direction);
                float lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl);
                lit *= step(0.5, light.shadowAttenuation) * 0.85 + 0.15 * light.shadowAttenuation;
                half3 color = lerp(albedo * _ShadeColor.rgb, albedo * light.color, lit);
                color += albedo * SampleSH(normal) * 0.3;
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                color += _RimColor.rgb * pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower) * _RimStrength;
                color += emission;
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
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fogFactor : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = _OutlineWidth * (1.0 - saturate(_NyxOutlineDisable)) * clamp(distance * 0.08, 1.0, 6.0);
                positionWS += normalWS * width;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1.0); }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
