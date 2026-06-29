using System;
using UnityEngine;

namespace NoitaCA
{
    public sealed class MovementSystem
    {
        private readonly System.Random random;

        public MovementSystem(System.Random random)
        {
            this.random = random ?? new System.Random();
        }

        public void Step(PixelGrid grid, PixelSimulationMode mode, int maxPixelsPerStep)
        {
            if (grid == null)
            {
                return;
            }

            if (mode == PixelSimulationMode.ActivePixels)
            {
                StepActivePixels(grid, maxPixelsPerStep);
            }
            else if (mode == PixelSimulationMode.ChunkBased)
            {
                StepActiveChunks(grid, maxPixelsPerStep);
            }
            else
            {
                StepFullScan(grid, maxPixelsPerStep);
            }
        }

        private void StepFullScan(PixelGrid grid, int maxPixelsPerStep)
        {
            // Naive cellular automata: visit every pixel, even quiet air and stone.
            // The row direction is randomized to reduce large-water left/right bias.
            for (int y = 0; y < grid.Height; y++)
            {
                if (random.Next(0, 2) == 0)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        if (!StepCell(grid, x, y, maxPixelsPerStep))
                        {
                            return;
                        }
                    }
                }
                else
                {
                    for (int x = grid.Width - 1; x >= 0; x--)
                    {
                        if (!StepCell(grid, x, y, maxPixelsPerStep))
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void StepActivePixels(PixelGrid grid, int maxPixelsPerStep)
        {
            // Active Pixel optimization: only pixels woken by nearby changes are visited.
            int count = grid.ActivePixelCount;
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = grid.GetActiveCell(i);
                if (!StepCell(grid, cell.x, cell.y, maxPixelsPerStep))
                {
                    return;
                }
            }
        }

        private void StepActiveChunks(PixelGrid grid, int maxPixelsPerStep)
        {
            // Chunk / dirty-region optimization: scan whole chunks, but skip sleeping chunks.
            int count = grid.ActiveChunkCount;
            for (int i = 0; i < count; i++)
            {
                Vector2Int chunk = grid.GetActiveChunk(i);
                int minY = chunk.y * grid.ChunkSize;
                int maxY = Mathf.Min(minY + grid.ChunkSize, grid.Height);
                int minX = chunk.x * grid.ChunkSize;
                int maxX = Mathf.Min(minX + grid.ChunkSize, grid.Width);

                for (int y = minY; y < maxY; y++)
                {
                    if (random.Next(0, 2) == 0)
                    {
                        for (int x = minX; x < maxX; x++)
                        {
                            if (!StepCell(grid, x, y, maxPixelsPerStep))
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        for (int x = maxX - 1; x >= minX; x--)
                        {
                            if (!StepCell(grid, x, y, maxPixelsPerStep))
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool StepCell(PixelGrid grid, int x, int y, int maxPixelsPerStep)
        {
            if (!grid.TryConsumePixelBudget(maxPixelsPerStep))
            {
                return false;
            }

            Pixel pixel = grid.GetCell(x, y);
            if (pixel.UpdatedThisFrame)
            {
                return true;
            }

            MaterialDefinition definition = MaterialDatabase.Get(pixel.MaterialType);
            if (definition.MovementMode == PixelMovementMode.Static || random.NextDouble() > definition.MoveProbability)
            {
                MarkUpdated(grid, x, y, pixel);
                return true;
            }

            int firstSide = random.Next(0, 2) == 0 ? -1 : 1;

            if (definition.CanMoveVertical && TryMove(grid, x, y, 0, definition.VerticalDirection, definition))
            {
                return true;
            }

            if (definition.CanMoveDiagonal)
            {
                // Sand and other powders randomly choose left-down or right-down first.
                if (TryMove(grid, x, y, firstSide, definition.VerticalDirection, definition))
                {
                    return true;
                }

                if (TryMove(grid, x, y, -firstSide, definition.VerticalDirection, definition))
                {
                    return true;
                }
            }

            if (definition.CanMoveHorizontal && random.NextDouble() <= definition.LateralProbability)
            {
                // Water randomly chooses left or right first, avoiding a visible scanning bias.
                if (TryHorizontalSpread(grid, x, y, firstSide, definition))
                {
                    return true;
                }

                if (TryHorizontalSpread(grid, x, y, -firstSide, definition))
                {
                    return true;
                }
            }

            pixel.FallingFrames = 0;
            MarkUpdated(grid, x, y, pixel);
            return true;
        }

        private bool TryHorizontalSpread(PixelGrid grid, int x, int y, int direction, MaterialDefinition definition)
        {
            int maxDistance = Mathf.Max(1, definition.HorizontalSearchDistance);
            for (int distance = 1; distance <= maxDistance; distance++)
            {
                if (TryMove(grid, x, y, direction * distance, 0, definition))
                {
                    return true;
                }

                int checkX = x + direction * distance;
                if (!grid.InBounds(checkX, y))
                {
                    return false;
                }

                MaterialDefinition checkedDefinition = MaterialDatabase.Get(grid.GetCell(checkX, y).MaterialType);
                if (!checkedDefinition.IsAir && checkedDefinition.Type != definition.Type)
                {
                    return false;
                }
            }

            return false;
        }

        private bool TryMove(PixelGrid grid, int fromX, int fromY, int offsetX, int offsetY, MaterialDefinition definition)
        {
            int toX = fromX + offsetX;
            int toY = fromY + offsetY;

            if (!grid.InBounds(toX, toY))
            {
                if (toY >= grid.Height && definition.MovementMode == PixelMovementMode.Gas)
                {
                    return false;
                }

                grid.SetCell(fromX, fromY, Pixel.FromMaterial(MaterialType.Air));
                grid.MarkChanged(fromX, fromY);
                return true;
            }

            Pixel source = grid.GetCell(fromX, fromY);
            Pixel target = grid.GetCell(toX, toY);
            MaterialDefinition targetDefinition = MaterialDatabase.Get(target.MaterialType);

            if (targetDefinition.IsAir)
            {
                source.UpdatedThisFrame = true;
                source.FallingFrames = offsetY == -1 ? Math.Min(source.FallingFrames + 1, 64) : 0;
                grid.SetCell(toX, toY, source);
                grid.SetCell(fromX, fromY, Pixel.FromMaterial(MaterialType.Air));
                grid.MarkChanged(fromX, fromY);
                grid.MarkChanged(toX, toY);
                return true;
            }

            if (!targetDefinition.CanBeDisplaced || !CanDisplace(definition, targetDefinition, offsetY))
            {
                return false;
            }

            source.UpdatedThisFrame = true;
            target.UpdatedThisFrame = true;
            source.FallingFrames = offsetY == -1 ? Math.Min(source.FallingFrames + 1, 64) : 0;
            target.FallingFrames = 0;
            grid.SetCell(toX, toY, source);
            grid.SetCell(fromX, fromY, target);
            grid.MarkChanged(fromX, fromY);
            grid.MarkChanged(toX, toY);
            return true;
        }

        private static bool CanDisplace(MaterialDefinition source, MaterialDefinition target, int offsetY)
        {
            if (offsetY < 0)
            {
                return source.Density > target.Density;
            }

            if (offsetY > 0)
            {
                return source.Density < target.Density;
            }

            return Math.Abs(source.Density - target.Density) >= 8
                && source.MovementMode != PixelMovementMode.Powder;
        }

        private static void MarkUpdated(PixelGrid grid, int x, int y, Pixel pixel)
        {
            pixel.UpdatedThisFrame = true;
            grid.SetCell(x, y, pixel);
        }
    }
}
