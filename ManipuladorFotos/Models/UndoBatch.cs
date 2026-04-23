namespace ManipuladorFotos.Models;

public sealed class UndoBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
    public List<UndoEntry> Entries { get; set; } = [];
    public string DisplayLabel => $"{CreatedAt:dd/MM HH:mm} - {Description} ({Entries.Count})";
}

public sealed class UndoEntry
{
    public string OriginalPath { get; set; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
}
