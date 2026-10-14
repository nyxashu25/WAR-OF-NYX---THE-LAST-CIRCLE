// WAR OF NYX - NyxToon with VERTEX COLOURS and SURFACE MATERIALS (detailed procedural characters).
// albedo = vertex rgb x _BaseColor; glow = vertex rgb x (1 - vertex alpha) x _VertexGlow (alpha 1 = not glowing).
// uv2 = (surface material, baked occlusion, outline weight, weave pattern) written by MeshKit (Surf / Weave ids):
//   0 cloth, 1 skin, 2 woven fabric, 3 leather, 4 brushed metal, 5 polymer armour, 6 hair, 7 carbon fibre,
//   8 rubber, 9 fur, 10 circuit (flowing glow), 11 eye, 12 polished gold / brass, 13 lacquer.
// Anime cel lighting (two bands, tinted shade) plus per-material stylised specular: banded clear-coat highlights,
// tinted metal reflections from a procedural environment, Kajiya-Kay hair highlights along the strand tangent,
// fabric sheen, warm skin shadows. Woven micro-patterns (ripstop, twill, knit rib, quilting, hex mesh, carbon twill,
// leather grain, brushed lines) perturb the normal in metre-based uvs and fade out before they would shimmer.
// Low quality tier: _NyxSurfaceDetailOff = 1 skips the micro-patterns; _NyxOutlineDisable = 1 skips outlines.
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
        _DetailStrength ("Micro-pattern strength", Range(0, 2)) = 1
        _SpecularStrength ("Specular strength", Range(0, 2)) = 1
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
            float _DetailStrength;
            float _SpecularStrength;
        CBUFFER_END

        // Globals set by the quality tier (GraphicsQualityService). 0 = on (default), 1 = off.
        float _NyxOutlineDisable;
        float _NyxSurfaceDetailOff;
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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 surf : TEXCOORD1;
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
                float4 tangentWS : TEXCOORD5;
                // Material id and weave are flat per triangle (panels of different materials stay crisp);
                // occlusion and outline weight interpolate.
                nointerpolation float2 material : TEXCOORD6;
                float ao : TEXCOORD7;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.color = input.color;
                output.material = input.surf.xw;
                output.ao = input.surf.y;
                return output;
            }

            // ---------------------------------------------------------------- noise
            float Hash11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash11(i), Hash11(i + 1.0), f * f * (3.0 - 2.0 * f));
            }
            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), u.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1.0), u.x), u.y);
            }

            // ---------------------------------------------------------------- micro height fields (0..1), uv in metres
            // Returns the height; period = the pattern's repeat (used to fade it out before it aliases).
            float MicroHeight(int id, float pattern, float2 uv, out float period)
            {
                if (id == 2) // woven fabric
                {
                    if (pattern < 0.5) // ripstop: raised grid every 5 mm over a fine weave
                    {
                        period = 0.005;
                        float2 g = abs(frac(uv / 0.005) - 0.5);
                        return smoothstep(0.38, 0.5, max(g.x, g.y)) * 0.75 + (0.5 + 0.25 * (sin(uv.x * 2600.0) + sin(uv.y * 2600.0))) * 0.25;
                    }
                    if (pattern < 1.5) { period = 0.0035; return 0.5 + 0.5 * sin((uv.x + uv.y) * 6.2832 / 0.0035); }   // twill
                    if (pattern < 2.5) { period = 0.004; return 0.5 + 0.5 * cos(uv.x * 6.2832 / 0.004); }                // knit rib
                    if (pattern < 3.5) // quilted diamonds 4.5 cm
                    {
                        period = 0.045;
                        float a = (uv.x + uv.y) / 0.045, b = (uv.x - uv.y) / 0.045;
                        return sqrt(saturate(sin(3.14159 * frac(a)) * sin(3.14159 * frac(b))));
                    }
                    if (pattern < 4.5) // hex mesh 9 mm
                    {
                        period = 0.009;
                        float2 p = uv / 0.009;
                        float2 r = float2(1.0, 1.7320508);
                        float2 h = r * 0.5;
                        float2 a = p - r * floor(p / r) - h;
                        float2 b = (p - h) - r * floor((p - h) / r) - h;
                        float2 gv = dot(a, a) < dot(b, b) ? a : b;
                        float2 q = abs(gv);
                        float d = max(dot(q, float2(0.5, 0.8660254)), q.x);
                        return 1.0 - smoothstep(0.36, 0.5, d);
                    }
                    period = 0.0016; // fine plain weave
                    return 0.5 + 0.5 * sin(uv.x * 3900.0) * sin(uv.y * 3900.0);
                }
                if (id == 3) { period = 0.0025; return Noise2(uv / 0.0025) * 0.65 + Noise2(uv / 0.011) * 0.35; } // leather grain
                if (id == 4) { period = 0.0008; return Noise1(uv.x / 0.0008) * 0.7 + Noise1(uv.x / 0.0031) * 0.3; } // brushed lines
                if (id == 7) // carbon twill: 7 mm tows alternating direction
                {
                    period = 0.007;
                    float2 cell = floor(uv / 0.007);
                    float2 f = frac(uv / 0.007);
                    float alt = fmod(abs(cell.x + cell.y), 2.0);
                    return alt < 0.5 ? sin(3.14159 * f.x) : sin(3.14159 * f.y);
                }
                if (id == 9) { period = 0.002; return Noise1(uv.x / 0.002); }       // fur fibres
                if (id == 6) { period = 0.0015; return Noise1(uv.x / 0.0015); }     // hair fibres
                period = 1.0;
                return 0.5;
            }

            // A procedural studio environment for reflections: ambient sky (SH), a bright horizon band, the sun.
            float3 Environment(float3 r, Light light)
            {
                float3 sky = SampleSH(r) * 1.25 * lerp(0.55, 1.0, saturate(r.y * 0.5 + 0.5));
                float horizon = exp(-abs(r.y - 0.05) * 9.0);
                float sun = pow(saturate(dot(r, light.direction)), 64.0);
                return sky + horizon * 0.35 * light.color + sun * 1.5 * light.color;
            }

            float Band(float x, float edge) { return smoothstep(edge - 0.05, edge + 0.05, x); }

            half4 frag(Varyings input) : SV_Target
            {
                int id = (int)(input.material.x + 0.5);
                float weave = input.material.y;
                float ao = input.ao;
                float3 normal = normalize(input.normalWS);
                float3 tangent = normalize(input.tangentWS.xyz - normal * dot(input.tangentWS.xyz, normal) + 1e-5);
                float3 across = cross(normal, tangent) * (input.tangentWS.w >= 0.0 ? 1.0 : -1.0);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw) * _BaseColor;
                albedo.rgb *= input.color.rgb;

                // Micro-patterns: bump the normal (finite differences in metre uvs), fade before aliasing.
                float pattern = 0.5;
                if (_NyxSurfaceDetailOff < 0.5 && (id == 2 || id == 3 || id == 4 || id == 7 || id == 9 || id == 6))
                {
                    float period;
                    float h0 = MicroHeight(id, weave, input.uv, period);
                    float e = period * 0.08;
                    float hu = MicroHeight(id, weave, input.uv + float2(e, 0), period);
                    float hv = MicroHeight(id, weave, input.uv + float2(0, e), period);
                    float footprint = max(fwidth(input.uv.x), fwidth(input.uv.y));
                    float fade = saturate(1.0 - footprint / (period * 0.6));
                    float strength = (id == 4 ? 0.06 : id == 2 && weave > 2.5 && weave < 3.5 ? 0.35 : id == 7 ? 0.22 : 0.16) * fade * _DetailStrength;
                    float2 slope = float2(hu - h0, hv - h0) / e * period;
                    normal = normalize(normal - strength * (slope.x * across + slope.y * tangent));
                    pattern = lerp(0.5, h0, fade);
                    // Woven cavities and carbon tows read in the colour too.
                    albedo.rgb *= id == 7 ? lerp(0.7, 1.25, pattern) : lerp(1.0 - 0.18 * fade, 1.0, pattern);
                }

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndl = dot(normal, light.direction);
                float ndv = saturate(dot(normal, viewDirection));
                float3 halfDir = normalize(light.direction + viewDirection);
                float ndh = saturate(dot(normal, halfDir));

                // Cel bands: skin and cloth get a softer terminator; hard materials a crisp one.
                float softness = _ShadeSoftness * (id == 1 ? 3.0 : (id == 0 || id == 2 || id == 9) ? 1.8 : 1.0);
                float lit = smoothstep(_ShadeThreshold - softness, _ShadeThreshold + softness, ndl);
                float shadow = step(0.5, light.shadowAttenuation) * 0.85 + 0.15 * light.shadowAttenuation;
                lit *= shadow;

                float3 shadeTint = id == 1 ? lerp(_ShadeColor.rgb, float3(1.0, 0.6, 0.58), 0.55) : _ShadeColor.rgb;
                float diffuseScale = id == 4 ? 0.45 : id == 12 ? 0.35 : 1.0;
                float occlusion = lerp(1.0, ao, 0.85);
                half3 color = lerp(albedo.rgb * shadeTint, albedo.rgb * light.color, lit) * diffuseScale * occlusion;
                color += albedo.rgb * SampleSH(normal) * 0.3 * occlusion;

                // Specular per material.
                float3 reflected = reflect(-viewDirection, normal);
                float fresnel = pow(1.0 - ndv, 4.0);
                float3 spec = 0;
                if (id == 1) spec = pow(ndh, 48.0) * 0.1 * light.color * lit;                                                     // skin
                else if (id == 0 || id == 2) spec = fresnel * 0.22 * albedo.rgb * light.color * (0.4 + lit);                     // cloth sheen
                else if (id == 3) spec = (Band(pow(ndh, 60.0), 0.45) * 0.22 + pow(ndh, 18.0) * 0.06) * light.color * lit;        // leather
                else if (id == 4 || id == 12) // metals: tinted, anisotropic (brushed) or polished
                {
                    float th = dot(tangent, halfDir);
                    float aniso = id == 4 ? pow(sqrt(saturate(1.0 - th * th)), 140.0) : pow(ndh, 160.0);
                    float3 tint = saturate(albedo.rgb * 1.6 + 0.08);
                    spec = Band(aniso, 0.45) * 1.1 * tint * light.color * lit;
                    spec += Environment(reflected, light) * tint * (id == 12 ? 0.85 : 0.6) * occlusion;
                }
                else if (id == 5 || id == 7 || id == 10 || id == 13) // clear-coated plates, carbon, lacquer
                {
                    float gloss = id == 13 ? 220.0 : 140.0;
                    spec = Band(pow(ndh, gloss), 0.5) * (id == 13 ? 0.9 : 0.55) * light.color * lit;
                    spec += Environment(reflected, light) * (fresnel * 0.6 + 0.05) * (id == 13 ? 0.6 : 0.35) * occlusion;
                    if (id == 7)
                    {
                        float th = dot(pattern > 0.5 ? tangent : across, halfDir);
                        spec += pow(sqrt(saturate(1.0 - th * th)), 60.0) * 0.18 * light.color * lit;
                    }
                }
                else if (id == 6 || id == 9) // hair / fur: Kajiya-Kay along the strand, shifted by fibre noise
                {
                    float shift = (pattern - 0.5) * 0.35 + (id == 6 ? 0.12 : 0.0);
                    float3 t1 = normalize(tangent + normal * shift);
                    float th = dot(t1, halfDir);
                    float sinTH = sqrt(saturate(1.0 - th * th));
                    float3 hairLight = lerp(albedo.rgb * 1.8, float3(1, 1, 1), 0.35);
                    if (id == 6)
                        spec = (Band(pow(sinTH, 90.0), 0.5) * 0.5 * hairLight + pow(sinTH, 10.0) * 0.1 * albedo.rgb) * light.color * (0.25 + lit);
                    else
                        spec = (pow(sinTH, 16.0) * 0.22 * hairLight + fresnel * 0.2 * albedo.rgb) * light.color * (0.3 + lit);
                }
                else if (id == 8) spec = pow(ndh, 16.0) * 0.04 * light.color * lit;                                               // rubber
                else if (id == 11) spec = Band(pow(ndh, 400.0), 0.5) * 0.9 * light.color + fresnel * 0.1;                        // wet eye
                color += spec * _SpecularStrength;

                float rim = pow(1.0 - ndv, _RimPower) * _RimStrength * (id == 4 || id == 12 ? 1.6 : 1.0);
                color += _RimColor.rgb * rim * occlusion;

                // Glow: circuits pulse with light flowing along their length.
                float glow = (1.0 - input.color.a) * _VertexGlow;
                if (id == 10)
                    glow *= 0.55 + 0.45 * sin(_Time.y * 3.0 - input.uv.y * 45.0 - input.uv.x * 8.0);
                color += _EmissionColor.rgb + input.color.rgb * glow;

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
                float4 surf : TEXCOORD1;
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
                // Width grows slowly with distance so far objects keep a readable silhouette; tiny details
                // (stitches, rivets, lashes) carry an outline weight of 0 so they stay crisp.
                float distance = length(GetCameraPositionWS() - positionWS);
                float width = _OutlineWidth * (1.0 - saturate(_NyxOutlineDisable)) * clamp(distance * 0.08, 1.0, 6.0) * input.surf.z;
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
