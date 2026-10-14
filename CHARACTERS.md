# WAR OF NYX: Characters

_Character bible, 2026-10-14. The models in the game are detailed **procedural placeholders** built in code (`CharacterModels.cs`); artists can use this page as the brief for final models._

**Fairness rule:** characters are cosmetic only. Every character has the same hit shapes (head sphere and body capsule on the character root), the same speed and the same health. Builds change only how wide the visual body is, within the shared hit shapes. Heights are identical.

## The roster

### KAI, The Shadow Ronin (Blade Duelist, lean build)
The last sworn guardian of the fallen Nyx clan. When the reactor tore the sky open, Kai's oath had no one left to protect, so he swore it to the fight itself. He moves in silence and finishes things with the blade.

| Detail | Why it's there |
|---|---|
| Violet haori over a fitted black uniform, open at the front, trimmed lapels, clan crest on the back | Clan swordsman's dress |
| Katana scabbard on the **left** hip, mouth forward | A right-handed draw pulls across the body from the left hip |
| Wide sleeves, bandaged forearms, fingerless gloves | Grip on the hilt, wrists protected |
| Obi sash with a knot and tassel; hakama-wide trousers; ankle wraps | Traditional kit |
| Scarf with two tails, coat panels front and back | Cloth that trails when he runs (`ClothDynamics`; front panels follow the thighs) |
| Scar across the left eye; stern brows | His history |
| Spiky black hair with violet tips | Clan colour |

### AKIRA, The Crimson Hunter (Assault Tracker, athletic build)
A bounty hunter who tracks targets across Nyx Island for a living. Methodical and patient, always counting rounds.

| Detail | Why it's there |
|---|---|
| Chest rig with **three rifle magazine pouches** (flaps, buckles), shoulder straps | Spare magazines for the rifle |
| Pistol holster on the **right** thigh, two leg straps | Backup sidearm for the shooting hand |
| Cropped black tactical jacket with crimson side panels, high collar, sleeves pushed up | Freedom of movement for aiming |
| Belt with buckle, dump pouch on the left hip, cargo pockets | Empty magazines and kit |
| Knee pads with crimson bands, laced combat boots | Kneeling to shoot |
| Crimson hunter's armband, single earring | Guild mark, personal |
| Swept-back red hair, two loose strands | Keeps hair out of the sights |

### YUNA, The Neon Phantom (Recon Hacker, slender build)
A runaway from the island's research labs who opens drones and doors faster than most can reload.

| Detail | Why it's there |
|---|---|
| Holographic half-visor over the right eye | Live scans while aiming |
| Wrist computer on the **left** forearm, glowing screen | Intrusion kit, operated by the free hand |
| Earpiece with antenna on the right ear | On every channel |
| White techwear jacket with cyan light piping, zip opening, hood folded down, back tail | Lab-issue gear, modified |
| Shorts over a black bodysuit, light-up thigh straps and boot soles | Mobility, visibility to her own drones |
| Blue bob with a straight fringe, glowing hair clip | Personal style |

### REN, The Iron Wolf (Heavy Vanguard, heavy build)
A veteran of the reactor wars who lost his left arm to the Nyx Core and rebuilt it himself.

| Detail | Why it's there |
|---|---|
| **Mechanical left arm** (segmented metal, glowing amber elbow, wrist, forearm strip and knuckles) | The arm he rebuilt |
| Oversized layered left pauldron with an amber emblem, smaller right pauldron | Protects the prosthetic side |
| **Shotgun-shell bandolier** from the right shoulder to the left hip, red shells with brass caps | Ammunition for his shotgun |
| Ribbed chest and back plates, abdominal plates | Front-line armour |
| Wolf-fur mantle around the collar | His call sign |
| Dog tags on a chain | His old unit |
| Heavy belt with shell pouches, knee armour, cargo trousers | Kit |
| Silver crest hair, undercut, beard and moustache, scar on the right cheek | Veteran |

## Faces and hair (detail pass)
- **Skin:**
  - cheek blush and a soft shadow under the jaw, painted into the head mesh;
  - a nose bridge with a tip highlight and nostrils;
  - lips with a cupid's-bow upper line and a fuller lower lip with a shadow beneath;
  - inner ears.
- **Eyes:**
  - sclera with a lid shadow;
  - an iris in layers (dark limbal ring, colour, lighter lower half, darker upper band);
  - a pupil, two highlights, a thick upper lash with a flick, a thin lower lash and an eyelid crease;
  - blinking.
- **Brows:** three overlapping strokes with a tapered tail; the angle sets the expression (KAI and REN stern, YUNA soft).
- **Hair:** built from layered locks (a main strand plus two finer strands), highlight streaks and an "angel ring" on the cap.
  - KAI: crown and back spikes, a fringe, side locks and an ahoge, with violet tips.
  - AKIRA: swept back in layers, side locks, nape flicks and a **tied ponytail that swings** (a hunter keeps hair out of the sights).
  - YUNA: a two-layer bob, a straight fringe, an asymmetric longer left lock, an ahoge and a glowing clip.
  - REN: an undercut with shaved-side lines, a two-row crest, a full beard with a braided chin tuft, and a moustache.
- **Personal marks:** KAI's scar has stitch marks; AKIRA has a beauty mark under the left eye; YUNA has freckles; REN has crow's feet and a forehead line.
- **YUNA's visor** is a glowing frame with a targeting reticle over the right eye, so her eye stays visible.

