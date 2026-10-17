using System;
using System.Collections.Generic;
using UnityEngine;

namespace PiratesDice.Game.UI
{
    /// <summary>
    /// Procedural art: wood, parchment, leather cups, coins, skulls and captain portraits, painted into textures
    /// at startup. It gives the game a finished look without art files, and each sprite can be swapped for
    /// hand-drawn art later by replacing one property.
    /// </summary>
    public static class Art
    {
        static Sprite _wood, _parchment, _cup, _coin, _skull, _plank;
        static readonly Dictionary<string, Sprite> Portraits = new Dictionary<string, Sprite>();

        public static Sprite Wood => _wood ? _wood : (_wood = MakeWood(256, 256, dark: false));
        public static Sprite Plank => _plank ? _plank : (_plank = MakeWood(128, 128, dark: true, border: 18));
        public static Sprite Parchment => _parchment ? _parchment : (_parchment = MakeParchment(128, 20));
        public static Sprite Cup => _cup ? _cup : (_cup = MakeCup(120, 140));
        public static Sprite Coin => _coin ? _coin : (_coin = MakeCoin(64));
        public static Sprite Skull => _skull ? _skull : (_skull = MakeSkull(96));

        public static Sprite Portrait(string captain)
        {
            if (Portraits.TryGetValue(captain, out Sprite s) && s) return s;
            s = MakePortrait(128, CaptainLook.For(captain));
            Portraits[captain] = s;
            return s;
        }

        // ---------- painting helpers ----------

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                Px = new Color[w * h];
            }

