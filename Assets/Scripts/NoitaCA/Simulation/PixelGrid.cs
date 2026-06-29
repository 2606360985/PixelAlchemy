using System.Collections.Generic;
using UnityEngine;

namespace NoitaCA
{
    public class PixelGrid
    {
        private readonly Pixel[,] cells;
        private readonly bool[,] activeCells;
        private readonly bool[,] nextActiveCells;
        private readonly List<Vector2Int> activeCellList = new List<Vector2Int>(1024);
        private readonly List<Vector2Int> nextActiveCellList = new List<Vector2Int>(1024);

        private bool[,] activeChunks;
        private bool[,] nextActiveChunks;
        private bool[,] changedChunks;
        private int[,] chunkSleepFrames;
        private readonly List<Vector2Int> activeChunkList = new List<Vector2Int>(64);
        private readonly List<Vector2Int> nextActiveChunkList = new List<Vector2Int>(64);

        public int Width { get; }
        public int Height { get; }
        public int ChunkSize { get; private set; } = 16;
        public int ChunkColumns { get; private set; }
        public int ChunkRows { get; private set; }
        public int ChunkSleepDelay { get; private set; } = 2;
        public int ProcessedPixelsThisStep { get; private set; }
        public int ChangedPixelsThisStep { get; private set; }
        public int ActivePixelCount => activeCellList.Count;
        public int ActiveChunkCount => activeChunkList.Count;

        public PixelGrid(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            cells = new Pixel[Width, Height];
            activeCells = new bool[Width, Height];
            nextActiveCells = new bool[Width, Height];
            ConfigureOptimization(16, 2);
            Clear();
        }