## Materials and outfit detail (2026-10-14)
Every surface now has a real material, lit by its own rules in `WarOfNyx/NyxToonVC`:
- **Fabrics:** each has a woven pattern you can see up close.
  - Ripstop grid on tactical cloth, twill on coats and trousers, knit ribs on scarves and cuffs.
  - Quilted diamonds on padded panels, and a hex mesh on bodysuits.
  - Each fabric has a soft sheen at grazing angles.
- **Leather:** grain and a glossy highlight.
- **Brushed metal:** stretched highlights and tinted reflections.
- **Polymer armour:** a clear-coat highlight.
- **Carbon fibre:** twill tows that catch the light in alternate directions.
- **Lacquer:** for KAI's armour and scabbard.
- **Hair:** anime highlight bands that follow each strand.
- **Fur:** a soft sheen.
- **Skin:** warm shadows.
- **Eyes:** wet highlights.
- **Circuits:** glowing lines with light flowing along them.

Patterns fade out with distance, so they never shimmer. On the Low graphics setting they are switched off.

| Character | What the suit adds |
|---|---|
| KAI | A bokashi-dyed haori: violet at the hem, fading to black at the shoulders. Drape folds, satin lapels with a glowing thread, and silver seigaiha wave embroidery around the hem. A lamellar chest guard (*do*) in the open front: lacquered plates in five rows, gold-edged and laced with violet cord. A lamellar shoulder guard (*sode*) over the left sleeve, splinted bracers (*kote*) and shin guards (*kyahan*). A twisted cord around the obi, with a silver clan plate. A glowing tomoe crest on the back. The scabbard has gold fittings and a wrapped cord. Fringed scarf tails. |
| AKIRA | A leather jacket with quilted crimson side panels and quilted shoulders, a half-open metal zipper and double-stitched seams. A plate carrier with MOLLE webbing, three open-top pouches showing rifle magazines with shock cords, a glowing hunter's patch and side-release buckles. A cobra-buckle belt with a carabiner, a polymer holster with a pistol, cargo pockets with snaps, polymer elbow and knee pads, and laced boots with brass eyelets. |
| YUNA | A white ripstop jacket with a polymer yoke and a glowing zipper and hem. Circuit traces run across the back and down the sleeves, with light flowing along them. A "data spine" on the back: a polymer module with carbon cooling fins, glowing slots and cables. Polymer shoulder caps, thigh plates and shin guards, each with a glowing line. A wrist computer with buttons, glowing soles and a hex-mesh bodysuit. |
| REN | Brushed-steel chest and back plates with pectoral plates, a panel line, rivets and battle scratches, plus overlapping abdominal plates. A wolf-fur mantle of up to 44 tufts. A stitched leather bandolier of red shells with brass caps. A ball-chain with dog tags. A riveted pauldron with three glowing claw marks. A mechanical arm with a carbon-fibre core, steel armour, hydraulic pistons with polished rods, cables and amber joints. Steel toe caps and a leather belt with snap pouches. |

All base clothing has cloth folds: creases at the elbows, knees, waist and ankles, and knife pleats on KAI's hakama. The folds darken their own creases (baked occlusion). Seams carry running stitches, gloves have stitching and knuckle guards, and boots have welts, tread lugs, toe caps and heel counters.

### Detail levels
Each character is built three times, and a `LODGroup` swaps between them by size on screen:

| Level | Used for | What it includes | Vertex budget |
|---|---|---|---|
| LOD 0 | Within about 10 m | Everything above: smooth resampled surfaces, stitches, zipper teeth, rivets, embroidery, iris fibres, single lashes | ≤ 90 000 |
| LOD 1 | About 10–35 m | Same outfits, without the smallest details | ≤ 34 000 |
| LOD 2 | Beyond that | Simplified shapes | ≤ 14 000 |

The budgets are checked by `CharacterModelTests` for every character. Each level has the same parts, so the swap is seamless.

## Shared anatomy (all characters)
- **Body:** sculpted torso, muscled limbs (deltoids, calves, forearm swell), gloved hands with curled fingers and thumbs.
- **Head:** anime proportions with a V jaw, ears and nose.
- **Eyes:** sclera, iris with a darker ring, pupil, two highlights, and a thick upper lash line with a flick. The eyes **blink** every 2–6 s (`FaceAnimator`).
- **Face:** brows angled per personality, and a mouth.
- **Hair:** strand-built hair per character, with an "angel ring" highlight.
- **Motion:** the jointed skeleton is animated by `CharacterAnimator` (gait, IK, weapon holding, melee combos).

## Technical notes
- One combined mesh per body part, coloured with vertex colours (`WarOfNyx/NyxToonVC`); glowing details use the colour's glow channel, and a second UV channel carries each vertex's material, occlusion, outline weight and weave. A character is about 16–28 renderers per detail level (see the budgets above, `CharacterModelTests`). Meshes are built once per character and level, and shared.
- **CHARACTERS** screen in the main menu: 3D preview holding the signature weapon (drag to turn), name, title, role, story, gear and SELECT.
- Signature weapons in the preview: KAI katana, AKIRA NX-7 assault rifle, YUNA Shadow SMG, REN Crimson shotgun. Match loadouts stay random (fairness).
- Replacing with final art: swap the meshes per joint (names in `CharacterModelTests.Parts`) or switch to a rigged model with an Animator (the path exists in `PlayerAnimationController`).
