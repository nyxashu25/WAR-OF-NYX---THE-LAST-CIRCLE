using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>Blinking (and a little life in the face): the eyes close for ~0.12 s every 2–6 s. Visual only.</summary>
    public sealed class FaceAnimator : MonoBehaviour
    {
        private Transform _eyes;
        private float _nextBlink;
        private float _blinkStart = -1f;

        public void Bind(Transform eyes)
        {
            _eyes = eyes;
            _nextBlink = Time.time + Random.Range(1f, 4f);
        }

        private void LateUpdate()
        {
            if (_eyes == null)
                return;
            if (_blinkStart < 0f && Time.time >= _nextBlink)
            {
                _blinkStart = Time.time;
                // Occasionally a double blink.
                _nextBlink = Time.time + (Random.value < 0.15f ? 0.25f : Random.Range(2f, 6f));
            }
            float open = 1f;
            if (_blinkStart >= 0f)
            {
                float t = (Time.time - _blinkStart) / 0.13f;
                open = t < 0.4f ? Mathf.Lerp(1f, 0.08f, t / 0.4f) : Mathf.Lerp(0.08f, 1f, (t - 0.4f) / 0.6f);
                if (t >= 1f)
                {
                    open = 1f;
                    _blinkStart = -1f;
                }
            }
            _eyes.localScale = new Vector3(1f, open, 1f);
        }
    }
}
