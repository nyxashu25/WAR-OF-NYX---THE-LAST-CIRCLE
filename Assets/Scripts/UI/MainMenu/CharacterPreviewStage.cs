using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WarOfNyx.Characters;
using WarOfNyx.Core.Combat;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.UI.MainMenu
{
    /// <summary>
    /// 3-D preview of a roster character for the CHARACTERS screen: the detailed model holding their signature
    /// weapon, idling (breathing, blinking), on a small lit stage far below the menu, rendered by its own camera
    /// into a texture. Drag the image to turn the character; it slowly turns by itself otherwise.
    /// Uses its own layer and lights, so nothing else in the menu is affected.
    /// </summary>
    public sealed class CharacterPreviewStage : MonoBehaviour, IDragHandler
    {
        private const int Layer = 31;
        private static readonly Vector3 StagePosition = new Vector3(0f, -1000f, 0f);

        private Camera _camera;
        private RenderTexture _texture;
        private Transform _puppet;
        private float _yaw = 200f;
        private float _lastDrag = -10f;
        private int _shownId = -1;

        public static CharacterPreviewStage Create(RectTransform parent)
        {
            var image = new GameObject("CharacterPreview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(parent, false);
            var stage = image.gameObject.AddComponent<CharacterPreviewStage>();
            stage.Build(image);
            return stage;
        }

        private void Build(RawImage image)
        {
            _texture = new RenderTexture(900, 1200, 24) { name = "CharacterPreview", antiAliasing = 4 };
            image.texture = _texture;
            image.raycastTarget = true;

            var stage = new GameObject("Character preview stage").transform;
            stage.position = StagePosition;
            stage.SetParent(transform.root, true);

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(stage, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.cullingMask = 1 << Layer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.04f, 0.1f, 1f);
            _camera.fieldOfView = 26f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.targetTexture = _texture;
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 4.6f);
            camGo.transform.LookAt(StagePosition + new Vector3(0f, 0.98f, 0f));

            Light(stage, "Key", new Vector3(35f, 150f, 0f), new Color(1f, 0.95f, 0.9f), 1.25f);
            Light(stage, "Rim", new Vector3(20f, -20f, 0f), new Color(0.55f, 0.6f, 1f), 1.1f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(floor.GetComponent<Collider>());
            floor.name = "Stage floor";
            floor.transform.SetParent(stage, false);
            floor.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
            floor.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            var renderer = floor.GetComponent<Renderer>();
            renderer.sharedMaterial = StyleKit.Toon;
            StyleKit.Paint(renderer, new Color(0.12f, 0.1f, 0.2f), new Color(0.05f, 0.03f, 0.12f));
            SetLayer(floor.transform);

            // Keep the menu's own camera from ever drawing the stage.
            var main = Camera.main;
            if (main != null)
                main.cullingMask &= ~(1 << Layer);
        }

        private static void Light(Transform parent, string name, Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name + "Light");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << Layer;
            light.shadows = LightShadows.None;
        }

        private static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform child in t)
                SetLayer(child);
        }

        /// <summary>Shows a character (rebuilds the puppet only when it changes).</summary>
        public void Show(CharacterLook look)
        {
            if (look == null || look.Id == _shownId)
                return;
            _shownId = look.Id;
            if (_puppet != null)
                Destroy(_puppet.gameObject);
            _puppet = new GameObject("Preview " + look.Name).transform;
            _puppet.position = StagePosition;
            var visual = new GameObject("Visual (PLACEHOLDER)").transform;
            visual.SetParent(_puppet, false);
            CharacterStyler.Apply(visual, look);
            var rig = WeaponVisualRig.Attach(_puppet, visual);
            if (rig != null && !string.IsNullOrEmpty(look.SignatureWeapon))
                rig.ShowWeapon(WeaponCatalog.Get(look.SignatureWeapon));
            SetLayer(_puppet);
        }

        public void SetActive(bool active)
        {
            if (_camera != null)
                _camera.enabled = active;
            if (_puppet != null)
                _puppet.gameObject.SetActive(active);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _yaw -= eventData.delta.x * 0.4f;
            _lastDrag = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (_puppet == null)
                return;
            // New parts (weapon swaps, cloth) appear on the default layer: keep everything on the preview layer.
            if (Time.frameCount % 15 == 0)
                SetLayer(_puppet);
            if (Time.unscaledTime - _lastDrag > 2f)
                _yaw += Time.unscaledDeltaTime * 12f;
            _puppet.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void OnDestroy()
        {
            if (_texture != null)
                _texture.Release();
            if (_camera != null)
                Destroy(_camera.transform.parent.gameObject);
            if (_puppet != null)
                Destroy(_puppet.gameObject);
        }
    }
}
