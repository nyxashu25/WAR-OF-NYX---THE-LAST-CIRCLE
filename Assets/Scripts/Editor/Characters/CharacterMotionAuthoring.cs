using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarOfNyx.Gameplay.Characters;

namespace WarOfNyx.EditorTools.Characters
{
    /// <summary>
    /// A character's movement personality (section 40.6): numbers that shape every authored clip. Gameplay speeds are
    /// identical for everyone (the clips are in place and match the shared walk / run / sprint speeds); only posture,
    /// rhythm and body language differ.
    /// </summary>
    public sealed class MotionStyle
    {
        public string Name = "KAI";
        // Gait cycles (seconds per two steps) and posture.
        public float WalkCycle = 1.1f, RunCycle = 0.72f, SprintCycle = 0.62f;
        public float WalkLean = 2f, RunLean = 9f, SprintLean = 19f;
        public float WalkBob = 0.018f, RunBob = 0.045f, SprintBob = 0.06f;
        public float WalkLift = 0.085f, RunLift = 0.2f, SprintLift = 0.3f;
        public float StanceHalf = 0.1f;
        public float HipSway = 0.012f, HipTwist = 6f, HipRoll = 2.5f, TorsoCounter = 1.2f;
        public float WalkArmSwing = 0.1f, RunArmSwing = 0.2f, SprintArmSwing = 0.32f;
        public float WalkElbow = 0.05f;         // hands forward of the hip at rest
        public float ArmOut = 0.05f;            // hands away from the body
        public float HeadForward = 6f;          // head slightly forward (degrees)
        public float ShoulderDrop = 0.012f;     // relaxed shoulders
        public float HeadStability = 0.85f;     // share of body motion the head cancels
        // Idle.
        public float BreathDegrees = 1.4f, WeightShift = 0.022f, IdleLookDegrees = 9f;
        public Vector3 IdleFootL = new Vector3(-0.115f, 0f, 0.035f), IdleFootR = new Vector3(0.125f, 0f, -0.045f);
        public float FingerCurl = 22f;

        /// <summary>KAI, the Abyss Walker: calm, upright, deliberate; compact, precise motion; minimal arm swing.</summary>
        public static MotionStyle Kai => new MotionStyle();
    }

    /// <summary>Live access to a character's bones for posing (bind = T-pose, identity rotations).</summary>
    public sealed class PoseRig
    {
        public readonly Transform Root;
        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        private readonly Dictionary<Transform, Vector3> _bindPositions = new Dictionary<Transform, Vector3>();

        public PoseRig(GameObject root)
        {
            Root = root.transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                _bones[t.name] = t;
                _bindPositions[t] = t.localPosition;
            }
        }

        public Transform this[string name] => _bones[name];

        public void Reset()
        {
            foreach (var pair in _bindPositions)
            {
                pair.Key.localPosition = pair.Value;
                pair.Key.localRotation = Quaternion.identity;
            }
        }

        public void Rot(string bone, float x, float y, float z) => _bones[bone].localRotation = Quaternion.Euler(x, y, z);

        public void HipsOffset(Vector3 offset) => _bones["Hips"].localPosition = _bindPositions[_bones["Hips"]] + offset;

        /// <summary>Plants a foot: ankle at <paramref name="ankle"/> (root space), knee toward <paramref name="pole"/>, foot pitch and toe bend.</summary>
        public void Leg(string side, Vector3 ankle, float pitch = 0f, float toe = 0f, float yaw = 0f, Vector3? pole = null)
        {
            var upper = _bones[side + "UpperLeg"];
            var lower = _bones[side + "LowerLeg"];
            var foot = _bones[side + "Foot"];
            var knee = pole ?? Root.TransformDirection(new Vector3(side == "Left" ? -0.12f : 0.12f, 0f, 1f));
            LimbIk.Solve(upper, lower, foot, Root.TransformPoint(ankle), knee);
            foot.rotation = Root.rotation * Quaternion.Euler(pitch, yaw, 0f);
            _bones[side + "Toes"].localRotation = Quaternion.Euler(-toe, 0f, 0f);
        }

        /// <summary>Places a hand: wrist at <paramref name="wrist"/> (root space), elbow toward <paramref name="pole"/>, hand rotation (root space).</summary>
        public void Arm(string side, Vector3 wrist, Vector3 pole, Quaternion? hand = null)
        {
            LimbIk.Solve(_bones[side + "UpperArm"], _bones[side + "LowerArm"], _bones[side + "Hand"], Root.TransformPoint(wrist), Root.TransformDirection(pole));
            if (hand.HasValue)
                _bones[side + "Hand"].rotation = Root.rotation * hand.Value;
        }

        /// <summary>Curls the fingers (degrees per joint) and the thumb.</summary>
        public void Fingers(string side, float curl, float thumb)
        {
            float s = side == "Left" ? 1f : -1f;
            foreach (var f in new[] { "Index", "Middle", "Ring", "Little" })
            {
                float extra = f == "Little" ? 1.15f : f == "Ring" ? 1.08f : 1f;
                for (int j = 1; j <= 3; j++)
                    _bones[side + f + j].localRotation = Quaternion.Euler(0f, 0f, s * curl * extra * (j == 1 ? 0.8f : 1f));
            }
            for (int j = 1; j <= 3; j++)
                _bones[side + "Thumb" + j].localRotation = Quaternion.Euler(0f, -s * thumb * 0.5f, s * thumb * (j == 1 ? 0.3f : 0.6f));
        }

