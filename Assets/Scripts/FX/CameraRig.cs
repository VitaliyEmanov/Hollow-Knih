using AshenWick.Art;
using UnityEngine;

namespace AshenWick
{
    /// <summary>Orthographic follow camera with room clamping, look-ahead, trauma shake and zoom punches.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Follow;
        public float BaseSize = 8.2f;

        Camera cam;
        Rect bounds = new Rect(0, 0, 100, 100);
        Vector2 pos;
        float trauma;
        float zoom = 1f, zoomTarget = 1f;
        float lookAhead;
        Vector2 focusOverride;
        float focusWeight;
        SpriteRenderer vignette;

        public Camera Cam { get { return cam; } }

        public Rect View
        {
            get
            {
                float h = cam.orthographicSize, w = h * cam.aspect;
                var p = transform.position;
                return new Rect(p.x - w, p.y - h, w * 2, h * 2);
            }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = BaseSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            transform.position = new Vector3(0, 0, -10f);

            vignette = Gfx.Part(transform, "vignette", SpriteBank.Get("Vignette", ArtLibrary.Vignette), Vector2.zero, Layer.Overlay);
            vignette.transform.localPosition = new Vector3(0, 0, 10f);
        }

        public void SetBounds(Rect r) { bounds = r; }

        public void SetImmediate(Vector2 p)
        {
            pos = p;
            Apply(Vector2.zero);
        }

        public void SnapTo(Vector3 p)
        {
            pos = Clamp(p);
            lookAhead = 0f;
            Apply(Vector2.zero);
        }

        public void Shake(float amount) { trauma = Mathf.Clamp01(Mathf.Max(trauma, amount)); }

        public void Punch(float zoomAmount) { zoom = Mathf.Min(zoom, 1f - zoomAmount); }

        public void SetZoom(float z) { zoomTarget = z; }

        /// <summary>Pulls the camera toward a point (boss intros). weight 0..1</summary>
        public void Focus(Vector2 p, float weight) { focusOverride = p; focusWeight = weight; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-4f * dt));
            cam.orthographicSize = BaseSize * zoom;

            if (Follow != null)
            {
                Vector2 target = Follow.position;
                var p = Follow.GetComponent<Player>();
                if (p != null)
                {
                    lookAhead = Mathf.Lerp(lookAhead, p.Facing * 1.6f, 1f - Mathf.Exp(-2f * dt));
                    target.x += lookAhead;
                    target.y += 1.2f + p.LookOffset;
                }
                if (focusWeight > 0f) target = Vector2.Lerp(target, focusOverride, focusWeight);
                target = Clamp(target);
                float k = 1f - Mathf.Exp(-7f * dt);
                pos = Vector2.Lerp(pos, target, k);
                pos = Clamp(pos);
            }

            trauma = Mathf.Max(0f, trauma - dt * 1.4f);
            float s = trauma * trauma;
            float t = Time.unscaledTime * 32f;
            Vector2 shake = new Vector2(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * s * 1.4f;
            Apply(shake);

            // vignette covers the view
            var vs = vignette.sprite.bounds.size;
            float h = cam.orthographicSize * 2f, w = h * cam.aspect;
            vignette.transform.localScale = new Vector3(w / vs.x * 1.02f, h / vs.y * 1.02f, 1f);
        }

        void Apply(Vector2 shake)
        {
            transform.position = new Vector3(pos.x + shake.x, pos.y + shake.y, -10f);
        }

        Vector2 Clamp(Vector2 p)
        {
            if (cam == null) return p;
            float h = BaseSize * zoom, w = h * cam.aspect;
            float x = bounds.width <= w * 2 ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + w, bounds.xMax - w);
            float y = bounds.height <= h * 2 ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + h, bounds.yMax - h);
            return new Vector2(x, y);
        }
    }
}
