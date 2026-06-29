using System;

namespace NoitaCA
{
    [Obsolete("Use PixelSimulation. This wrapper keeps older bootstrap code compiling.")]
    public sealed class SimulationSystem
    {
        private readonly PixelSimulation simulation;

        public float SideFlowProbability { get; set; } = 0.45f;
        public float SplashProbability { get; set; } = 0.55f;
        public int SplashFallThreshold { get; set; } = 5;
        public float PressureFlowProbability { get; set; } = 0.9f;
        public int PressureFlowMaxDistance { get; set; } = 6;

        public SimulationSystem(int seed = 0)
        {
            simulation = new PixelSimulation(seed);
        }

        public void Step(PixelGrid grid)
        {
            simulation.Step(grid);
        }
    }
}
