# Building KAI in VRoid Studio

KAI, the Abyss Walker, is WAR OF NYX's dark supernatural swordsman. This guide turns his design sheet ([CHARACTER_PIPELINE.md](CHARACTER_PIPELINE.md) §3) into VRoid Studio choices. It should take one to two hours.

The game imports the exported `.vrm` file and keeps everything VRoid made:
- the humanoid skeleton;
- the face expressions (blink, mouth shapes, angry, joy and so on);
- the hair and coat physics bones;
- the textures.

## 0. Install

1. Download **VRoid Studio** (free, by pixiv) from <https://vroid.com/en/studio>, or from Steam.
2. Install it, then create a **New** character with the **male** base.

**Optional:** export one of VRoid's sample avatars right away as `Sample.vrm` (see step 6) and put it in `ArtSource/Characters/`. I can then test the whole import path with a real VRoid file while you build KAI. The sample is only for testing and is not shipped.

## 1. Body: tall, lean, long-limbed (about 7.5 heads)

| Setting | Aim |
|---|---|
| Height | Tall. Target 1.80 m (the game scales to this anyway). |
| Head size | A little smaller than the default, for adult proportions. |
| Neck | Natural length and thickness: not thin, not long. |
| Shoulders | Slightly broader than the default, defined but not bulky. |
| Chest / waist | Lean: a narrow waist with a little taper from the chest. |
| Arms / legs | Long. Legs a touch longer than the default. |
| Hands | Default. The game curls the fingers around weapons. |

Keep the **default T-pose**. Don't pose or rotate anything before export.

## 2. Face: calm, sharp, dangerous

- **Face shape:** a sharp V-shaped jaw with a narrow chin. Cheeks slightly hollow. A mature face, not a child's.
- **Eyes:** narrow and almond-shaped, with the outer corners slightly upturned. Keep them a little smaller than the VRoid default.
  - **Iris:** dark violet, from about `#5A2E8C` at the centre to `#2A1440` at the edge, with a darker ring around the iris edge.
  - **Highlights:** two small ones (upper left and lower right).
  - **Whites:** slightly warm white, not pure white.
- **Eyeliner and lashes:** a heavy upper lash line with a short flick at the outer corner. Thin lower lashes.
- **Eyebrows:** straight and calm, dark charcoal, slightly thick at the inner end.
- **Nose and mouth:** minimal. Small and neutral, in anime style.
- **Skin:** pale and slightly warm, about `#F3D9C8`. Soft cheek shading only.
- **Scar:** a thin old scar running vertically across the **left** eye. Use the face texture editor (paint layer): a pale line from the brow to the cheekbone, about 2–3 px wide at 2048 px, slightly pinker than the skin.

**Don't copy any existing anime character's face.** KAI must be original.

## 3. Hair: medium, layered black hair with a violet sheen

- **Length and parting:** medium layered hair. The back reaches the nape, and side strands reach the jaw. The bangs part **off-centre** and fall across the forehead, with one longer strand near KAI's right eye.
- **Volume:** layered and slightly messy, not spiky. Use several hair groups (front, sides, back) so it reads as layers.
- **Colour:**
  - base near-black with a blue-violet tint, about `#14111C`;
  - highlight and sheen violet, about `#6B3FA0`;
  - tips can fade slightly toward `#2B1A44`.
- **Physics:** turn on hair swaying (spring bones) for the back and side groups, and keep the bangs stiffer. The game drives these bones while running and turning.

## 4. Outfit: long black coat over fitted armour

Use VRoid's outfit categories. Edit colours and textures with the built-in texture editor.

| Piece | Look |
|---|---|
| Coat | A **long black coat** reaching the knees, about `#121018`, with a **high collar**. Inside lining **purple**, about `#3A1E5C`. Thin **violet piping** along the hems and the front edge, about `#8A4FD8`. If no coat item is long enough, add a black long-skirt / one-piece layer under the jacket as the coat tails. |
| Under the coat | A fitted black undersuit with **dark armour plates** on the chest and shoulders: charcoal `#24222B` with silver edges `#9A9AA6`. |
| Trousers | Black tactical trousers, slightly loose, with straps at the thighs. |
| Boots | Black armoured boots to mid-shin, with silver toe caps and buckles. |
| Gloves | Black **fingered** gloves, not mittens. |
| Emblem | A small violet crescent or abyss mark on the back of the coat. |

**Licensing:** use only VRoid's built-in items and your own edits. Avoid paid or downloaded outfit textures unless their licence allows commercial games.

## 5. Look check before export

- **Viewing:** look at KAI from the front, the side and behind. He should read as tall, dark and long-coated, with a violet accent.
- **Expressions:** test the preview expressions (blink, joy, angry, sorrow). The eyes should close fully on blink.
- **Clipping:** move the preview pose or animation and check that the coat and hair don't cut through the body.

## 6. Export (VRM)

From the **camera / export** menu, choose **Export VRM**:

| Option | Setting |
|---|---|
| VRM version | **1.0** (0.0 also works). |
| Polygon reduction | Moderate. Aim for **under ~40,000 triangles** in total; VRoid shows the count. |
| Material reduction | Reduce to **2–4 materials**, with a **2048 × 2048** texture atlas. |
| Bone reduction | Keep the hair and coat bones. Don't remove them. |
| Blend shapes | **Keep the expressions.** |
| Avatar information | Name **KAI**, author: you. Allow **commercial use**, because this is your own creation. |

Save the file as **`KAI.vrm`** in:

```
C:\Users\digig\Desktop\WAR OF NYX\ArtSource\Characters\
```

Then tell me. I'll import it with its own skeleton, expressions and hair and coat physics, put it in KAI's slot with his movement style and weapons, and send you studio and in-game screenshots.
