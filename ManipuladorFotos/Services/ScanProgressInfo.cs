namespace ManipuladorFotos.Services;

public readonly record struct ScanProgressInfo(string Stage, int Processed, int Total);