        public void ConfigureOptimization(int chunkSize, int chunkSleepDelay)
        {
            ChunkSize = Mathf.Max(4, chunkSize);
            ChunkSleepDelay = Mathf.Max(0, chunkSleepDelay);
            ChunkColumns = Mathf.CeilToInt(Width / (float)ChunkSize);
            ChunkRows = Mathf.CeilToInt(Height / (float)ChunkSize);
            activeChunks = new bool[ChunkColumns, ChunkRows];
            nextActiveChunks = new bool[ChunkColumns, ChunkRows];
            changedChunks = new bool[ChunkColumns, ChunkRows];
            chunkSleepFrames = new int[ChunkColumns, ChunkRows];
            activeChunkList.Clear();
            nextActiveChunkList.Clear();
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public Pixel GetCell(int x, int y)
        {
            return cells[x, y];
        }

        public void SetCell(int x, int y, Pixel pixel)
        {
            if (!InBounds(x, y))
            {
                return;
            }

            cells[x, y] = pixel;
        }

        public bool IsAir(int x, int y)
        {
            return InBounds(x, y) && MaterialDatabase.Get(cells[x, y].MaterialType).IsAir;
        }

        public bool IsEmpty(int x, int y)
        {
            return IsAir(x, y);
        }

        public void SetMaterial(int x, int y, MaterialType materialType)
        {
            if (!InBounds(x, y))
            {
                return;
            }

            cells[x, y] = Pixel.FromMaterial(materialType);
            MarkChanged(x, y);
        }

        public void SwapCells(int firstX, int firstY, int secondX, int secondY)
        {
            if (!InBounds(firstX, firstY) || !InBounds(secondX, secondY))
            {
                return;
            }

            Pixel first = cells[firstX, firstY];
            Pixel second = cells[secondX, secondY];
            cells[firstX, firstY] = second;
            cells[secondX, secondY] = first;
            MarkChanged(firstX, firstY);
            MarkChanged(secondX, secondY);
        }

        public void BeginSimulationStep()
        {
            ProcessedPixelsThisStep = 0;
            ChangedPixelsThisStep = 0;
            ClearNextActiveCells();
            ClearNextActiveChunks();
            ClearChangedChunks();
        }

        public void EndSimulationStep(PixelSimulationMode mode)
        {
            if (mode == PixelSimulationMode.ActivePixels)
            {
                SwapActiveCellBuffers();
            }
            else if (mode == PixelSimulationMode.ChunkBased)
            {
                BuildNextChunkFrame();
                SwapActiveChunkBuffers();
            }
        }

        public bool TryConsumePixelBudget(int maxPixelsPerStep)
        {
            if (maxPixelsPerStep > 0 && ProcessedPixelsThisStep >= maxPixelsPerStep)
            {
                return false;
            }

            ProcessedPixelsThisStep++;
            return true;
        }

        public void ClearUpdatedFlags(PixelSimulationMode mode)
        {
            if (mode == PixelSimulationMode.ActivePixels)
            {
                for (int i = 0; i < activeCellList.Count; i++)
                {
                    Vector2Int cellPosition = activeCellList[i];
                    ClearUpdatedFlag(cellPosition.x, cellPosition.y);
                }

                return;
            }

            if (mode == PixelSimulationMode.ChunkBased)
            {
                for (int i = 0; i < activeChunkList.Count; i++)
                {
                    Vector2Int chunk = activeChunkList[i];
                    ForEachCellInChunk(chunk.x, chunk.y, ClearUpdatedFlag);
                }

                return;
            }

            ClearUpdatedFlags();
        }

        public void ClearUpdatedFlags()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    ClearUpdatedFlag(x, y);
                }
            }
        }

        public void MarkChanged(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return;
            }

            ChangedPixelsThisStep++;
            MarkActiveArea(x, y, 1);
            MarkChunkAndNeighborsActive(x, y);
        }

        public void MarkActiveArea(int centerX, int centerY, int radius)
        {
            int safeRadius = Mathf.Max(0, radius);
            for (int y = centerY - safeRadius; y <= centerY + safeRadius; y++)
            {
                for (int x = centerX - safeRadius; x <= centerX + safeRadius; x++)
                {
                    MarkCurrentActiveCell(x, y);
                    MarkNextActiveCell(x, y);
                }
            }
        }

        public void ActivateAll()
        {
            ClearActiveCells();
            ClearActiveChunks();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!activeCells[x, y])
                    {
                        activeCells[x, y] = true;
                        activeCellList.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int cy = 0; cy < ChunkRows; cy++)
            {
                for (int cx = 0; cx < ChunkColumns; cx++)
                {
                    activeChunks[cx, cy] = true;
                    activeChunkList.Add(new Vector2Int(cx, cy));
                }
            }
        }

        public Vector2Int GetActiveCell(int index)
        {
            return activeCellList[index];
        }

        public Vector2Int GetActiveChunk(int index)
        {
            return activeChunkList[index];
        }

        public bool IsCellActive(int x, int y)
        {
            return InBounds(x, y) && activeCells[x, y];
        }

        public bool IsChunkActive(int chunkX, int chunkY)
        {
            return chunkX >= 0
                && chunkX < ChunkColumns
                && chunkY >= 0
                && chunkY < ChunkRows
                && activeChunks[chunkX, chunkY];
        }

        public void ForEachCellInChunk(int chunkX, int chunkY, System.Action<int, int> action)
        {
            int minX = chunkX * ChunkSize;
            int minY = chunkY * ChunkSize;
            int maxX = Mathf.Min(minX + ChunkSize, Width);
            int maxY = Mathf.Min(minY + ChunkSize, Height);

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    action(x, y);
                }
            }
        }

        public void CountMaterials(out int nonAirPixels, out int waterPixels)
        {
            nonAirPixels = 0;
            waterPixels = 0;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    MaterialType type = cells[x, y].MaterialType;
                    if (type == MaterialType.Water)
                    {
                        waterPixels++;
                    }

                    if (!MaterialDatabase.Get(type).IsAir)
                    {
                        nonAirPixels++;
                    }
                }
            }
        }

        public void PaintCircle(int centerX, int centerY, int radius, MaterialType materialType)
        {
            int safeRadius = Mathf.Max(1, radius);
            int radiusSquared = safeRadius * safeRadius;

            for (int y = centerY - safeRadius; y <= centerY + safeRadius; y++)
            {
                for (int x = centerX - safeRadius; x <= centerX + safeRadius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetMaterial(x, y, materialType);
                    }
                }
            }
        }

        private void Clear()
        {
            Pixel air = Pixel.FromMaterial(MaterialType.Air);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    cells[x, y] = air;
                }
            }

            ActivateAll();
        }

        private void ClearUpdatedFlag(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return;
            }

            Pixel pixel = cells[x, y];
            pixel.UpdatedThisFrame = false;
            cells[x, y] = pixel;
        }

        private void MarkNextActiveCell(int x, int y)
        {
            if (!InBounds(x, y) || nextActiveCells[x, y])
            {
                return;
            }

            nextActiveCells[x, y] = true;
            nextActiveCellList.Add(new Vector2Int(x, y));
        }

        private void MarkChunkAndNeighborsActive(int x, int y)
        {
            int chunkX = Mathf.Clamp(x / ChunkSize, 0, ChunkColumns - 1);
            int chunkY = Mathf.Clamp(y / ChunkSize, 0, ChunkRows - 1);

            if (chunkX >= 0 && chunkX < ChunkColumns && chunkY >= 0 && chunkY < ChunkRows)
            {
                changedChunks[chunkX, chunkY] = true;
            }

            for (int cy = chunkY - 1; cy <= chunkY + 1; cy++)
            {
                for (int cx = chunkX - 1; cx <= chunkX + 1; cx++)
                {
                    MarkCurrentActiveChunk(cx, cy);
                    MarkNextActiveChunk(cx, cy);
                }
            }
        }

        private void MarkCurrentActiveCell(int x, int y)
        {
            if (!InBounds(x, y) || activeCells[x, y])
            {
                return;
            }

            activeCells[x, y] = true;
            activeCellList.Add(new Vector2Int(x, y));
        }

        private void MarkNextActiveChunk(int chunkX, int chunkY)
        {
            if (chunkX < 0 || chunkX >= ChunkColumns || chunkY < 0 || chunkY >= ChunkRows || nextActiveChunks[chunkX, chunkY])
            {
                return;
            }

            nextActiveChunks[chunkX, chunkY] = true;
            nextActiveChunkList.Add(new Vector2Int(chunkX, chunkY));
        }

        private void MarkCurrentActiveChunk(int chunkX, int chunkY)
        {
            if (chunkX < 0 || chunkX >= ChunkColumns || chunkY < 0 || chunkY >= ChunkRows || activeChunks[chunkX, chunkY])
            {
                return;
            }

            activeChunks[chunkX, chunkY] = true;
            activeChunkList.Add(new Vector2Int(chunkX, chunkY));
        }

        private void BuildNextChunkFrame()
        {
            // Chunk / dirty-region teaching simplification:
            // changed chunks wake themselves and neighbors; unchanged chunks stay awake briefly, then sleep.
            for (int i = 0; i < activeChunkList.Count; i++)
            {
                Vector2Int chunk = activeChunkList[i];
                if (changedChunks[chunk.x, chunk.y])
                {
                    chunkSleepFrames[chunk.x, chunk.y] = 0;
                    MarkNextActiveChunk(chunk.x, chunk.y);
                    continue;
                }

                chunkSleepFrames[chunk.x, chunk.y]++;
                if (chunkSleepFrames[chunk.x, chunk.y] <= ChunkSleepDelay)
                {
                    MarkNextActiveChunk(chunk.x, chunk.y);
                }
            }
        }

        private void ClearActiveCells()
        {
            for (int i = 0; i < activeCellList.Count; i++)
            {
                Vector2Int cell = activeCellList[i];
                activeCells[cell.x, cell.y] = false;
            }

            activeCellList.Clear();
        }

        private void ClearNextActiveCells()
        {
            for (int i = 0; i < nextActiveCellList.Count; i++)
            {
                Vector2Int cell = nextActiveCellList[i];
                nextActiveCells[cell.x, cell.y] = false;
            }

            nextActiveCellList.Clear();
        }

        private void SwapActiveCellBuffers()
        {
            ClearActiveCells();
            for (int i = 0; i < nextActiveCellList.Count; i++)
            {
                Vector2Int cell = nextActiveCellList[i];
                activeCells[cell.x, cell.y] = true;
                activeCellList.Add(cell);
            }
        }

        private void ClearActiveChunks()
        {
            for (int i = 0; i < activeChunkList.Count; i++)
            {
                Vector2Int chunk = activeChunkList[i];
                activeChunks[chunk.x, chunk.y] = false;
            }

            activeChunkList.Clear();
        }

        private void ClearNextActiveChunks()
        {
            for (int i = 0; i < nextActiveChunkList.Count; i++)
            {
                Vector2Int chunk = nextActiveChunkList[i];
                nextActiveChunks[chunk.x, chunk.y] = false;
            }

            nextActiveChunkList.Clear();
        }

        private void SwapActiveChunkBuffers()
        {
            ClearActiveChunks();
            for (int i = 0; i < nextActiveChunkList.Count; i++)
            {
                Vector2Int chunk = nextActiveChunkList[i];
                activeChunks[chunk.x, chunk.y] = true;
                activeChunkList.Add(chunk);
            }
        }

        private void ClearChangedChunks()
        {
            for (int y = 0; y < ChunkRows; y++)
            {
                for (int x = 0; x < ChunkColumns; x++)
                {
                    changedChunks[x, y] = false;
                }
            }
        }
    }
}
