using System.IO;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class FileScannerService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".heic"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v"
    };

    public List<MediaItem> Scan(string folderPath, bool includeSubfolders)
    {
        var result = new List<MediaItem>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return result;
        }

        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*.*", searchOption);
        }
        catch
        {
            return result;
        }

        foreach (var path in files)
        {
            try
            {
                var info = new FileInfo(path);
                var ext = info.Extension;
                var kind = ResolveKind(ext);
                var (width, height) = kind == MediaKind.Foto ? TryReadImageDimensions(info.FullName) : ((int?)null, (int?)null);

                result.Add(new MediaItem
                {
                    FullPath = info.FullName,
                    Name = Path.GetFileNameWithoutExtension(info.Name),
                    Extension = ext,
                    SizeBytes = info.Length,
                    CreationTime = info.CreationTime,
                    LastWriteTime = info.LastWriteTime,
                    Kind = kind,
                    Width = width,
                    Height = height
                });
            }
            catch
            {
                // Ignora arquivos com acesso negado/erro de leitura.
            }
        }

        return result;
    }

    public List<MediaItem> ScanUnwantedByExtensions(string folderPath, bool includeSubfolders, IEnumerable<string> extensions)
    {
        var normalized = extensions
            .Select(NormalizeExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return [];
        }

        var all = Scan(folderPath, includeSubfolders);
        return all.Where(x => normalized.Contains(x.Extension)).ToList();
    }

    private static string NormalizeExtension(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }

    private static MediaKind ResolveKind(string extension)
    {
        if (ImageExtensions.Contains(extension))
        {
            return MediaKind.Foto;
        }

        if (VideoExtensions.Contains(extension))
        {
            return MediaKind.Video;
        }

        return MediaKind.Outro;
    }

    private static (int? Width, int? Height) TryReadImageDimensions(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                return (null, null);
            }

            return (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (null, null);
        }
    }
}
