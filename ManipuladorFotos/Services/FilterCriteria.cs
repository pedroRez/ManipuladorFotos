namespace ManipuladorFotos.Services;

public sealed class FilterCriteria
{
    public string NameContains { get; init; } = string.Empty;
    public string ExtensionContains { get; init; } = string.Empty;
    public string TypeFilter { get; init; } = "Todos";
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public DateTime? ModifiedFrom { get; init; }
    public DateTime? ModifiedTo { get; init; }
    public long? MinSizeBytes { get; init; }
    public long? MaxSizeBytes { get; init; }
}
