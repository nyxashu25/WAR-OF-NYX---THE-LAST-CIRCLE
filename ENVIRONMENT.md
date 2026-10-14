# Environment: weather, time of day, vegetation

_Environment pass, 2026-10-14. Everything below is procedural PLACEHOLDER art (original code-built geometry), meant to be replaced by artist-made assets without changing the systems._

## What a match looks like now
- **Time of day:** each match is Dawn, Day, Dusk or Night. Night is moonlit with stars and a neon horizon, and stays bright enough to play.
- **Weather:** Clear, Overcast, Rain, Storm (lightning + thunder) or Fog. In 45 % of matches the weather turns once, between 2:30 and 5:30, to a neighbouring state (for example Clear → Overcast or Rain → Storm). The change blends over 45 s.
- **Sky:** drifting procedural clouds whose cover follows the weather, a sun or moon disc with a halo, twinkling stars, and lightning flashes.
- **Light:** sun angle and colour per time of day. Clouds dim the sun and soften the shadows. Ambient and fog colour follow the sky.
- **Sea:** animated waves (procedural normals), sky reflection, hard anime sun glints. Rain roughens the water.
- **Vegetation on NYX ISLAND** (about 1 250 trees, 850 bushes, 280 rocks):
  - The Shadow Forest is dense, with violet-tinted canopies and a winding lane kept open.
  - Cherry blossoms surround Crimson Temple, with drifting petals.
  - Palms line the coast, and pines grow in Eclipse Valley and around Iron Fortress.
  - The open fields have broadleaf groves.
  - Instanced grass and flower fields cover the ground.
  - Faceted, mossy rocks are scattered across the island.
  - Worn dirt paths run along the routes between locations, with sandy beaches at the coast.
- **Wind:** grass, leaves, palm fronds and rain all move with one wind that slowly changes direction. Stronger weather means stronger wind.
- **Ambient life:** bird flocks in calm daylight, fireflies on calm nights, cherry petals near the temple.
- **Sound:** rain, wind gusts, and thunder delayed by distance (about 343 m/s). All are procedural placeholders.

## Map detail (build version 3)
- **Procedural surface patterns** (`WarOfNyx/NyxSurface`, no textures):
  - building facades: windows fitted to every wall, frames, sills, floor ledges, shopfronts with doors and neon strips, lit rooms at night;
  - corrugated containers with doors;
  - metal panels with rivets, stone brickwork, formwork concrete and wood planks.
- **Props** (`NyxIslandDetails.cs`):
  - streets with lane markings, street lamps, parked cars, vending machines, benches, bins and barricades;
  - rooftop AC units, water tanks, antennas, billboards, balconies and awnings;
  - pagoda roofs and torii beams, paper and stone lanterns, banners;
  - battlements, sandbag nests, crates, barrels, tents, flags and floodlights;
  - bollards, boats, buoys and crane hooks; pipework, consoles and a rune ring at NYX CORE;
  - fences, signposts and wrecked cars in the fields; broken columns in Eclipse Valley.
- Solid-looking props (cars, crates, barrels, sandbags, machines) have colliders: they are cover and part of the NavMesh. Small decoration has none.
- **Grass:** three clump types: meadow, fine lawn (near paths and in the forest) and tall tufts with seed heads.

## Sound (2026-10-14, procedural placeholders)
- **Gunfire:** each gun has its own layered shot (crack, blast, thump, action sound, outdoor echo), 3 variants with pitch jitter. Beyond 45 m a muffled distant boom is used, audible to about 450 m (`GunAudio`).
- **Reloads:** staged per weapon and timed to the reload length:
  - rifles and SMGs: magazine release, out, in, bolt rack;
  - pistol: magazine change, then slide release;
  - shotgun: shell by shell, then pump;
  - marksman: bolt open, magazine change, bolt close.

  A dry click sounds on an empty magazine. Switching weapons cuts the reload sound.
- **Movement:** a footstep on every foot contact of the gait, by surface (grass, dirt / sand, stone / concrete, metal, wood). Steps get louder and carry further from walk (≈ 15 m) to sprint (≈ 40 m); crouching is quiet. Jumps, landings (by fall speed) and slides have their own sounds. Other players' steps are positional (`FootstepAudio`, `CharacterAudio`).
- Voices are pooled (48), so automatic fire from many characters doesn't allocate per shot.

