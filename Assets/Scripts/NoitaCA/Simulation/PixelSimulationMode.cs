namespace NoitaCA
{
    public enum PixelSimulationMode
    {
        // Naive cellular automata: scan the whole grid every simulation step.
        FullScan,
        // Teaching version of Noita-style "only wake pixels near change" thinking.
        ActivePixels,
        // Dirty-region version: wake 16x16 / 32x32 regions instead of individual pixels.
        ChunkBased
    }
}
