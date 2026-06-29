using System.Diagnostics;

namespace NoitaCA
{
    public sealed class PixelSimulation
    {
        private readonly MovementSystem movementSystem;
        private readonly InteractionSystem interactionSystem;
        private readonly Stopwatch stopwatch = new Stopwatch();

        public PixelSimulationMode Mode { get; set; } = PixelSimulationMode.FullScan;
        public int MaxProcessedPixelsPerStep { get; set; }
        public PixelSimulationStats LastStats { get; private set; }

        public PixelSimulation(int seed = 0)
        {
            System.Random random = seed == 0 ? new System.Random() : new System.Random(seed);
            movementSystem = new MovementSystem(random);
            interactionSystem = new InteractionSystem(random);
        }

        public void Step(PixelGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            stopwatch.Reset();
            stopwatch.Start();

            grid.BeginSimulationStep();
            grid.ClearUpdatedFlags(Mode);

            // Both passes are intentionally kept separate for teaching:
            // movement is density/flow only, interaction is heat/lifetime/material conversion only.
            movementSystem.Step(grid, Mode, MaxProcessedPixelsPerStep);
            interactionSystem.Step(grid, Mode, MaxProcessedPixelsPerStep);

            grid.EndSimulationStep(Mode);

            stopwatch.Stop();
            PixelSimulationStats stats = LastStats;
            stats.TotalPixels = grid.Width * grid.Height;
            stats.ActivePixels = Mode == PixelSimulationMode.ChunkBased
                ? grid.ActiveChunkCount * grid.ChunkSize * grid.ChunkSize
                : grid.ActivePixelCount;
            stats.ActiveChunks = grid.ActiveChunkCount;
            stats.ProcessedPixels = grid.ProcessedPixelsThisStep;
            stats.SimulationMs = (float)stopwatch.Elapsed.TotalMilliseconds;
            stats.Mode = Mode;
            stats.UseChunkOptimization = Mode == PixelSimulationMode.ChunkBased;
            stats.UseActiveRegionOptimization = Mode == PixelSimulationMode.ActivePixels;
            LastStats = stats;
        }

        public void SetRenderTime(float renderMs)
        {
            PixelSimulationStats stats = LastStats;
            stats.RenderMs = renderMs;
            LastStats = stats;
        }

        public void SetMaterialCounts(int nonAirPixels, int waterPixels)
        {
            PixelSimulationStats stats = LastStats;
            stats.NonAirPixels = nonAirPixels;
            stats.WaterPixels = waterPixels;
            LastStats = stats;
        }
    }
}
