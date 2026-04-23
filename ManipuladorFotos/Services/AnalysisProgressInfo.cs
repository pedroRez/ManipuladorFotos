namespace ManipuladorFotos.Services;

public readonly record struct AnalysisProgressInfo(int CompletedSteps, int TotalSteps, string Stage);
