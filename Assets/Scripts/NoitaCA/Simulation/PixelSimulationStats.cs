namespace NoitaCA
{
    public struct PixelSimulationStats
    {
        public int TotalPixels;
        public int NonAirPixels;
        public int WaterPixels;
        public int ActivePixels;
        public int ActiveChunks;
        public int ProcessedPixels;
        public float SimulationMs;
        public float RenderMs;
        public PixelSimulationMode Mode;
        public bool UseChunkOptimization;
        public bool UseActiveRegionOptimization;
    }
}
