using System.IO;
using ManipuladorFotos.Infrastructure;

namespace ManipuladorFotos.Models;

public sealed class MediaItem : ObservableObject
{
    private bool _isMarked;

    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime CreationTime { get; init; }
    public required DateTime LastWriteTime { get; init; }
    public required MediaKind Kind { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }

    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public string SizeLabel => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024L * 1024L => $"{SizeBytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{SizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };

    public bool IsImage => Kind == MediaKind.Foto;
    public long ResolutionPixels => (long)(Width ?? 0) * (Height ?? 0);
    public string ResolutionLabel => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : "-";

    public bool IsMarked
    {
        get => _isMarked;
        set => SetProperty(ref _isMarked, value);
    }
}
