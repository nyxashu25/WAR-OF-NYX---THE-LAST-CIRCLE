using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.Characters
{
    /// <summary>Facial expressions (section 40.9).</summary>
    public enum FaceExpression : byte { Neutral, Focused, Angry, Determined, Injured, Exhausted, Confident, Victory, Defeat }

    /// <summary>
    /// Brings the painted face to life: blinking (random intervals, occasional double blinks), expressions chosen from
    /// what the character is doing (focused while aiming, angry when hurt or attacking, injured / exhausted at low health,
    /// confident or a victory smile at the end, closed eyes on defeat), and an eye glow at special moments. Each change is
    /// a tile switch on the face material (a MaterialPropertyBlock on the face submesh), so it costs nothing on the GPU;
    /// far-away faces update less often and skip blinking. Imported models with a blend-shape face (VRM expressions:
    /// blink, angry, sad, happy, relaxed, aa…) get the same behaviour through blend-shape weights instead of tiles.
    /// </summary>
    [DefaultExecutionOrder(57)]
    public sealed class CharacterFacialAnimator : MonoBehaviour
    {
        private static readonly int EyeTileId = Shader.PropertyToID("_EyeTile");
        private static readonly int BrowTileId = Shader.PropertyToID("_BrowTile");
        private static readonly int MouthTileId = Shader.PropertyToID("_MouthTile");
        private static readonly int EyeGlowId = Shader.PropertyToID("_EyeGlow");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

        private SkinnedMeshRenderer[] _renderers;
        private MaterialPropertyBlock _block;
        private int _submesh = 1;
        private Color _glowColor = new Color(0.62f, 0.25f, 1f);
        private CharacterAnimationController _controller;
        private float _nextBlink;
        private float _blinkUntil;
        private float _angryUntil;
        private float _glow;
        private float _nextUpdate;
        private int _shownEye = -1, _shownBrow = -1, _shownMouth = -1;
        private float _shownGlow = -1f;

        /// <summary>Forced expression (character select, victory screen); null = automatic.</summary>
        public FaceExpression? Override { get; set; }
        public FaceExpression Current { get; private set; }

        public void Init(CharacterVisualProfile profile)
        {
            if (profile != null)
            {
                _submesh = profile.faceSubmesh;
                _glowColor = profile.energyColor;
                _faceShapes = profile.HasBlendShapeFace ? profile.faceShapes : null;
            }
        }

        // ------------------------------------------------------------------ blend-shape faces (imported models)

        private FaceShapeDef[] _faceShapes;
        private Dictionary<string, List<(SkinnedMeshRenderer renderer, int index, float weight)>> _binds;
        private readonly Dictionary<string, float> _presetWeight = new Dictionary<string, float>();
        private readonly Dictionary<(SkinnedMeshRenderer, int), float> _shapeTotals = new Dictionary<(SkinnedMeshRenderer, int), float>();

        /// <summary>The VRM expression presets (0..1) an expression is made of; blinking is added separately.</summary>
        public static void Presets(FaceExpression e, Dictionary<string, float> into)
        {
            into.Clear();
            switch (e)
            {
                case FaceExpression.Focused: into["angry"] = 0.25f; break;
                case FaceExpression.Angry: into["angry"] = 1f; break;
                case FaceExpression.Determined: into["angry"] = 0.5f; break;
                case FaceExpression.Injured: into["sad"] = 0.7f; break;
                case FaceExpression.Exhausted: into["sad"] = 0.8f; into["aa"] = 0.35f; break;
                case FaceExpression.Confident: into["relaxed"] = 0.8f; break;
                case FaceExpression.Victory: into["happy"] = 1f; break;
                case FaceExpression.Defeat: into["blink"] = 1f; into["sad"] = 0.6f; break;
            }
        }

        private readonly Dictionary<string, float> _target = new Dictionary<string, float>();

        private void BuildBinds()
        {
            _binds = new Dictionary<string, List<(SkinnedMeshRenderer, int, float)>>();
            foreach (var def in _faceShapes)
            {
                var list = new List<(SkinnedMeshRenderer, int, float)>();
                foreach (var r in _renderers)
                {
                    if (r == null || r.sharedMesh == null)
                        continue;
                    for (int i = 0; i < def.shapes.Length; i++)
                    {
                        int index = r.sharedMesh.GetBlendShapeIndex(def.shapes[i]);
                        if (index >= 0)
                            list.Add((r, index, i < def.weights.Length ? def.weights[i] : 1f));
                    }
                }
                if (list.Count > 0)
                    _binds[def.preset] = list;
            }
        }

        private void UpdateBlendShapes(float distance)
        {
            if (_binds == null)
                BuildBinds();
            Presets(Current, _target);
            bool eyesBusy = _target.ContainsKey("blink") || (_target.TryGetValue("happy", out float happy) && happy > 0.5f);
            if (distance < 15f && !eyesBusy)
            {
                if (Time.time > _nextBlink)
                {
                    _blinkUntil = Time.time + 0.12f;
                    _nextBlink = Time.time + (Random.value < 0.15f ? 0.25f : Random.Range(2.2f, 5.5f));
                }
                if (Time.time < _blinkUntil)
                    _target["blink"] = 1f;
            }
            _shapeTotals.Clear();
            foreach (var pair in _binds)
            {
                _target.TryGetValue(pair.Key, out float goal);
                _presetWeight.TryGetValue(pair.Key, out float now);
                float speed = pair.Key == "blink" ? 25f : 6f;
                now = Mathf.MoveTowards(now, goal, Time.deltaTime * speed);
                _presetWeight[pair.Key] = now;
                foreach (var (r, index, weight) in pair.Value)
                {
                    var key = (r, index);
                    _shapeTotals[key] = (_shapeTotals.TryGetValue(key, out float t) ? t : 0f) + now * weight;
                }
            }
            foreach (var pair in _shapeTotals)
                pair.Key.Item1.SetBlendShapeWeight(pair.Key.Item2, Mathf.Clamp01(pair.Value) * 100f);
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _block = new MaterialPropertyBlock();
            _controller = GetComponent<CharacterAnimationController>();
            _nextBlink = Time.time + Random.Range(1f, 4f);
        }

        /// <summary>A short burst of anger (taking a hit, a strike).</summary>
        public void Flare(float seconds = 1.2f) => _angryUntil = Time.time + seconds;

        /// <summary>Eyes glow for a moment (spawn, victory, special attacks).</summary>
        public void Glow(float amount = 1.2f) => _glow = Mathf.Max(_glow, amount);

        public static void Tiles(FaceExpression e, out int eye, out int brow, out int mouth)
        {
            switch (e)
            {
                case FaceExpression.Focused: eye = FaceTiles.EyeFocused; brow = FaceTiles.BrowNeutral; mouth = FaceTiles.MouthNeutral; break;
                case FaceExpression.Angry: eye = FaceTiles.EyeAngry; brow = FaceTiles.BrowAngry; mouth = FaceTiles.MouthGrit; break;
                case FaceExpression.Determined: eye = FaceTiles.EyeDetermined; brow = FaceTiles.BrowAngry; mouth = FaceTiles.MouthNeutral; break;
                case FaceExpression.Injured: eye = FaceTiles.EyeWide; brow = FaceTiles.BrowWorried; mouth = FaceTiles.MouthGrit; break;
                case FaceExpression.Exhausted: eye = FaceTiles.EyePained; brow = FaceTiles.BrowWorried; mouth = FaceTiles.MouthOpen; break;
                case FaceExpression.Confident: eye = FaceTiles.EyeFocused; brow = FaceTiles.BrowRaised; mouth = FaceTiles.MouthSmirk; break;
                case FaceExpression.Victory: eye = FaceTiles.EyeDetermined; brow = FaceTiles.BrowRaised; mouth = FaceTiles.MouthSmirk; break;
                case FaceExpression.Defeat: eye = FaceTiles.EyeClosed; brow = FaceTiles.BrowWorried; mouth = FaceTiles.MouthNeutral; break;
                default: eye = FaceTiles.EyeNeutral; brow = FaceTiles.BrowNeutral; mouth = FaceTiles.MouthNeutral; break;
            }
        }

        private FaceExpression Automatic()
        {
            if (_controller == null)
                return FaceExpression.Neutral;
            if (_controller.Dead)
                return FaceExpression.Defeat;
            if (_controller.Victorious)
                return FaceExpression.Victory;
            if (Time.time < _angryUntil)
                return FaceExpression.Angry;
            if (_controller.Health01 < 0.25f)
                return FaceExpression.Exhausted;
            if (_controller.Health01 < 0.5f)
                return FaceExpression.Injured;
            return _controller.Moving ? FaceExpression.Determined : FaceExpression.Focused;
        }

        private void LateUpdate()
        {
            if (_renderers == null || _renderers.Length == 0)
                return;
            var cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
            if (distance > 40f)
                return; // too far to read a face
            if (_faceShapes != null)
            {
                Current = Override ?? Automatic();
                UpdateBlendShapes(distance);
                return;
            }
            if (Time.time < _nextUpdate)
                return;
            _nextUpdate = distance > 15f ? Time.time + 0.4f : 0f;

            Current = Override ?? Automatic();
            Tiles(Current, out int eye, out int brow, out int mouth);
            // Blinks (not with closed or happy eyes, not far away).
            if (distance < 15f && eye != FaceTiles.EyeClosed)
            {
                if (Time.time > _nextBlink)
                {
                    _blinkUntil = Time.time + 0.11f;
                    _nextBlink = Time.time + (Random.value < 0.15f ? 0.25f : Random.Range(2.2f, 5.5f));
                }
                if (Time.time < _blinkUntil)
                    eye = FaceTiles.EyeClosed;
            }
            _glow = Mathf.MoveTowards(_glow, Current == FaceExpression.Victory ? 0.6f : 0f, Time.deltaTime * 0.8f);
            if (eye == _shownEye && brow == _shownBrow && mouth == _shownMouth && Mathf.Abs(_glow - _shownGlow) < 0.01f)
                return;
            _shownEye = eye;
            _shownBrow = brow;
            _shownMouth = mouth;
            _shownGlow = _glow;
            foreach (var r in _renderers)
            {
                if (r == null || _submesh < 0 || r.sharedMaterials.Length <= _submesh)
                    continue;
                r.GetPropertyBlock(_block, _submesh);
                _block.SetFloat(EyeTileId, eye);
                _block.SetFloat(BrowTileId, brow);
                _block.SetFloat(MouthTileId, mouth);
                _block.SetFloat(EyeGlowId, _glow);
                _block.SetColor(GlowColorId, _glowColor);
                r.SetPropertyBlock(_block, _submesh);
            }
        }
    }
}
