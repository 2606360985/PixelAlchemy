using System;
using UnityEngine;

namespace NoitaCA
{
    [Serializable]
    public sealed class StressTestConfig
    {
        [Header("Grid")]
        public int gridWidth = 384;
        public int gridHeight = 216;
        public int pixelsPerUnit = 16;

        [Header("Water Tank")]
        public int initialWaterWidth = 112;
        public int initialWaterHeight = 78;
        public bool autoStart;
        public float gateOpenDelay = 1.25f;

        [Header("Simulation Budget")]
        [Tooltip("0 means unlimited. Use a smaller value to demonstrate visible budget throttling.")]
        public int maxSimulatedPixelsPerStep;
        public int simulationStepsPerFrame = 1;
        public float simulationStepInterval = 0.016f;

        [Header("Optimization Demo")]
        public PixelSimulationMode initialMode = PixelSimulationMode.FullScan;
        public bool enableChunkUpdates = true;
        public bool enableActiveRegionOptimization = true;
        public int chunkSize = 16;
        public int chunkSleepDelay = 2;

        [Header("Debug Display")]
        public bool showPerformancePanel = true;
        public bool showChunkBoundaries;
        public bool showActivePixelCount = true;
        public bool showActiveRegions;

        [Header("Camera")]
        public float cameraPadding = 0.65f;
    }
}
