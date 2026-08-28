using System.Collections.Generic;
using UnityEngine;

namespace ServerGame.Utils
{
    // Sprites de la UI generados por código (rectángulos redondeados y círculos), sin
    // ningún asset binario.
    public static class TextureFactory
    {
        static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        static Sprite _plain;
        static Sprite _circle;

        /// <summary>Sprite blanco de 1 px. Se tiñe con Image.color.</summary>
        public static Sprite Plain
        {
            get
            {
                if (_plain == null)
                {
                    var tex = NewTexture(4, 4, "SG_Plain");
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    _plain = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f,
                        0, SpriteMeshType.FullRect);
                    _plain.name = "SG_Plain";
                }
                return _plain;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    const int size = 64;
                    var tex = NewTexture(size, size, "SG_Circle");
                    var pixels = new Color32[size * size];
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x + 0.5f - r;
                            float dy = y + 0.5f - r;
                            float d = Mathf.Sqrt(dx * dx + dy * dy) - (r - 0.5f);
                            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                            pixels[y * size + x] = new Color32(255, 255, 255, a);
                        }
                    }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                        0, SpriteMeshType.FullRect);
                    _circle.name = "SG_Circle";
                }
                return _circle;
            }
        }

        // preparado para Image.Type.Sliced: el radio no depende del tamaño del panel
        public static Sprite RoundedRect(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 48);
            if (RoundedCache.TryGetValue(radius, out var cached) && cached != null) return cached;

            int size = radius * 2 + 2;
            var tex = NewTexture(size, size, "SG_Rounded_" + radius);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half - radius;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - inner;
                    float py = Mathf.Abs(y + 0.5f - half) - inner;
                    float qx = Mathf.Max(px, 0f);
                    float qy = Mathf.Max(py, 0f);
                    // SDF de rectángulo redondeado
                    float d = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px, py), 0f) - radius + 0.5f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            var border = new Vector4(radius, radius, radius, radius);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
            sprite.name = "SG_Rounded_" + radius;
            RoundedCache[radius] = sprite;
            return sprite;
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            return tex;
        }
    }
}
