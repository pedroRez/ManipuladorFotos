namespace ManipuladorFotos.Services;

public sealed class DuplicateAnalysisOptions
{
    public bool UseSameNameRule { get; init; }
    public bool UseSameSizeRule { get; init; }
    public bool UseSimilarInSequenceRule { get; init; }
    public int SimilarSecondsWindow { get; init; } = 10;
    public int SimilarDistanceThreshold { get; init; } = 8;
}