            /// <summary>Blends colour into every pixel by the coverage a shape function returns (0 to 1).</summary>
            public void Paint(Func<float, float, float> coverage, Func<float, float, Color> color)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float a = coverage(x + 0.5f, y + 0.5f);
                    if (a <= 0) continue;
                    Color c = color(x + 0.5f, y + 0.5f);
                    int i = y * W + x;
                    float alpha = Mathf.Clamp01(a) * c.a;
                    Color dst = Px[i];
                    float outA = alpha + dst.a * (1 - alpha);
                    Color rgb = outA > 0 ? (c * alpha + dst * dst.a * (1 - alpha)) / outA : Color.clear;
                    rgb.a = outA;
                    Px[i] = rgb;
                }
            }

            public void Paint(Func<float, float, float> coverage, Color color) => Paint(coverage, (x, y) => color);

            public Sprite ToSprite(string name, Vector4 border = default, bool tiled = false)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    name = name,
                    wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                tex.SetPixels(Px);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            }
        }

        static float Circle(float x, float y, float cx, float cy, float r) =>
            Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((1 - d) * Mathf.Min(rx, ry) + 0.5f);
        }

        static float RoundedBox(float x, float y, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Mathf.Clamp(x, x0 + r, x1 - r);
            float cy = Mathf.Clamp(y, y0 + r, y1 - r);
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        /// <summary>Coverage of a convex polygon given counter-clockwise points.</summary>
        static float Polygon(float x, float y, Vector2[] pts)
        {
            float inside = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                Vector2 edge = (b - a).normalized;
                float dist = (x - a.x) * -edge.y + (y - a.y) * edge.x; // left of edge is positive
                inside = Mathf.Min(inside, dist);
            }
            return Mathf.Clamp01(inside + 0.5f);
        }

        static float Noise(float x, float y, float scale) => Mathf.PerlinNoise(x * scale + 17.3f, y * scale + 41.7f);

        // ---------- sprites ----------

        static Sprite MakeWood(int w, int h, bool dark, int border = 0)
        {
            var c = new Canvas(w, h);
            Color baseA = dark ? new Color(0.22f, 0.13f, 0.07f) : new Color(0.36f, 0.22f, 0.12f);
            Color baseB = dark ? new Color(0.30f, 0.18f, 0.10f) : new Color(0.46f, 0.29f, 0.16f);
            int planks = dark ? 3 : 4;
            float plankH = h / (float)planks;

            c.Paint((x, y) => border > 0 ? RoundedBox(x, y, 0, 0, w, h, border) : 1f, (x, y) =>
            {
                int plank = Mathf.FloorToInt(y / plankH);
                float local = y - plank * plankH;
                // Grain: long stretched noise plus fine streaks, offset per plank so seams don't line up.
                float grain = Noise(x + plank * 61f, y * 6f, 0.035f);
                float streak = 0.5f + 0.5f * Mathf.Sin((y * 0.9f + Noise(x, y, 0.05f) * 9f) * 1.7f);
                Color col = Color.Lerp(baseA, baseB, grain * 0.75f + streak * 0.25f);
                col *= 0.92f + 0.08f * Mathf.Sin(plank * 2.3f);
                float seam = Mathf.Min(local, plankH - local);
                if (seam < 1.6f) col *= 0.45f + 0.3f * seam;
                col.a = 1;
                return col;
            });
            return c.ToSprite(dark ? "Plank" : "Wood", new Vector4(border, border, border, border), tiled: border == 0);
        }

        static Sprite MakeParchment(int size, int r)
        {
            var c = new Canvas(size, size);
            var light = new Color(0.96f, 0.90f, 0.75f);
            var dark = new Color(0.80f, 0.66f, 0.45f);
            c.Paint((x, y) => RoundedBox(x, y, 0, 0, size, size, r), (x, y) =>
            {
                float edge = Mathf.Min(Mathf.Min(x, size - x), Mathf.Min(y, size - y));
                float burn = Mathf.Clamp01(1 - edge / (r * 0.9f));
                float blotch = Noise(x, y, 0.08f) * 0.35f;
                Color col = Color.Lerp(light, dark, Mathf.Clamp01(blotch + burn * 0.9f));
                col.a = 1;
                return col;
            });
            return c.ToSprite("Parchment", new Vector4(r, r, r, r));
        }

        static Sprite MakeCup(int w, int h)
        {
            var c = new Canvas(w, h);
            var leather = new Color(0.45f, 0.24f, 0.12f);
            var shadow = new Color(0.24f, 0.12f, 0.06f);
            float top = h - 8, bottom = 12;
            var body = new[]
            {
                new Vector2(10, bottom), new Vector2(w - 10, bottom),
                new Vector2(w - 26, top), new Vector2(26, top),
            };
            c.Paint((x, y) => Polygon(x, y, body), (x, y) =>
            {
                // Rounded shading across the cup, darker at the edges.
                float u = Mathf.Abs(x - w / 2f) / (w / 2f);
                Color col = Color.Lerp(leather, shadow, u * u * 1.1f);
                col *= 0.9f + 0.2f * Noise(x, y, 0.2f);
                col.a = 1;
                return col;
            });
            // Brass rim at the mouth and a band near the top.
            c.Paint((x, y) => RoundedBox(x, y, 4, bottom - 6, w - 4, bottom + 10, 5), (x, y) =>
                Color.Lerp(new Color(0.86f, 0.66f, 0.26f), new Color(0.55f, 0.38f, 0.12f), Mathf.Abs(x - w / 2f) / (w / 2f)));
            c.Paint((x, y) => RoundedBox(x, y, 24, top - 22, w - 24, top - 14, 3), new Color(0.72f, 0.52f, 0.18f));
            // Stitching down the middle.
            c.Paint((x, y) => Mathf.Abs(x - w / 2f) < 1.4f && ((int)(y / 6) % 2 == 0) && y > bottom + 14 && y < top - 26 ? 1 : 0,
                new Color(0.9f, 0.8f, 0.6f, 0.8f));
            return c.ToSprite("Cup");
        }

        static Sprite MakeCoin(int size)
        {
            var c = new Canvas(size, size);
            float r = size / 2f - 1;
            c.Paint((x, y) => Circle(x, y, size / 2f, size / 2f, r), new Color(0.62f, 0.44f, 0.10f));
            c.Paint((x, y) => Circle(x, y, size / 2f, size / 2f, r - 4), (x, y) =>
                Color.Lerp(new Color(1f, 0.86f, 0.38f), new Color(0.85f, 0.62f, 0.18f), (y / size) * -1 + 1));
            c.Paint((x, y) => Circle(x, y, size * 0.38f, size * 0.64f, size * 0.1f), new Color(1, 1, 0.9f, 0.6f));
            return c.ToSprite("Coin");
        }

        static Sprite MakeSkull(int size)
        {
            var c = new Canvas(size, size);
            var bone = new Color(0.95f, 0.93f, 0.86f);
            float cx = size / 2f;
            c.Paint((x, y) => Mathf.Max(Circle(x, y, cx, size * 0.58f, size * 0.32f),
                RoundedBox(x, y, cx - size * 0.18f, size * 0.16f, cx + size * 0.18f, size * 0.42f, 6)), bone);
            var hole = new Color(0.12f, 0.08f, 0.06f);
            c.Paint((x, y) => Circle(x, y, cx - size * 0.13f, size * 0.55f, size * 0.09f), hole);
            c.Paint((x, y) => Circle(x, y, cx + size * 0.13f, size * 0.55f, size * 0.09f), hole);
            c.Paint((x, y) => Polygon(x, y, new[]
            {
                new Vector2(cx - 5, size * 0.38f), new Vector2(cx + 5, size * 0.38f), new Vector2(cx, size * 0.46f),
            }), hole);
            for (int i = -1; i <= 1; i++)
            {
                float tx = cx + i * size * 0.09f;
                c.Paint((x, y) => Mathf.Abs(x - tx) < 1.2f && y > size * 0.17f && y < size * 0.3f ? 1 : 0, hole);
            }
            return c.ToSprite("Skull");
        }

        static Sprite MakePortrait(int size, CaptainLook look)
        {
            var c = new Canvas(size, size);
            float cx = size / 2f, r = size / 2f - 2;

            // Backdrop medallion with a gold ring.
            c.Paint((x, y) => Circle(x, y, cx, cx, r), new Color(0.85f, 0.65f, 0.22f));
            c.Paint((x, y) => Circle(x, y, cx, cx, r - 5), (x, y) => Color.Lerp(look.Backdrop, look.Backdrop * 0.6f, y / size * -1 + 1));

            // Clip everything else to the inner circle.
            Func<float, float, float> clip = (x, y) => Circle(x, y, cx, cx, r - 5);

            // Coat, then face.
            c.Paint((x, y) => Mathf.Min(clip(x, y), Ellipse(x, y, cx, size * 0.02f, size * 0.42f, size * 0.3f)), look.Coat);
            c.Paint((x, y) => Mathf.Min(clip(x, y), Ellipse(x, y, cx, size * 0.47f, size * 0.24f, size * 0.27f)), look.Skin);

            if (look.Beard)
                c.Paint((x, y) => Mathf.Min(clip(x, y),
                    Mathf.Min(Ellipse(x, y, cx, size * 0.33f, size * 0.24f, size * 0.17f), y < size * 0.42f ? 1 : 0)), look.Hair);

            // Eyes, eyepatch, grin.
            var ink = new Color(0.1f, 0.07f, 0.05f);
            c.Paint((x, y) => Circle(x, y, cx + size * 0.09f, size * 0.51f, size * 0.03f), ink);
            if (look.Eyepatch)
            {
                c.Paint((x, y) => Circle(x, y, cx - size * 0.09f, size * 0.51f, size * 0.065f), ink);
                c.Paint((x, y) => Mathf.Abs((y - size * 0.51f) - (x - cx) * 0.55f) < 1.6f && Circle(x, y, cx, size * 0.47f, size * 0.25f) > 0 ? 1 : 0, ink);
            }
            else
            {
                c.Paint((x, y) => Circle(x, y, cx - size * 0.09f, size * 0.51f, size * 0.03f), ink);
            }
            c.Paint((x, y) => Mathf.Min(Ellipse(x, y, cx, size * 0.38f, size * 0.09f, size * 0.035f), y < size * 0.385f ? 1 : 0),
                new Color(0.45f, 0.12f, 0.1f));

            // Tricorne hat: a wide dark brim and crown, with a skull badge.
            c.Paint((x, y) => Mathf.Min(clip(x, y), Ellipse(x, y, cx, size * 0.66f, size * 0.38f, size * 0.09f)), look.Hat);
            c.Paint((x, y) => Mathf.Min(clip(x, y), Polygon(x, y, new[]
            {
                new Vector2(cx - size * 0.28f, size * 0.66f), new Vector2(cx + size * 0.28f, size * 0.66f),
                new Vector2(cx + size * 0.16f, size * 0.86f), new Vector2(cx - size * 0.16f, size * 0.86f),
            })), look.Hat);
            c.Paint((x, y) => Circle(x, y, cx, size * 0.74f, size * 0.05f), new Color(0.95f, 0.93f, 0.86f));

            return c.ToSprite("Portrait");
        }
    }

    /// <summary>How each named captain is painted. Unknown names get a look derived from the name.</summary>
    public struct CaptainLook
    {
        public Color Backdrop, Coat, Skin, Hair, Hat;
        public bool Eyepatch, Beard;

        static readonly Color[] Skins =
        {
            new Color(0.96f, 0.80f, 0.66f), new Color(0.80f, 0.60f, 0.44f), new Color(0.55f, 0.38f, 0.26f), new Color(0.90f, 0.72f, 0.56f),
        };

        public static CaptainLook For(string name)
        {
            switch (name)
            {
                case "Calico Jane":
                    return Make(new Color(0.55f, 0.18f, 0.35f), new Color(0.75f, 0.2f, 0.2f), Skins[0], new Color(0.6f, 0.3f, 0.1f), false, false);
                case "One-Eyed Morgan":
                    return Make(new Color(0.15f, 0.30f, 0.45f), new Color(0.15f, 0.2f, 0.4f), Skins[1], new Color(0.15f, 0.1f, 0.08f), true, true);
                case "Barnacle Bess":
                    return Make(new Color(0.18f, 0.45f, 0.42f), new Color(0.3f, 0.45f, 0.25f), Skins[2], new Color(0.1f, 0.08f, 0.06f), false, false);
                case "Salty Pete":
                    return Make(new Color(0.55f, 0.38f, 0.12f), new Color(0.5f, 0.15f, 0.12f), Skins[3], new Color(0.85f, 0.85f, 0.8f), false, true);
                case "Mad Maggie Flint":
                    return Make(new Color(0.40f, 0.18f, 0.55f), new Color(0.2f, 0.2f, 0.2f), Skins[0], new Color(0.8f, 0.35f, 0.1f), true, false);
                case "Old Tom Rumbelly":
                    return Make(new Color(0.35f, 0.40f, 0.20f), new Color(0.35f, 0.25f, 0.15f), Skins[1], new Color(0.7f, 0.7f, 0.7f), false, true);
                default:
                    int h = 0;
                    foreach (char ch in name ?? "") h = h * 31 + ch;
                    h &= 0x7fffffff;
                    return Make(Color.HSVToRGB((h % 360) / 360f, 0.55f, 0.5f), new Color(0.25f, 0.2f, 0.3f), Skins[h % Skins.Length],
                        new Color(0.2f, 0.12f, 0.08f), h % 3 == 0, h % 2 == 0);
            }
        }

        static CaptainLook Make(Color backdrop, Color coat, Color skin, Color hair, bool eyepatch, bool beard) => new CaptainLook
        {
            Backdrop = backdrop,
            Coat = coat,
            Skin = skin,
            Hair = hair,
            Hat = new Color(0.1f, 0.08f, 0.07f),
            Eyepatch = eyepatch,
            Beard = beard,
        };
    }
}
