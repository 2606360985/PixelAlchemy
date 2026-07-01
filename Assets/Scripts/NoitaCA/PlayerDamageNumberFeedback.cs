using UnityEngine;

namespace NoitaCA
{
    public sealed class PlayerDamageNumberFeedback : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Texture2D texture;
        private Sprite sprite;
        private Vector3 velocity;
        private float lifetime = 0.8f;
        private float age;

        public static void Spawn(Vector3 worldPosition, Transform parent, int pixelsPerUnit, float damage, int sortingLayerId, int sortingOrder)
        {
            int displayDamage = Mathf.Max(1, Mathf.CeilToInt(damage));
            GameObject numberObject = new GameObject("Player Damage Number");
            numberObject.transform.SetParent(parent, true);
            numberObject.transform.position = worldPosition;

            PlayerDamageNumberFeedback feedback = numberObject.AddComponent<PlayerDamageNumberFeedback>();
            feedback.Initialize("-" + displayDamage, Mathf.Max(1, pixelsPerUnit), sortingLayerId, sortingOrder);
        }

        private void Initialize(string text, int pixelsPerUnit, int sortingLayerId, int sortingOrder)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerID = sortingLayerId;
            spriteRenderer.sortingOrder = sortingOrder;
            sprite = CreateNumberSprite(text, pixelsPerUnit);
            spriteRenderer.sprite = sprite;
            velocity = new Vector3(0.18f, 1.15f, 0f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
            velocity += Vector3.down * 0.65f * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.82f, t);

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = 1f - t * t;
                spriteRenderer.color = color;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private Sprite CreateNumberSprite(string text, int pixelsPerUnit)
        {
            const int GlyphWidth = 3;
            const int GlyphHeight = 5;
            const int PixelScale = 2;
            const int Spacing = 1;
            int safeLength = Mathf.Max(1, text.Length);
            int width = (safeLength * (GlyphWidth + Spacing) - Spacing + 2) * PixelScale;
            int height = (GlyphHeight + 2) * PixelScale;

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Player Damage Number Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32[] colors = new Color32[width * height];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = clear;
            }

            Color32 shadow = new Color32(74, 12, 18, 210);
            Color32 fill = new Color32(255, 76, 58, 255);
            Color32 highlight = new Color32(255, 218, 112, 255);
            int cursor = PixelScale;
            for (int i = 0; i < text.Length; i++)
            {
                DrawGlyph(colors, width, height, cursor + PixelScale, PixelScale, text[i], shadow, PixelScale);
                DrawGlyph(colors, width, height, cursor, PixelScale * 2, text[i], fill, PixelScale);
                DrawGlyphHighlight(colors, width, height, cursor, PixelScale * 2, text[i], highlight, PixelScale);
                cursor += (GlyphWidth + Spacing) * PixelScale;
            }

            texture.SetPixels32(colors);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private static void DrawGlyph(Color32[] colors, int width, int height, int originX, int originY, char glyph, Color32 color, int scale)
        {
            string[] rows = GetGlyphRows(glyph);
            for (int row = 0; row < rows.Length; row++)
            {
                string pattern = rows[row];
                for (int col = 0; col < pattern.Length; col++)
                {
                    if (pattern[col] != '1')
                    {
                        continue;
                    }

                    FillScaledPixel(colors, width, height, originX + col * scale, originY + (rows.Length - 1 - row) * scale, scale, color);
                }
            }
        }

        private static void DrawGlyphHighlight(Color32[] colors, int width, int height, int originX, int originY, char glyph, Color32 color, int scale)
        {
            string[] rows = GetGlyphRows(glyph);
            for (int row = 0; row < Mathf.Min(2, rows.Length); row++)
            {
                string pattern = rows[row];
                for (int col = 0; col < pattern.Length; col++)
                {
                    if (pattern[col] == '1')
                    {
                        FillScaledPixel(colors, width, height, originX + col * scale, originY + (rows.Length - 1 - row) * scale, 1, color);
                    }
                }
            }
        }

        private static void FillScaledPixel(Color32[] colors, int width, int height, int x, int y, int scale, Color32 color)
        {
            for (int py = 0; py < scale; py++)
            {
                for (int px = 0; px < scale; px++)
                {
                    int targetX = x + px;
                    int targetY = y + py;
                    if (targetX >= 0 && targetX < width && targetY >= 0 && targetY < height)
                    {
                        colors[targetY * width + targetX] = color;
                    }
                }
            }
        }

        private static string[] GetGlyphRows(char glyph)
        {
            switch (glyph)
            {
                case '-':
                    return new[] { "000", "000", "111", "000", "000" };
                case '0':
                    return new[] { "111", "101", "101", "101", "111" };
                case '1':
                    return new[] { "010", "110", "010", "010", "111" };
                case '2':
                    return new[] { "111", "001", "111", "100", "111" };
                case '3':
                    return new[] { "111", "001", "111", "001", "111" };
                case '4':
                    return new[] { "101", "101", "111", "001", "001" };
                case '5':
                    return new[] { "111", "100", "111", "001", "111" };
                case '6':
                    return new[] { "111", "100", "111", "101", "111" };
                case '7':
                    return new[] { "111", "001", "010", "010", "010" };
                case '8':
                    return new[] { "111", "101", "111", "101", "111" };
                case '9':
                    return new[] { "111", "101", "111", "001", "111" };
                default:
                    return new[] { "000", "000", "000", "000", "000" };
            }
        }

        private void OnDestroy()
        {
            DestroyObject(sprite);
            DestroyObject(texture);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
