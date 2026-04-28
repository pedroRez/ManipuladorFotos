using System.IO;
using ManipuladorFotos.Infrastructure;

namespace ManipuladorFotos.Models;

public sealed class MediaItem : ObservableObject
{
    private bool _isMarked;
    private DateTime? _originalTakenTime;
    private int? _width;
    private int? _height;

    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime CreationTime { get; init; }
    public required DateTime LastWriteTime { get; init; }
    public DateTime? OriginalTakenTime
    {
        get => _originalTakenTime;
        set => SetProperty(ref _originalTakenTime, value);
    }
    public required MediaKind Kind { get; init; }
    public int? Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }
    public int? Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public string SizeLabel => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024L * 1024L * 1024L => $"{SizeBytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{SizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };

    public bool IsImage => Kind == MediaKind.Foto;
    public DateTime PrimaryPhotoDate => OriginalTakenTime ?? CreationTime;
    public long ResolutionPixels => (long)(Width ?? 0) * (Height ?? 0);
    public string ResolutionLabel => Width.HasValue && Height.HasValue ? $"{Width}x{Height}" : "-";

    public bool IsMarked
    {
        get => _isMarked;
        set => SetProperty(ref _isMarked, value);
    }
}