## Rules that keep it fair
| Rule | Where |
|---|---|
| Weather is **visual and audio only**: it never changes movement, damage, the circle, loot or bots' decisions | `WeatherSystem` writes only lighting, fog, shader globals, particles and sounds |
| Every player of a match sees the **same** weather: it comes from the match seed (offline) or a replicated seed (online) | `Atmosphere.Plan(seed)`; `NetworkNyxCircle.NetAtmosphereSeed` |
| Fog never closes in further than **250 m** | `Atmosphere.MinimumVisibilityMetres` (tested) |
| Grass is shorter than a crouched player (**≤ 0.55 m**, ≤ 0.6 m after scaling), so lower quality tiers, which draw less grass, never reveal anyone | `VegetationMeshes.MaxGrassHeight` (tested) |
| Bushes, trees and rocks are cover and are culled the **same way on every tier** (LOD bias 1.0 on Low, Medium and High) | `ProjectSetupWizard.ConfigureQualityLevels` |
| Bushes hide players **from bots too**: a sight-only trigger volume on the `Foliage` layer blocks bot line of sight beyond 4 m. Bullets and players pass through bushes | `BotController.HiddenByFoliage`, `EnvironmentLayers` |
| Tree trunks and rocks stop bullets and movement, but canopies do not | Prefab colliders (`EnvironmentArtBuilder`) |
| "Reduce flashing" turns off the lightning flash and bolt (thunder stays) | `WeatherSystem` |

## Phone budget per quality tier
| | Low | Medium | High |
|---|---|---|---|
| Grass density / draw distance | 0.3 / 25 m | 0.55 / 45 m | 0.9 / 80 m |
| Shadows | 30 m, 1 cascade, hard, 1024 | 60 m, 2 cascades, soft, 2048 | 120 m, 4 cascades, soft, 2048 |
| Max rain drops / s | 350 | 1 100 | 2 200 |
| Fireflies, petals | reduced | full | full |
| Outlines, post-processing | off | on | on |

The vegetation uses a few shared meshes, with two levels of detail (LOD0 under about 3 500 vertices, LOD1 under 60 % of that). The SRP batcher draws them, and they are not static-batched, to keep memory low. These numbers are estimates: **frame rate on real phones has not been measured yet** (PERFORMANCE.md §2).

## Code map
| Piece | File |
|---|---|
| Atmosphere plan (pure C#, deterministic, tested) | `Assets/Scripts/Core/Weather/Atmosphere.cs` |
| Runtime weather: light, fog, sky, wind, rain, lightning, audio | `Assets/Scripts/Gameplay/World/WeatherSystem.cs` |
| Colours per time of day | `Assets/Scripts/Gameplay/World/TimeOfDayLighting.cs` |
| Birds, fireflies, petals | `Assets/Scripts/Gameplay/World/AmbientLife.cs`, `PetalSource.cs` |
| Procedural trees, grass, flowers, bushes, rocks | `Assets/Scripts/Gameplay/World/VegetationMeshes.cs` |
| Grass density per tier | `Assets/Scripts/Gameplay/World/TerrainDetailQuality.cs` |
| Shaders | `Assets/Art/Shaders/NyxFoliage.shader`, `NyxWind.hlsl`, `NyxWater.shader`, `NyxSky.shader`, `NyxAdditive.shader` |
| Asset builder (meshes, materials, prefabs, terrain textures, `Foliage` layer) | `Assets/Scripts/Editor/EnvironmentArtBuilder.cs` |
| Island placement (biomes, paths, grass map) | `Assets/Scripts/Editor/NyxIslandBuilder.cs` (`BuildVersion` 2: setup rebuilds older islands) |
| Screenshot runs | editor: `ShowcaseCapture.Capture`; built game: `WarOfNyx.exe -devCapture <folder>` |

## Replacing the placeholder art
- **Trees, bushes, rocks:** replace the prefabs in `Assets/Art/Vegetation/Prefabs` and keep these properties:
  - an LOD group
  - a trunk collider for trees
  - a trigger collider on the `Foliage` layer for bushes
  - the same culling sizes on every tier

  Materials can use any URP shader. Use `WarOfNyx/NyxFoliage` (vertex alpha = wind weight) to keep the wind.
- **Grass:** replace the `Grass_*` / `Flowers_*` prefabs. They must stay ≤ 0.55 m tall, which a test checks.
- **Sky, clouds, water:** the shaders take colours from `TimeOfDayLighting`, so artists can retune each time of day there.
- **Sounds:** replace `ProceduralAudio.RainLoop`, `WindLoop` and `Thunder` with designed clips.

## Known limits
- Rain falls through roofs (no collision, to save phone CPU).
- Weather doesn't make surfaces look wet.
- The grass and ground are a stylized, anime look, not photoreal. A photoreal look would need different shaders and textured assets, and would cost much more on phones.
- None of this has been run on a phone yet.
