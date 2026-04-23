using ManipuladorFotos.Infrastructure;

namespace ManipuladorFotos.Models;

public sealed class DeletionCandidate : ObservableObject
{
    private bool _isMarked = true;

    public required MediaItem Item { get; init; }
    public required string Reason { get; init; }
    public required string Rule { get; init; }
    public required string KeepFilePath { get; init; }

    public string Name => Item.Name;
    public string Extension => Item.Extension;
    public string FullPath => Item.FullPath;
    public string SizeLabel => Item.SizeLabel;
    public DateTime CreationTime => Item.CreationTime;
    public string Kind => Item.Kind.ToString();

    public bool IsMarked
    {
        get => _isMarked;
        set => SetProperty(ref _isMarked, value);
    }
}