        public Vector3 Shoulder(string side) => Root.InverseTransformPoint(_bones[side + "UpperArm"].position);
        public Quaternion ChestRotation => Quaternion.Inverse(Root.rotation) * _bones["UpperChest"].rotation;
    }

    /// <summary>
    /// Bakes authored poses into Unity humanoid clips (muscle space, so they retarget to any humanoid model):
    /// each frame is posed on the real skeleton (IK for feet and hands), read back with a HumanPoseHandler and written
    /// as muscle and root curves. Clips are in place (no root motion); the body offset is baked into the pose.
    /// </summary>
    public static class ClipBaker
    {
        public delegate void Pose(PoseRig rig, float t);

        public static AnimationClip Bake(PoseRig rig, HumanPoseHandler handler, string name, float length, bool loop, Pose pose, int fps = 30)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * fps));
            int muscles = HumanTrait.MuscleCount;
            var curves = new AnimationCurve[muscles + 7];
            for (int i = 0; i < curves.Length; i++)
                curves[i] = new AnimationCurve();
            var human = new HumanPose();
            Quaternion previous = Quaternion.identity;
            for (int f = 0; f <= frames; f++)
            {
                float t = f / (float)frames * length;
                rig.Reset();
                pose(rig, loop && f == frames ? 0f : t);
                handler.GetHumanPose(ref human);
                for (int m = 0; m < muscles; m++)
                    curves[m].AddKey(t, human.muscles[m]);
                curves[muscles].AddKey(t, human.bodyPosition.x);
                curves[muscles + 1].AddKey(t, human.bodyPosition.y);
                curves[muscles + 2].AddKey(t, human.bodyPosition.z);
                var q = human.bodyRotation;
                if (f > 0 && Quaternion.Dot(previous, q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                previous = q;
                curves[muscles + 3].AddKey(t, q.x);
                curves[muscles + 4].AddKey(t, q.y);
                curves[muscles + 5].AddKey(t, q.z);
                curves[muscles + 6].AddKey(t, q.w);
            }
            var clip = new AnimationClip { name = name, frameRate = fps };
            for (int m = 0; m < muscles; m++)
                Set(clip, MuscleProperty(HumanTrait.MuscleName[m]), curves[m]);
            string[] root = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < 7; i++)
                Set(clip, root[i], curves[muscles + i]);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void Set(AnimationClip clip, string property, AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property), curve);
        }

        /// <summary>HumanTrait names finger muscles "Left Index 1 Stretched"; clip curves call them "LeftHand.Index.1 Stretched".</summary>
        public static string MuscleProperty(string muscle)
        {
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                foreach (var side in new[] { "Left", "Right" })
                {
                    string prefix = side + " " + finger + " ";
                    if (!muscle.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    string rest = muscle.Substring(prefix.Length);
                    if (rest == "Spread")
                        return $"{side}Hand.{finger}.Spread";
                    int space = rest.IndexOf(' ');
                    return $"{side}Hand.{finger}.{rest.Substring(0, space)}{rest.Substring(space)}";
                }
            }
            return muscle;
        }
    }

    /// <summary>
    /// The authored motion of a character from its <see cref="MotionStyle"/>: idle and idle variation, directional walk
    /// and run, sprint, crouch, air, slide, seated, hit, death, victory and the seven melee combo attacks.
    /// </summary>
    public static class CharacterMotionAuthoring
    {
        private const float Ankle = 0.085f;

        public static Dictionary<string, AnimationClip> BakeAll(GameObject character, Avatar avatar, MotionStyle style)
        {
            var rig = new PoseRig(character);
            var handler = new HumanPoseHandler(avatar, character.transform);
            var clips = new Dictionary<string, AnimationClip>();
            void Add(string name, float length, bool loop, ClipBaker.Pose pose) => clips[name] = ClipBaker.Bake(rig, handler, name, length, loop, pose);

            // Authoring check (QA log): the authored skeleton pose before conversion.
            foreach (var probe in new[] { ("Idle", 1f), ("WalkF", 0f), ("RunF", 0.2f), ("Melee_HorizontalSlash", 0.36f) })
            {
                rig.Reset();
                if (probe.Item1 == "Idle")
                    Idle(rig, probe.Item2, style);
                else if (probe.Item1 == "RunF")
                    Gait(rig, probe.Item2 / style.RunCycle, Vector3.forward, 1, style, crouch: false);
                else if (probe.Item1.StartsWith("Melee"))
                    Melee(rig, probe.Item2, Core.Combat.MeleePattern.HorizontalSlash, style);
                else
                    Gait(rig, probe.Item2 / style.WalkCycle, Vector3.forward, 0, style, crouch: false);
                Vector3 P(string b) => character.transform.InverseTransformPoint(rig[b].position);
                Debug.Log($"[WarOfNyx][Characters] authored {probe.Item1}@{probe.Item2}: hips {P("Hips")} ankle {P("LeftFoot")} lHand {P("LeftHand")} rElbow {P("RightLowerArm")} rHand {P("RightHand")}");
            }
            Add("Idle", 8f, true, (r, t) => Idle(r, t, style));
            Add("IdleVariant", 4f, false, (r, t) => IdleVariant(r, t, style));
            foreach (var (name, x, z, gait) in new[]
                     {
                         ("WalkF", 0f, 1f, 0), ("WalkB", 0f, -1f, 0), ("WalkL", -1f, 0f, 0), ("WalkR", 1f, 0f, 0),
                         ("RunF", 0f, 1f, 1), ("RunB", 0f, -1f, 1), ("RunL", -1f, 0f, 1), ("RunR", 1f, 0f, 1), ("Sprint", 0f, 1f, 2),
                     })
            {
                float cycle = gait == 0 ? style.WalkCycle : gait == 1 ? style.RunCycle : style.SprintCycle;
                Add(name, cycle, true, (r, t) => Gait(r, t / cycle, new Vector3(x, 0f, z), gait, style, crouch: false));
            }
            Add("CrouchIdle", 4f, true, (r, t) => Crouch(r, t, style));
            Add("CrouchWalk", 0.95f, true, (r, t) => Gait(r, t / 0.95f, Vector3.forward, 3, style, crouch: true));
            Add("Air", 1f, true, (r, t) => Air(r, t, style));
            Add("Slide", 1f, true, (r, t) => Slide(r, t, style));
            Add("Seated", 2f, true, (r, t) => Seated(r, t, style));
            Add("Hit", 0.45f, false, (r, t) => Hit(r, t, style));
            Add("Death", 1.6f, false, (r, t) => Death(r, t, style));
            Add("Victory", 3f, false, (r, t) => Victory(r, t, style));
            foreach (var pattern in (Core.Combat.MeleePattern[])Enum.GetValues(typeof(Core.Combat.MeleePattern)))
                Add("Melee_" + pattern, CharacterAnimatorParams.MeleeClipSeconds, false, (r, t) => Melee(r, t, pattern, style));
            handler.Dispose();
            rig.Reset(); // leave the character in its bind pose
            return clips;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Ease(float a, float b, float t) => Mathf.Lerp(a, b, Smooth(Mathf.Clamp01(t)));
        private static Vector3 Ease(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, Smooth(Mathf.Clamp01(t)));

        // ------------------------------------------------------------------ shared body helpers

        /// <summary>Torso: pelvis offset / rotation, then the spine bends and twists, the head compensates.</summary>
        private static void Torso(PoseRig r, MotionStyle s, Vector3 hips, float lean, float twist, float roll, float headPitch = 0f, float headYaw = 0f,
            float sideLean = 0f, float hipTwist = 0f, float hipRoll = 0f, float hipPitch = 0f)
        {
            r.HipsOffset(hips);
            r.Rot("Hips", hipPitch, hipTwist, hipRoll);
            r.Rot("Spine", lean * 0.35f, twist * 0.3f, sideLean * 0.4f - hipRoll * 0.5f);
            r.Rot("Chest", lean * 0.35f, twist * 0.35f, sideLean * 0.3f);
            r.Rot("UpperChest", lean * 0.3f, twist * 0.35f, sideLean * 0.3f + roll);
            // Relaxed shoulders.
            r["LeftShoulder"].localPosition += Vector3.down * s.ShoulderDrop;
            r["RightShoulder"].localPosition += Vector3.down * s.ShoulderDrop;
            float cancelPitch = -(lean + hipPitch) * s.HeadStability;
            float cancelYaw = -(twist + hipTwist) * s.HeadStability;
            r.Rot("Neck", s.HeadForward * 0.5f + cancelPitch * 0.4f, cancelYaw * 0.4f, -(sideLean + roll) * 0.4f);
            r.Rot("Head", s.HeadForward * 0.5f + cancelPitch * 0.6f + headPitch, cancelYaw * 0.6f + headYaw, -(sideLean + roll) * 0.5f);
        }

        /// <summary>A relaxed hanging arm: the hand hangs beside the thigh, swinging forward / back by <paramref name="swing"/> metres.</summary>
        private static void HangingArm(PoseRig r, string side, MotionStyle s, float swing, float bend, float curl)
        {
            float x = side == "Left" ? -1f : 1f;
            var shoulder = r.Shoulder(side);
            var wrist = shoulder + new Vector3(x * s.ArmOut, -0.5f + bend * 0.6f, s.WalkElbow + swing + bend);
            var hand = Quaternion.Euler(-swing * 60f - bend * 40f, 0f, x < 0f ? 90f : -90f);
            r.Arm(side, wrist, new Vector3(x * 0.35f, 0f, -1f), hand);
            r.Fingers(side, curl, curl * 0.6f);
        }

        /// <summary>A running arm: elbow bent ~90°, the hand pumping between hip and chest.</summary>
        private static void RunArm(PoseRig r, string side, MotionStyle s, float drive, float height, float across)
        {
            float x = side == "Left" ? -1f : 1f;
            var shoulder = r.Shoulder(side);
            var wrist = shoulder + new Vector3(x * (0.06f - across), -0.3f + height + Mathf.Max(0f, drive) * 0.12f, 0.08f + drive);
            var hand = Quaternion.Euler(-30f - drive * 60f, x * 10f, x < 0f ? 80f : -80f);
            r.Arm(side, wrist, new Vector3(x * 0.25f, -0.2f, -1f), hand);
            r.Fingers(side, 60f, 40f);
        }

        // ------------------------------------------------------------------ idle

        private static void Idle(PoseRig r, float t, MotionStyle s)
        {
            float breath = Mathf.Sin(t * Mathf.PI * 2f / 4f);
            float shift = Mathf.Sin(t * Mathf.PI * 2f / 8f);
            float look = Mathf.Sin(t * Mathf.PI * 2f / 8f + 1.3f) * s.IdleLookDegrees;
            Torso(r, s, new Vector3(shift * s.WeightShift, -0.018f + breath * 0.003f, 0f), breath * -s.BreathDegrees * 0.5f, look * 0.15f, 0f,
                headPitch: breath * 0.6f, headYaw: look, hipRoll: -shift * 2.2f, sideLean: shift * 1.2f);
            r.Rot("UpperChest", -breath * s.BreathDegrees, look * 0.05f, 0f);
            r.Leg("Left", s.IdleFootL + Vector3.up * Ankle, yaw: -8f);
            r.Leg("Right", s.IdleFootR + Vector3.up * Ankle, yaw: 10f);
            HangingArm(r, "Left", s, 0.01f * breath, 0.02f, s.FingerCurl + 6f * Mathf.Sin(t * 0.9f));
            HangingArm(r, "Right", s, -0.01f * breath, 0.03f, s.FingerCurl + 4f);
        }

        /// <summary>KAI's idle variation: a slow look back over the right shoulder, then a flex of the right hand.</summary>
        private static void IdleVariant(PoseRig r, float t, MotionStyle s)
        {
            float look = Mathf.Sin(Mathf.Clamp01(t / 2.2f) * Mathf.PI);
            float flex = Mathf.Sin(Mathf.Clamp01((t - 2.2f) / 1.6f) * Mathf.PI);
            Torso(r, s, new Vector3(0f, -0.018f, 0f), 0f, 18f * look, 0f, headYaw: 45f * look, headPitch: -4f * look);
            r.Leg("Left", s.IdleFootL + Vector3.up * Ankle, yaw: -8f);
            r.Leg("Right", s.IdleFootR + Vector3.up * Ankle, yaw: 10f);
            HangingArm(r, "Left", s, 0f, 0.02f, s.FingerCurl);
            // Right hand rises a little and closes into a fist, then relaxes.
            var shoulder = r.Shoulder("Right");
            var wrist = shoulder + new Vector3(s.ArmOut + 0.03f * flex, -0.5f + 0.14f * flex, 0.06f + 0.12f * flex);
            r.Arm("Right", wrist, new Vector3(0.4f, 0f, -1f), Quaternion.Euler(-40f * flex, 0f, -90f));
            r.Fingers("Right", Mathf.Lerp(s.FingerCurl, 85f, flex), Mathf.Lerp(s.FingerCurl, 60f, flex));
        }

        // ------------------------------------------------------------------ locomotion

        /// <summary>
        /// One gait cycle (phase 0..1) moving in <paramref name="dir"/> (root space). gait: 0 walk, 1 run, 2 sprint,
        /// 3 crouch walk. In place: each foot slides back at the ground speed while planted, so nothing slides in game.
        /// </summary>
        private static void Gait(PoseRig r, float phase, Vector3 dir, int gait, MotionStyle s, bool crouch)
        {
            float speed = gait == 0 ? CharacterAnimatorParams.WalkSpeed : gait == 1 ? CharacterAnimatorParams.RunSpeed : gait == 2 ? CharacterAnimatorParams.SprintSpeed
                : CharacterAnimatorParams.CrouchSpeed;
            float cycle = gait == 0 ? s.WalkCycle : gait == 1 ? s.RunCycle : gait == 2 ? s.SprintCycle : 0.95f;
            float stance = gait == 0 || gait == 3 ? 0.62f : gait == 1 ? 0.4f : 0.34f;
            float lift = gait == 0 ? s.WalkLift : gait == 1 ? s.RunLift : gait == 2 ? s.SprintLift : 0.07f;
            float bob = gait == 0 ? s.WalkBob : gait == 1 ? s.RunBob : gait == 2 ? s.SprintBob : 0.015f;
            float lean = gait == 0 ? s.WalkLean : gait == 1 ? s.RunLean : gait == 2 ? s.SprintLean : 16f;
            bool backward = dir.z < -0.5f, sideways = Mathf.Abs(dir.x) > 0.5f;
            if (backward)
                lean = -lean * 0.3f;
            if (sideways)
                lean *= 0.4f;
            float travel = speed * cycle * stance; // how far a planted foot slides back
            float crouchDrop = crouch ? 0.3f : 0f;

            // Feet.
            float lowest = 0f;
            for (int leg = 0; leg < 2; leg++)
            {
                string side = leg == 0 ? "Left" : "Right";
                float x = leg == 0 ? -1f : 1f;
                float p = Mathf.Repeat(phase + leg * 0.5f, 1f);
                float along, up = 0f, pitch, toe = 0f;
                if (p < stance)
                {
                    float u = p / stance;
                    along = Mathf.Lerp(travel * 0.5f, -travel * 0.5f, u);
                    pitch = u > 0.75f ? Mathf.Lerp(0f, 22f, (u - 0.75f) / 0.25f) : 0f; // heel lifts before toe-off
                    toe = pitch * 0.8f;
                    up = Mathf.Max(0f, pitch) * 0.002f;
                }
                else
                {
                    float u = (p - stance) / (1f - stance);
                    along = Mathf.Lerp(-travel * 0.5f, travel * 0.5f, Smooth(u));
                    up = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.8f) * lift;
                    pitch = Mathf.Lerp(22f, gait == 0 ? -12f : -6f, u);
                    if (gait >= 1 && gait <= 2 && u < 0.5f)
                        up += Mathf.Sin(Mathf.PI * u * 2f) * lift * 0.5f; // heel kicks up behind when running
                }
                var foot = new Vector3(x * (s.StanceHalf + (sideways ? 0.02f : 0f) + (crouch ? 0.04f : 0f)), Ankle + up, 0f) + dir.normalized * along;
                if (gait == 0 && !sideways && !backward)
                    foot.x += x * 0.01f * Mathf.Sin(p * Mathf.PI * 2f); // slight inward placement
                r.Leg(side, foot, backward ? -pitch * 0.5f : pitch, toe, dir.x * 10f * x);
                lowest = Mathf.Min(lowest, foot.y);
            }

            // Pelvis bob (lowest at weight acceptance), sway toward the stance leg, twist with the stride.
            float bobPhase = gait == 0 || gait == 3 ? phase * 2f : phase * 2f - stance;
            float height = -crouchDrop - 0.012f - bob * 0.5f * (1f + Mathf.Cos(bobPhase * Mathf.PI * 2f));
            if (gait >= 1 && gait <= 2)
                height -= 0.03f + (gait == 2 ? 0.02f : 0f);
            float sway = Mathf.Sin(phase * Mathf.PI * 2f);
            float twist = (sideways || backward ? 0.4f : 1f) * s.HipTwist * Mathf.Sin(phase * Mathf.PI * 2f) * (gait == 2 ? 1.4f : 1f);
            Torso(r, s, new Vector3(sway * s.HipSway * (gait == 0 ? 1f : 0.5f), height, 0f), lean, -twist * s.TorsoCounter, 0f,
                hipTwist: twist, hipRoll: -sway * s.HipRoll, sideLean: -dir.x * (gait == 0 ? 3f : 6f));

            // Arms: opposite to the legs; pumping when running.
            float swing = Mathf.Sin(phase * Mathf.PI * 2f);
            float amount = gait == 0 ? s.WalkArmSwing : gait == 1 ? s.RunArmSwing : gait == 2 ? s.SprintArmSwing : 0.05f;
            if (sideways || backward)
                amount *= 0.45f;
            if (gait == 0 || gait == 3)
            {
                HangingArm(r, "Left", s, swing * amount, crouch ? 0.15f : 0.03f, s.FingerCurl + 8f);
                HangingArm(r, "Right", s, -swing * amount, crouch ? 0.15f : 0.03f, s.FingerCurl + 8f);
            }
            else
            {
                RunArm(r, "Left", s, swing * amount, gait == 2 ? 0.05f : 0f, gait == 2 ? 0.02f : 0f);
                RunArm(r, "Right", s, -swing * amount, gait == 2 ? 0.05f : 0f, gait == 2 ? 0.02f : 0f);
            }
        }

        private static void Crouch(PoseRig r, float t, MotionStyle s)
        {
            float breath = Mathf.Sin(t * Mathf.PI * 2f / 4f);
            Torso(r, s, new Vector3(0f, -0.32f + breath * 0.004f, -0.04f), 18f, 0f, 0f, hipPitch: 6f);
            r.Leg("Left", new Vector3(-0.15f, Ankle, 0.12f), toe: 0f);
            r.Leg("Right", new Vector3(0.15f, Ankle + 0.02f, -0.16f), pitch: 25f, toe: 25f);
            HangingArm(r, "Left", s, 0.08f, 0.2f, s.FingerCurl + 10f);
            HangingArm(r, "Right", s, 0.04f, 0.2f, s.FingerCurl + 10f);
        }

        private static void Air(PoseRig r, float t, MotionStyle s)
        {
            float wobble = Mathf.Sin(t * Mathf.PI * 2f) * 0.01f;
            Torso(r, s, new Vector3(0f, 0.02f + wobble, 0f), 6f, 0f, 0f);
            r.Leg("Left", new Vector3(-0.12f, Ankle + 0.32f, 0.16f), pitch: 25f, toe: 10f);
            r.Leg("Right", new Vector3(0.12f, Ankle + 0.22f, -0.12f), pitch: 35f, toe: 10f);
            HangingArm(r, "Left", s, 0.12f, 0.12f, 40f);
            HangingArm(r, "Right", s, -0.05f, 0.18f, 40f);
        }

        private static void Slide(PoseRig r, float t, MotionStyle s)
        {
            Torso(r, s, new Vector3(0f, -0.5f, -0.08f), -24f, 8f, 0f, hipPitch: -14f, headPitch: 10f);
            r.Leg("Left", new Vector3(-0.12f, Ankle + 0.06f, 0.62f), pitch: -30f);
            r.Leg("Right", new Vector3(0.16f, Ankle + 0.02f, -0.02f), pitch: 60f, toe: 20f, pole: Vector3.right + Vector3.forward);
            var shoulder = r.Shoulder("Left");
            r.Arm("Left", shoulder + new Vector3(-0.25f, -0.25f, 0.2f), new Vector3(-1f, 0f, -0.5f), Quaternion.Euler(0f, 0f, 70f));
            HangingArm(r, "Right", s, 0.1f, 0.25f, 50f);
        }

        private static void Seated(PoseRig r, float t, MotionStyle s)
        {
            float breath = Mathf.Sin(t * Mathf.PI) * 0.6f;
            Torso(r, s, new Vector3(0f, 0f, -0.04f), -4f + breath, 0f, 0f, hipPitch: -4f);
            r.Leg("Left", new Vector3(-0.15f, 0.55f, 0.45f), pitch: 10f);
            r.Leg("Right", new Vector3(0.15f, 0.55f, 0.45f), pitch: 10f);
            HangingArm(r, "Left", s, 0.25f, 0.2f, 40f);
            HangingArm(r, "Right", s, 0.25f, 0.2f, 40f);
        }

        // ------------------------------------------------------------------ reactions

        private static void Hit(PoseRig r, float t, MotionStyle s)
        {
            float k = t < 0.08f ? t / 0.08f : 1f - Smooth((t - 0.08f) / 0.37f);
            Torso(r, s, new Vector3(0f, -0.02f - 0.03f * k, -0.03f * k), -12f * k, 8f * k, 0f, headPitch: -14f * k, headYaw: -10f * k);
            r.Leg("Left", s.IdleFootL + Vector3.up * Ankle);
            r.Leg("Right", s.IdleFootR + Vector3.up * Ankle);
            HangingArm(r, "Left", s, -0.06f * k, 0.05f + 0.1f * k, s.FingerCurl + 30f * k);
            HangingArm(r, "Right", s, -0.06f * k, 0.05f + 0.1f * k, s.FingerCurl + 30f * k);
        }

        /// <summary>Hit, knees buckle, a twist, and a fall onto the back.</summary>
        private static void Death(PoseRig r, float t, MotionStyle s)
        {
            float recoil = Mathf.Clamp01(t / 0.15f) * (1f - Mathf.Clamp01((t - 0.15f) / 0.3f));
            float buckle = Smooth(Mathf.Clamp01((t - 0.1f) / 0.5f));
            float fall = Smooth(Mathf.Clamp01((t - 0.45f) / 0.75f));
            float hipsY = Mathf.Lerp(0f, -0.42f, buckle) + Mathf.Lerp(0f, -0.43f, fall);
            float back = Mathf.Lerp(0f, -0.35f, fall);
            r.HipsOffset(new Vector3(0.05f * buckle, hipsY, back));
            r.Rot("Hips", Mathf.Lerp(0f, -84f, fall) - 10f * recoil, 20f * buckle - 10f * fall, -8f * buckle);
            r.Rot("Spine", -8f * recoil + 6f * buckle * (1f - fall), 6f * buckle, 0f);
            r.Rot("Chest", -6f * recoil + 10f * buckle * (1f - fall) - 4f * fall, 4f, 0f);
            r.Rot("UpperChest", -4f * recoil, 0f, 0f);
            r.Rot("Neck", 10f * fall, -15f * fall, 0f);
            r.Rot("Head", -20f * recoil + 20f * buckle - 25f * fall, -30f * fall, 10f * fall);
            // Feet stay planted while the knees go, then slide out as the body lands.
            r.Leg("Left", Ease(s.IdleFootL + Vector3.up * Ankle, new Vector3(-0.22f, Ankle * 0.6f, 0.38f), fall), pitch: -40f * fall);
            r.Leg("Right", Ease(s.IdleFootR + Vector3.up * Ankle, new Vector3(0.18f, Ankle * 0.6f, 0.28f), fall), pitch: -50f * fall,
                pole: Vector3.Lerp(Vector3.forward, Vector3.up + Vector3.right * 0.3f, fall));
            foreach (var side in new[] { "Left", "Right" })
            {
                float x = side == "Left" ? -1f : 1f;
                var shoulder = r.Shoulder(side);
                var hang = shoulder + new Vector3(x * 0.08f, -0.5f, 0.05f);
                var spread = shoulder + new Vector3(x * 0.55f, -0.1f, -0.1f);
                r.Arm(side, Vector3.Lerp(hang, spread, fall), new Vector3(x * 0.3f, -0.5f, -1f), Quaternion.Euler(0f, 0f, x < 0f ? 90f - 60f * fall : -90f + 60f * fall));
                r.Fingers(side, Mathf.Lerp(30f, 15f, fall), 20f);
            }
        }

        /// <summary>KAI's victory: lowers the weapon, turns slightly away, stands tall and calm (the aura is an effect).</summary>
        private static void Victory(PoseRig r, float t, MotionStyle s)
        {
            float turn = Smooth(Mathf.Clamp01(t / 1.2f));
            float settle = Smooth(Mathf.Clamp01((t - 1.0f) / 1.2f));
            float breath = Mathf.Sin(t * Mathf.PI * 2f / 3f) * settle;
            Torso(r, s, new Vector3(0f, -0.01f, 0f), -2f * settle + breath * 0.5f, 14f * turn, 0f, headYaw: -22f * turn, headPitch: -6f * settle, hipTwist: 14f * turn);
            r.Leg("Left", Vector3.Lerp(s.IdleFootL, new Vector3(-0.13f, 0f, 0.02f), turn) + Vector3.up * Ankle, yaw: -14f * turn);
            r.Leg("Right", Vector3.Lerp(s.IdleFootR, new Vector3(0.1f, 0f, -0.1f), turn) + Vector3.up * Ankle, yaw: 20f * turn);
            HangingArm(r, "Left", s, 0f, 0.02f, s.FingerCurl);
            HangingArm(r, "Right", s, -0.03f * turn, 0.02f, Mathf.Lerp(s.FingerCurl, 70f, turn));
        }

        // ------------------------------------------------------------------ melee

        private struct BladeKey
        {
            public float Time;
            public Vector3 Grip;      // where the blade is held (root space)
            public Vector3 Blade;     // blade direction
            public Vector3 Edge;      // cutting-edge direction
            public float Twist, Lean; // torso
            public bool TwoHanded;
        }

        private static BladeKey Key(float time, Vector3 grip, Vector3 blade, Vector3 edge, float twist, float lean, bool twoHanded = true) =>
            new BladeKey { Time = time, Grip = grip, Blade = blade.normalized, Edge = edge.normalized, Twist = twist, Lean = lean, TwoHanded = twoHanded };

        /// <summary>The combo attacks: guard → wind-up → strike (at 40 % of the clip) → follow-through → guard.</summary>
        private static BladeKey[] Path(Core.Combat.MeleePattern pattern)
        {
            var guard = Key(0f, new Vector3(0.12f, 1.08f, 0.3f), new Vector3(0.05f, 0.62f, 0.78f), Vector3.down, 0f, 4f);
            var guardEnd = guard;
            guardEnd.Time = CharacterAnimatorParams.MeleeClipSeconds;
            float strike = CharacterAnimatorParams.MeleeClipSeconds * CharacterAnimatorParams.MeleeStrikeFraction;
            switch (pattern)
            {
                case Core.Combat.MeleePattern.BackhandSlash:
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(-0.32f, 1.3f, 0.1f), new Vector3(-0.6f, 0.5f, -0.6f), new Vector3(1f, 0f, 0.4f), -35f, 2f),
                        Key(strike, new Vector3(0f, 1.25f, 0.45f), new Vector3(0.1f, 0.15f, 1f), new Vector3(1f, 0f, 0f), 0f, 10f),
                        Key(strike + 0.14f, new Vector3(0.38f, 1.18f, 0.2f), new Vector3(0.9f, 0.05f, -0.3f), new Vector3(0.2f, 0f, -1f), 32f, 8f),
                        guardEnd };
                case Core.Combat.MeleePattern.OverheadStrike:
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(0.08f, 1.62f, -0.05f), new Vector3(0f, 0.4f, -0.9f), Vector3.up, 6f, -10f),
                        Key(strike, new Vector3(0.04f, 1.38f, 0.5f), new Vector3(0f, 0.6f, 0.8f), Vector3.forward, 0f, 14f),
                        Key(strike + 0.14f, new Vector3(0.05f, 0.92f, 0.45f), new Vector3(0f, -0.6f, 0.8f), Vector3.down, 0f, 24f),
                        guardEnd };
                case Core.Combat.MeleePattern.Thrust:
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(0.22f, 1.18f, -0.1f), new Vector3(-0.04f, 0.04f, 1f), Vector3.down, 20f, 0f, false),
                        Key(strike, new Vector3(0.08f, 1.26f, 0.68f), new Vector3(-0.04f, 0.04f, 1f), Vector3.down, -8f, 14f, false),
                        Key(strike + 0.14f, new Vector3(0.06f, 1.27f, 0.74f), new Vector3(-0.04f, 0.04f, 1f), Vector3.down, -10f, 16f, false),
                        guardEnd };
                case Core.Combat.MeleePattern.RisingSlash:
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(0.34f, 0.85f, 0.15f), new Vector3(0.7f, -0.5f, 0.4f), new Vector3(-0.3f, 1f, 0f), 30f, 10f),
                        Key(strike, new Vector3(0.1f, 1.2f, 0.48f), new Vector3(0.05f, 0.4f, 1f), new Vector3(-0.6f, 0.8f, 0f), 5f, 6f),
                        Key(strike + 0.14f, new Vector3(-0.2f, 1.6f, 0.25f), new Vector3(-0.5f, 0.85f, 0.1f), new Vector3(-0.8f, 0.6f, 0f), -25f, -4f),
                        guardEnd };
                case Core.Combat.MeleePattern.SpinSlash:
                    return new[] { guard,
                        Key(strike * 0.6f, new Vector3(-0.3f, 1.15f, 0.05f), new Vector3(-0.9f, 0.1f, -0.2f), new Vector3(0.2f, 0f, 1f), -60f, 4f, false),
                        Key(strike, new Vector3(0.1f, 1.15f, 0.5f), new Vector3(0.2f, 0.05f, 1f), new Vector3(1f, 0f, -0.2f), 20f, 6f, false),
                        Key(strike + 0.18f, new Vector3(0.45f, 1.15f, -0.05f), new Vector3(0.9f, 0.05f, -0.4f), new Vector3(-0.3f, 0f, -1f), 75f, 4f, false),
                        guardEnd };
                case Core.Combat.MeleePattern.QuickStab:
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(0.26f, 1.12f, 0.02f), new Vector3(-0.05f, 0.05f, 1f), Vector3.down, 12f, 0f, false),
                        Key(strike, new Vector3(0.12f, 1.24f, 0.6f), new Vector3(-0.05f, 0.05f, 1f), Vector3.down, -6f, 10f, false),
                        guardEnd };
                default: // HorizontalSlash: a wide right-to-left cut
                    return new[] { guard,
                        Key(strike * 0.8f, new Vector3(0.38f, 1.3f, 0.05f), new Vector3(0.75f, 0.45f, -0.5f), new Vector3(-0.5f, 0f, 1f), 40f, 2f),
                        Key(strike, new Vector3(0.05f, 1.24f, 0.48f), new Vector3(0.1f, 0.2f, 1f), new Vector3(-1f, 0f, 0f), 0f, 10f),
                        Key(strike + 0.14f, new Vector3(-0.36f, 1.16f, 0.2f), new Vector3(-0.9f, 0f, -0.25f), new Vector3(-0.2f, 0f, -1f), -38f, 8f),
                        guardEnd };
            }
        }

        private static void Melee(PoseRig r, float t, Core.Combat.MeleePattern pattern, MotionStyle s)
        {
            var keys = Path(pattern);
            int i = 0;
            while (i < keys.Length - 2 && t > keys[i + 1].Time)
                i++;
            var a = keys[i];
            var b = keys[i + 1];
            float u = Mathf.InverseLerp(a.Time, b.Time, t);
            // The strike segment is fast and linear-ish; the rest eases.
            float k = i == 1 ? Mathf.Pow(u, 0.8f) : Smooth(u);
            var grip = Vector3.Lerp(a.Grip, b.Grip, k);
            var blade = Vector3.Slerp(a.Blade, b.Blade, k).normalized;
            var edge = Vector3.Slerp(a.Edge, b.Edge, k);
            edge = Vector3.ProjectOnPlane(edge, blade).normalized;
            if (edge.sqrMagnitude < 0.5f)
                edge = Vector3.ProjectOnPlane(Vector3.down, blade).normalized;
            float twist = Mathf.Lerp(a.Twist, b.Twist, k), lean = Mathf.Lerp(a.Lean, b.Lean, k);
            bool twoHanded = k < 0.5f ? a.TwoHanded : b.TwoHanded;

            // Lunge: the front foot steps in on the strike, the body drops a little.
            float strike = Mathf.Sin(Mathf.Clamp01(t / CharacterAnimatorParams.MeleeClipSeconds) * Mathf.PI);
            Torso(r, s, new Vector3(0f, -0.04f - 0.06f * strike, 0.04f * strike), lean, twist, 0f, headYaw: -twist * 0.6f, hipTwist: twist * 0.4f);
            r.Leg("Left", new Vector3(-0.13f, Ankle, 0.12f + 0.22f * strike), pitch: -6f * strike);
            r.Leg("Right", new Vector3(0.13f, Ankle + 0.02f * strike, -0.16f - 0.06f * strike), pitch: 18f * strike, toe: 18f * strike);

            var handRot = Quaternion.LookRotation(edge, blade) * Quaternion.Inverse(CharacterAnimationController.MeleeGripRotation);
            var wrist = grip - handRot * CharacterAnimationController.MeleeGripPosition;
            r.Arm("Right", wrist, new Vector3(0.6f, -0.6f, -0.5f), handRot);
            r.Fingers("Right", 85f, 60f);
            if (twoHanded)
            {
                var left = grip - blade * 0.11f + new Vector3(-0.015f, 0f, 0f);
                r.Arm("Left", left, new Vector3(-0.7f, -0.6f, -0.4f), handRot * Quaternion.Euler(0f, 180f, 0f));
                r.Fingers("Left", 85f, 60f);
            }
            else
            {
                var shoulder = r.Shoulder("Left");
                r.Arm("Left", shoulder + new Vector3(-0.18f, -0.38f, -0.05f - 0.1f * strike), new Vector3(-0.5f, 0f, -1f), Quaternion.Euler(0f, 0f, 75f));
                r.Fingers("Left", 30f, 20f);
            }
        }
    }
}
