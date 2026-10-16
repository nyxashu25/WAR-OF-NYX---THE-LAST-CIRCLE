using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using P = WarOfNyx.Gameplay.Characters.CharacterAnimatorParams;

namespace WarOfNyx.EditorTools.Characters
{
    /// <summary>
    /// Builds the shared character Animator Controller (section 40.8):
    ///  * Base layer (IK pass): 2D freeform directional locomotion (idle, walk and run in four directions), sprint,
    ///    crouch (idle / walk), air, slide, seated, death and victory, with tuned transitions; an idle variation.
    ///  * Upper-body layer (avatar mask: spine, arms, head): the seven melee combo attacks (re-timed to the server's windup
    ///    by MeleeRate) and the hit reaction; empty otherwise, so locomotion and weapon IK show through.
    /// Characters plug in their own clips through an AnimatorOverrideController on the same slots.
    /// </summary>
    public static class CharacterControllerBuilder
    {
        public static AnimatorController Build(Dictionary<string, AnimationClip> clips, string path, string maskPath)
        {
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(P.MoveX, AnimatorControllerParameterType.Float);
            controller.AddParameter(P.MoveY, AnimatorControllerParameterType.Float);
            controller.AddParameter(P.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = P.LocomotionRate, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = P.MeleeRate, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(P.Sprint, AnimatorControllerParameterType.Bool);
            controller.AddParameter(P.Crouch, AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = P.Grounded, type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter(P.Slide, AnimatorControllerParameterType.Bool);
            controller.AddParameter(P.Seated, AnimatorControllerParameterType.Bool);
            controller.AddParameter(P.Dead, AnimatorControllerParameterType.Bool);
            controller.AddParameter(P.Victory, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(P.Hit, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(P.Melee, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(P.MeleePattern, AnimatorControllerParameterType.Int);
            controller.AddParameter(P.IdleVariant, AnimatorControllerParameterType.Trigger);

            // ---- Base layer.
            var layers = controller.layers;
            layers[0].name = "Base Locomotion";
            layers[0].iKPass = true;
            controller.layers = layers;
            var sm = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = P.MoveX;
            tree.blendParameterY = P.MoveY;
            tree.AddChild(clips["Idle"], Vector2.zero);
            float walk = P.WalkSpeed / P.RunSpeed;
            tree.AddChild(clips["WalkF"], new Vector2(0f, walk));
            tree.AddChild(clips["WalkB"], new Vector2(0f, -walk));
            tree.AddChild(clips["WalkL"], new Vector2(-walk, 0f));
            tree.AddChild(clips["WalkR"], new Vector2(walk, 0f));
            tree.AddChild(clips["RunF"], new Vector2(0f, 1f));
            tree.AddChild(clips["RunB"], new Vector2(0f, -1f));
            tree.AddChild(clips["RunL"], new Vector2(-1f, 0f));
            tree.AddChild(clips["RunR"], new Vector2(1f, 0f));
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = P.LocomotionRate;
            sm.defaultState = locomotion;

            var idleVariant = State(sm, "IdleVariant", clips["IdleVariant"]);
            var sprint = State(sm, "Sprint", clips["Sprint"]);
            sprint.speedParameterActive = true;
            sprint.speedParameter = P.LocomotionRate;
            var crouch = controller.CreateBlendTreeInController("Crouch", out var crouchTree, 0);
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = P.Speed;
            crouchTree.AddChild(clips["CrouchIdle"], 0f);
            crouchTree.AddChild(clips["CrouchWalk"], P.CrouchSpeed);
            var air = State(sm, "Air", clips["Air"]);
            var slide = State(sm, "Slide", clips["Slide"]);
            var seated = State(sm, "Seated", clips["Seated"]);
            var death = State(sm, "Death", clips["Death"]);
            var victory = State(sm, "Victory", clips["Victory"]);

            Go(locomotion, idleVariant, 0.3f, (AnimatorConditionMode.If, P.IdleVariant, 0f), (AnimatorConditionMode.Less, P.Speed, 0.2f));
            Go(idleVariant, locomotion, 0.35f, (AnimatorConditionMode.Greater, P.Speed, 0.25f));
            Exit(idleVariant, locomotion, 0.95f, 0.4f);
            Go(locomotion, sprint, 0.2f, (AnimatorConditionMode.If, P.Sprint, 0f));
            Go(sprint, locomotion, 0.25f, (AnimatorConditionMode.IfNot, P.Sprint, 0f));
            Go(locomotion, crouch, 0.2f, (AnimatorConditionMode.If, P.Crouch, 0f));
            Go(crouch, locomotion, 0.2f, (AnimatorConditionMode.IfNot, P.Crouch, 0f));
            Go(sprint, crouch, 0.15f, (AnimatorConditionMode.If, P.Crouch, 0f));
            foreach (var from in new[] { locomotion, idleVariant, sprint, crouch })
            {
                Go(from, air, 0.15f, (AnimatorConditionMode.IfNot, P.Grounded, 0f));
                Go(from, slide, 0.12f, (AnimatorConditionMode.If, P.Slide, 0f));
            }
            Go(air, locomotion, 0.12f, (AnimatorConditionMode.If, P.Grounded, 0f));
            Go(slide, locomotion, 0.2f, (AnimatorConditionMode.IfNot, P.Slide, 0f));
            AnyGo(sm, seated, 0.25f, (AnimatorConditionMode.If, P.Seated, 0f), (AnimatorConditionMode.IfNot, P.Dead, 0f));
            Go(seated, locomotion, 0.25f, (AnimatorConditionMode.IfNot, P.Seated, 0f));
            AnyGo(sm, death, 0.15f, (AnimatorConditionMode.If, P.Dead, 0f));
            AnyGo(sm, victory, 0.4f, (AnimatorConditionMode.If, P.Victory, 0f), (AnimatorConditionMode.IfNot, P.Dead, 0f));

            // ---- Upper-body layer: melee attacks and hit reactions.
            var mask = BuildUpperBodyMask(maskPath);
            var upper = new AnimatorControllerLayer
            {
                name = "Upper Body Actions", defaultWeight = 1f, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "Upper Body Actions", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(upper.stateMachine, controller);
            controller.AddLayer(upper);
            var usm = controller.layers[1].stateMachine;
            var empty = usm.AddState("Empty");
            usm.defaultState = empty;
            foreach (var pattern in (Core.Combat.MeleePattern[])System.Enum.GetValues(typeof(Core.Combat.MeleePattern)))
            {
                var attack = State(usm, "Melee_" + pattern, clips["Melee_" + pattern]);
                attack.speedParameterActive = true;
                attack.speedParameter = P.MeleeRate;
                AnyGo(usm, attack, 0.05f, (AnimatorConditionMode.If, P.Melee, 0f), (AnimatorConditionMode.Equals, P.MeleePattern, (float)(int)pattern),
                    (AnimatorConditionMode.IfNot, P.Dead, 0f));
                Exit(attack, empty, 0.92f, 0.15f);
            }
            var hit = State(usm, "Hit", clips["Hit"]);
            AnyGo(usm, hit, 0.05f, (AnimatorConditionMode.If, P.Hit, 0f), (AnimatorConditionMode.IfNot, P.Dead, 0f));
            Exit(hit, empty, 0.85f, 0.15f);
            var dead = usm.AddState("Dead");
            AnyGo(usm, dead, 0.1f, (AnimatorConditionMode.If, P.Dead, 0f));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion)
        {
            var state = sm.AddState(name);
            state.motion = motion;
            return state;
        }

        private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float duration, params (AnimatorConditionMode mode, string param, float value)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.interruptionSource = TransitionInterruptionSource.Destination;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.value, c.param);
            return t;
        }

        private static void Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }

        private static void AnyGo(AnimatorStateMachine sm, AnimatorState to, float duration, params (AnimatorConditionMode mode, string param, float value)[] conditions)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.canTransitionToSelf = false;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.value, c.param);
        }

        private static AvatarMask BuildUpperBodyMask(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var mask = new AvatarMask { name = "UpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var part in new[]
                     {
                         AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                         AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
                     })
                mask.SetHumanoidBodyPartActive(part, true);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }
    }
}
