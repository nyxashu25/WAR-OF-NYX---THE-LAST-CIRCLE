using UnityEngine;
using UnityEngine.UI;

namespace WarOfNyx.UI.Common
{
    /// <summary>
    /// Circle outline drawn as a UI mesh with a constant line width at any radius (minimap circles).
    /// The ring is centred on the RectTransform's pivot; <see cref="Radius"/> is in canvas units and
    /// may be larger than the rect (clip it with a RectMask2D).
    /// </summary>
    public sealed class UiRing : MaskableGraphic
    {
        private float _radius = 50f;
        private float _thickness = 3f;
        private int _segments = 72;

        public float Radius
        {
            get => _radius;
            set
            {
                if (Mathf.Approximately(_radius, value))
                    return;
                _radius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public float Thickness
        {
            get => _thickness;
            set
            {
                _thickness = Mathf.Max(0.5f, value);
                SetVerticesDirty();
            }
        }

        public int Segments
        {
            get => _segments;
            set
            {
                _segments = Mathf.Clamp(value, 8, 256);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_radius <= 0.01f)
                return;

            float outer = _radius + _thickness * 0.5f;
            float inner = Mathf.Max(0f, _radius - _thickness * 0.5f);
            var c = (Color32)color;
            for (int i = 0; i <= _segments; i++)
            {
                float a = i * Mathf.PI * 2f / _segments;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                vh.AddVert(dir * outer, c, Vector2.zero);
                vh.AddVert(dir * inner, c, Vector2.zero);
                if (i > 0)
                {
                    int k = i * 2;
                    vh.AddTriangle(k - 2, k, k - 1);
                    vh.AddTriangle(k - 1, k, k + 1);
                }
            }
        }
    }

    /// <summary>Filled triangle pointing up (+Y), sized by the rect. Used for the player marker and direction arrows.</summary>
    public sealed class UiArrow : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            var c = (Color32)color;
            vh.AddVert(new Vector2(r.center.x, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector2(r.center.x, r.yMin + r.height * 0.3f), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
