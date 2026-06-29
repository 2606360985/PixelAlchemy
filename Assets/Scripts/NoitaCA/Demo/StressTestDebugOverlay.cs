using UnityEngine;

namespace NoitaCA
{
    public sealed class StressTestDebugOverlay : MonoBehaviour
    {
        private const int MaxActivePixelRects = 8000;
        private static Texture2D whiteTexture;

        private StressTestBootstrap bootstrap;
        private Camera targetCamera;

        public void Initialize(StressTestBootstrap stressTestBootstrap, Camera cameraToUse)
        {
            bootstrap = stressTestBootstrap;
            targetCamera = cameraToUse;
            EnsureTexture();
        }

        private void OnGUI()
        {
            if (bootstrap == null || bootstrap.Grid == null || bootstrap.Renderer == null || targetCamera == null)
            {
                return;
            }

            StressTestConfig config = bootstrap.Config;
            if (config.showChunkBoundaries)
            {
                DrawChunkBoundaries();
            }

            if (config.showActiveRegions)
            {
                DrawActiveRegions();
            }
        }

        private void DrawChunkBoundaries()
        {
            PixelGrid grid = bootstrap.Grid;
            Color lineColor = new Color(1f, 1f, 1f, 0.22f);
            for (int cy = 0; cy < grid.ChunkRows; cy++)
            {
                for (int cx = 0; cx < grid.ChunkColumns; cx++)
                {
                    Rect rect = CellRectToGuiRect(cx * grid.ChunkSize, cy * grid.ChunkSize, grid.ChunkSize, grid.ChunkSize);
                    DrawRectOutline(rect, lineColor, 1f);
                }
            }
        }

        private void DrawActiveRegions()
        {
            PixelGrid grid = bootstrap.Grid;
            if (bootstrap.Mode == PixelSimulationMode.ChunkBased)
            {
                for (int i = 0; i < grid.ActiveChunkCount; i++)
                {
                    Vector2Int chunk = grid.GetActiveChunk(i);
                    Rect rect = CellRectToGuiRect(chunk.x * grid.ChunkSize, chunk.y * grid.ChunkSize, grid.ChunkSize, grid.ChunkSize);
                    DrawRect(rect, new Color(0.1f, 1f, 0.35f, 0.13f));
                    DrawRectOutline(rect, new Color(0.2f, 1f, 0.45f, 0.55f), 2f);
                }

                return;
            }

            if (bootstrap.Mode == PixelSimulationMode.ActivePixels)
            {
                int count = Mathf.Min(MaxActivePixelRects, grid.ActivePixelCount);
                for (int i = 0; i < count; i++)
                {
                    Vector2Int cell = grid.GetActiveCell(i);
                    Rect rect = CellRectToGuiRect(cell.x, cell.y, 1, 1);
                    DrawRect(rect, new Color(0.1f, 1f, 0.35f, 0.3f));
                }
            }
        }

        private Rect CellRectToGuiRect(int cellX, int cellY, int cellWidth, int cellHeight)
        {
            PixelWorldRenderer renderer = bootstrap.Renderer;
            float ppu = Mathf.Max(1, renderer.PixelsPerUnit);
            Vector3 minWorld = renderer.transform.TransformPoint(new Vector3(cellX / ppu, cellY / ppu, 0f));
            Vector3 maxWorld = renderer.transform.TransformPoint(new Vector3((cellX + cellWidth) / ppu, (cellY + cellHeight) / ppu, 0f));
            Vector3 minScreen = targetCamera.WorldToScreenPoint(minWorld);
            Vector3 maxScreen = targetCamera.WorldToScreenPoint(maxWorld);

            float xMin = Mathf.Min(minScreen.x, maxScreen.x);
            float xMax = Mathf.Max(minScreen.x, maxScreen.x);
            float yMin = Screen.height - Mathf.Max(minScreen.y, maxScreen.y);
            float yMax = Screen.height - Mathf.Min(minScreen.y, maxScreen.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            EnsureTexture();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = previous;
        }

        private static void EnsureTexture()
        {
            if (whiteTexture != null)
            {
                return;
            }

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply(false);
        }
    }
}
