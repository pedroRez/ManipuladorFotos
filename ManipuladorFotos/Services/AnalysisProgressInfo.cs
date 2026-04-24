namespace ManipuladorFotos.Services;

public readonly record struct AnalysisProgressInfo(
    string Stage,
    int StageIndex,
    int TotalStages,
    int StageProcessed,
    int StageTotal);
