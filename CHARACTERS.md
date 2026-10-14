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

## Shared anatomy (all characters)
- **Body:** sculpted torso, muscled limbs (deltoids, calves, forearm swell), gloved hands with curled fingers and thumbs.
- **Head:** anime proportions with a V jaw, ears and nose.
- **Eyes:** sclera, iris with a darker ring, pupil, two highlights, and a thick upper lash line with a flick. The eyes **blink** every 2–6 s (`FaceAnimator`).
- **Face:** brows angled per personality, and a mouth.
- **Hair:** strand-built hair per character, with an "angel ring" highlight.
- **Motion:** the jointed skeleton is animated by `CharacterAnimator` (gait, IK, weapon holding, melee combos).

## Technical notes
- One combined mesh per body part, coloured with vertex colours (`WarOfNyx/NyxToonVC`); glowing details use the colour's glow channel. A character is about 16–25 renderers and up to about 24 000 vertices (`CharacterModelTests`). Meshes are built once per character and shared.
- **CHARACTERS** screen in the main menu: 3D preview holding the signature weapon (drag to turn), name, title, role, story, gear and SELECT.
- Signature weapons in the preview: KAI katana, AKIRA NX-7 assault rifle, YUNA Shadow SMG, REN Crimson shotgun. Match loadouts stay random (fairness).
- Replacing with final art: swap the meshes per joint (names in `CharacterModelTests.Parts`) or switch to a rigged model with an Animator (the path exists in `PlayerAnimationController`).
