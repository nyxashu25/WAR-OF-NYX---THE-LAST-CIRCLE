using UnityEngine;
using WarOfNyx.Core.Movement;

namespace WarOfNyx.Characters
{
    /// <summary>
    /// Inspector-editable wrapper around the engine-independent <see cref="MovementSettings"/>.
    /// One shared asset: Assets/ScriptableObjects/Gameplay/MovementConfig.asset.
    /// Online, the server's copy is authoritative; clients use the same asset for prediction.
    /// </summary>
    [CreateAssetMenu(menuName = "War of Nyx/Movement Config", fileName = "MovementConfig")]
    public sealed class MovementConfig : ScriptableObject
    {
        public MovementSettings settings = new MovementSettings();

        private void OnValidate()
        {
            if (settings == null)
                settings = new MovementSettings();
            settings.crouchHeight = Mathf.Clamp(settings.crouchHeight, settings.capsuleRadius * 2f + 0.05f, settings.standingHeight);
            settings.slideDuration = Mathf.Max(0.05f, settings.slideDuration);
            settings.gravity = Mathf.Max(0.1f, settings.gravity);
        }
    }
}
