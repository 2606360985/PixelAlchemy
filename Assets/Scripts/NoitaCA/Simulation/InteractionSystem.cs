using UnityEngine;

namespace NoitaCA
{
    public sealed class InteractionSystem
    {
        private static readonly Vector2Int[] NeighborOffsets =
        {
            new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
            new Vector2Int(-1, 0),                         new Vector2Int(1, 0),
            new Vector2Int(-1, 1),  new Vector2Int(0, 1),  new Vector2Int(1, 1)
        };

        private readonly System.Random random;

        public InteractionSystem(System.Random random)
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
            // Naive cellular automata interaction pass: every pixel is checked every step.
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!StepCell(grid, x, y, maxPixelsPerStep))
                    {
                        return;
                    }
                }
            }
        }

        private void StepActivePixels(PixelGrid grid, int maxPixelsPerStep)
        {
            // Active Pixel optimization: interaction is also skipped for sleeping pixels.
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
            // Chunk / dirty-region optimization: this is intentionally simple for teaching.
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
                    for (int x = minX; x < maxX; x++)
                    {
                        if (!StepCell(grid, x, y, maxPixelsPerStep))
                        {
                            return;
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
            MaterialDefinition definition = MaterialDatabase.Get(pixel.MaterialType);
            bool changed = false;

            if (definition.ConsumesLifetime)
            {
                pixel.Lifetime -= definition.LifetimeDecay;
                changed = true;
                if (pixel.Lifetime <= 0)
                {
                    MaterialType decay = pixel.DecayMaterial == MaterialType.Air
                        ? definition.BurnoutMaterial
                        : pixel.DecayMaterial;
                    grid.SetMaterial(x, y, decay);
                    return true;
                }
            }

            if (definition.HeatEmission > 0f)
            {
                ReleaseHeat(grid, x, y, definition);
                changed = true;
            }

            float oldTemperature = pixel.Temperature;
            Color32 oldColor = pixel.Color;
            CoolTowardAmbient(ref pixel, definition);
            TintVolatilePixel(ref pixel, definition);

            changed = changed
                || !Mathf.Approximately(oldTemperature, pixel.Temperature)
                || !oldColor.Equals(pixel.Color);

            grid.SetCell(x, y, pixel);
            if (changed)
            {
                grid.MarkChanged(x, y);
            }

            return true;
        }

        private void ReleaseHeat(PixelGrid grid, int x, int y, MaterialDefinition heatSource)
        {
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                int nx = x + NeighborOffsets[i].x;
                int ny = y + NeighborOffsets[i].y;
                if (!grid.InBounds(nx, ny))
                {
                    continue;
                }

                Pixel neighbor = grid.GetCell(nx, ny);
                MaterialDefinition neighborDefinition = MaterialDatabase.Get(neighbor.MaterialType);
                if (neighborDefinition.IsAir)
                {
                    continue;
                }

                neighbor.Temperature += heatSource.HeatEmission;

                if (neighborDefinition.IsFlammable
                    && neighbor.Temperature >= neighborDefinition.IgniteTemperature
                    && random.NextDouble() <= neighborDefinition.Flammability)
                {
                    Ignite(grid, nx, ny, neighborDefinition);
                }
                else
                {
                    grid.SetCell(nx, ny, neighbor);
                    grid.MarkChanged(nx, ny);
                }
            }
        }

        private void Ignite(PixelGrid grid, int x, int y, MaterialDefinition source)
        {
            MaterialDefinition fire = MaterialDatabase.Get(source.BurnMaterial);
            MaterialType decayMaterial = random.NextDouble() <= source.AlternateBurnoutChance
                ? source.AlternateBurnoutMaterial
                : source.BurnoutMaterial;
            int lifetime = random.Next(source.BurnLifetimeMin, source.BurnLifetimeMax + 1);
            grid.SetCell(x, y, MaterialDatabase.CreateBurningPixel(source, fire, decayMaterial, lifetime));
            grid.MarkChanged(x, y);
        }

        private static void CoolTowardAmbient(ref Pixel pixel, MaterialDefinition definition)
        {
            if (definition.HeatEmission > 0f)
            {
                return;
            }

            pixel.Temperature = Mathf.MoveTowards(pixel.Temperature, MaterialDatabase.Ambient, 3f);
        }

        private static void TintVolatilePixel(ref Pixel pixel, MaterialDefinition definition)
        {
            if (!definition.ConsumesLifetime || definition.StartLifetime <= 0)
            {
                return;
            }

            float life01 = Mathf.Clamp01(pixel.Lifetime / (float)definition.StartLifetime);
            if (definition.Type == MaterialType.Fire)
            {
                pixel.Color = (Color32)Color.Lerp(new Color32(120, 26, 12, 255), definition.Color, life01);
            }
            else if (definition.Type == MaterialType.Smoke)
            {
                Color32 faded = definition.Color;
                faded.a = (byte)Mathf.RoundToInt(Mathf.Lerp(35f, definition.Color.a, life01));
                pixel.Color = faded;
            }
        }
    }
}
