// WAR OF NYX - shared wind for vegetation (grass, flowers, bushes, trees).
// Globals set by WeatherSystem every frame:
//   _NyxWind     xz = wind direction (normalized), y = unused, w = strength 0..1
//   _NyxWindTime   seconds (kept separate from _Time so a paused game also pauses the wind)
// Vertex colour alpha = how much a vertex may bend (0 at roots / trunk base, 1 at grass tips and leaf ends).
#ifndef NYX_WIND_INCLUDED
#define NYX_WIND_INCLUDED

float4 _NyxWind;
float _NyxWindTime;

// Bends a world-space position with the wind. `weight` = vertex colour alpha, `amount` = material amplitude (metres).
float3 NyxApplyWind(float3 positionWS, float weight, float amount, float frequency)
{
    float strength = saturate(_NyxWind.w);
    float2 direction = _NyxWind.xz;
    if (dot(direction, direction) < 1e-4)
        direction = float2(0.8, 0.6);

    // Phase from world position: neighbouring plants move together, distant ones don't.
    float phase = dot(positionWS.xz, float2(0.071, 0.053));
    float t = _NyxWindTime * frequency;
    float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.25;
    // Slow gusts travelling along the wind direction.
    float gust = saturate(sin(dot(positionWS.xz, direction) * 0.03 - _NyxWindTime * 1.4) * 0.5 + 0.5);
    float bend = (0.25 + 0.75 * strength) * (sway + gust * strength * 1.2);

    float3 offset;
    offset.xz = direction * bend * amount * weight;
    // Small perpendicular flutter so leaves don't move like a rigid plate.
    offset.xz += float2(-direction.y, direction.x) * sin(t * 3.1 + phase * 2.9) * 0.15 * amount * weight;
    offset.y = -abs(bend) * amount * weight * 0.25; // tips dip slightly when bent
    return positionWS + offset;
}

#endif
