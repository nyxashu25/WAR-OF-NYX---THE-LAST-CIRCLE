using UnityEngine;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>
    /// Runtime appearance loader (section 40.8): if a character has a skinned model (a <see cref="CharacterVisualProfile"/>
    /// in Resources/WarOfNyx/Characters/{NAME}), it is instantiated under the character's visual root with its animation
    /// layers (<see cref="CharacterAnimationController"/>, <see cref="CharacterFootIK"/>, <see cref="CharacterFacialAnimator"/>,
    /// <see cref="CharacterSecondaryMotion"/>, <see cref="CharacterVisualEffects"/>). Characters without one keep the older
    /// jointed placeholder. Same path for the human, bots, the menu preview and spectators: an AI-controlled KAI moves
    /// like KAI. Hit shapes never change (they live on the character root).
    /// </summary>
    public static class SkinnedCharacterLoader
    {
        public const string ResourceFolder = "WarOfNyx/Characters/";

        public static CharacterVisualProfile ProfileFor(CharacterLook look) =>
            look != null ? Resources.Load<CharacterVisualProfile>(ResourceFolder + look.Name) : null;

        public static bool HasSkinnedModel(CharacterLook look)
        {
            var profile = ProfileFor(look);
            return profile != null && profile.prefab != null;
        }

        private static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        /// <summary>Instantiates the skinned model under <paramref name="visualRoot"/>; null when the character has none.</summary>
        public static GameObject TryCreate(Transform visualRoot, CharacterLook look, string name)
        {
            var profile = ProfileFor(look);
            if (profile == null || profile.prefab == null)
                return null;
            var model = Object.Instantiate(profile.prefab, visualRoot, false);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Ensure<CharacterAnimationController>(model).Init(profile.animation);
            Ensure<CharacterFootIK>(model).Init(profile.animation);
            Ensure<CharacterFacialAnimator>(model).Init(profile);
            Ensure<CharacterSecondaryMotion>(model);
            Ensure<CharacterVisualEffects>(model).Init(profile);
            return model;
        }
    }
}
