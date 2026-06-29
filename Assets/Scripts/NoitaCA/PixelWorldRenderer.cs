using UnityEngine;

namespace NoitaCA
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PixelWorldRenderer : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Texture2D texture;
        private Sprite sprite;
        private Color32[] pixels;
        private PixelGrid grid;

        public int PixelsPerUnit { get; private set; } = 16;

        public Vector2 WorldSize
        {
            get
            {
                if (grid == null)
                {
                    return Vector2.zero;
                }

                return new Vector2(grid.Width / (float)PixelsPerUnit, grid.Height / (float)PixelsPerUnit);
            }
        }

        public void Initialize(PixelGrid worldGrid, int pixelsPerUnit)
        {
            grid = worldGrid;
            PixelsPerUnit = Mathf.Max(1, pixelsPerUnit);

            spriteRenderer = GetComponent<SpriteRenderer>();
            pixels = new Color32[grid.Width * grid.Height];

            if (sprite != null)
            {
                Destroy(sprite);
            }

            if (texture != null)
            {
                Destroy(texture);
            }

            texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false);
            texture.name = "Pixel World Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            sprite = Sprite.Create(
                texture,
                new Rect(0, 0, grid.Width, grid.Height),
                Vector2.zero,
                PixelsPerUnit);
            sprite.name = "Pixel World Sprite";
            spriteRenderer.sprite = sprite;
        }

        public void Render()
        {
            if (grid == null || texture == null)
            {
                return;
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    pixels[y * grid.Width + x] = grid.GetCell(x, y).Color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            return new Vector2Int(
                Mathf.FloorToInt(localPosition.x * PixelsPerUnit),
                Mathf.FloorToInt(localPosition.y * PixelsPerUnit));
        }

        public Vector3 CellToWorldCenter(int x, int y)
        {
            Vector3 localPosition = new Vector3(
                (x + 0.5f) / PixelsPerUnit,
                (y + 0.5f) / PixelsPerUnit,
                0f);
            return transform.TransformPoint(localPosition);
        }

        private void OnDestroy()
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
