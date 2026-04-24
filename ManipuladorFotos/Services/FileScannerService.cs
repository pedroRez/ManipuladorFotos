using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class FileScannerService
{
    private const string InternalFolderName = ".manipuladorfotos";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".heic", ".heif"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v"
    };

    public List<MediaItem> Scan(
        string folderPath,
        bool includeSubfolders,
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressInfo>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        List<string> files;
        try
        {
            files = [];
            foreach (var path in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                files.Add(path);
                if (files.Count % 2000 == 0)
                {
                    progress?.Report(new ScanProgressInfo("Descobrindo arquivos", files.Count, 0));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IOException($"Falha ao enumerar arquivos em '{folderPath}': {ex.Message}", ex);
        }

        var total = files.Count;
        if (total == 0)
        {
            progress?.Report(new ScanProgressInfo("Escaneando metadados", 0, 0));
            return [];
        }

        var processed = 0;
        var bag = new ConcurrentBag<MediaItem>();

        Parallel.ForEach(
            files,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            },
            path =>
            {
                try
                {
                    if (!IsInternalAppPath(path))
                    {
                        var info = new FileInfo(path);
                        var ext = info.Extension;
                        var kind = ResolveKind(ext);
                        var (width, height, originalTakenTime) = kind == MediaKind.Foto
                            ? TryReadImageMetadata(info.FullName)
                            : ((int?)null, (int?)null, (DateTime?)null);

                        bag.Add(new MediaItem
                        {
                            FullPath = info.FullName,
                            Name = Path.GetFileNameWithoutExtension(info.Name),
                            Extension = ext,
                            SizeBytes = info.Length,
                            CreationTime = info.CreationTime,
                            LastWriteTime = info.LastWriteTime,
                            OriginalTakenTime = originalTakenTime,
                            Kind = kind,
                            Width = width,
                            Height = height
                        });
                    }
                }
                catch
                {
                    // Ignora arquivos com acesso negado/erro de leitura.
                }
                finally
                {
                    var done = Interlocked.Increment(ref processed);
                    if (done % 200 == 0 || done == total)
                    {
                        progress?.Report(new ScanProgressInfo("Escaneando metadados", done, total));
                    }
                }
            });

        if (processed < total)
        {
            progress?.Report(new ScanProgressInfo("Escaneando metadados", processed, total));
        }

        return bag.ToList();
    }

    public List<MediaItem> ScanUnwantedByExtensions(
        string folderPath,
        bool includeSubfolders,
        IEnumerable<string> extensions,
        CancellationToken cancellationToken = default,
        IProgress<ScanProgressInfo>? progress = null)
    {
        var normalized = extensions
            .Select(NormalizeExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return [];
        }

        var all = Scan(folderPath, includeSubfolders, cancellationToken, progress);
        cancellationToken.ThrowIfCancellationRequested();
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

    private static (int? Width, int? Height, DateTime? OriginalTakenTime) TryReadImageMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null)
            {
                return (null, null, null);
            }

            var originalTaken = TryReadOriginalTakenTime(frame.Metadata as BitmapMetadata);
            return (frame.PixelWidth, frame.PixelHeight, originalTaken);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static DateTime? TryReadOriginalTakenTime(BitmapMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var raw = metadata.DateTaken;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        if (DateTime.TryParseExact(
                raw,
                "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var exifDate))
        {
            return exifDate;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool IsInternalAppPath(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}{InternalFolderName}{Path.DirectorySeparatorChar}";
        return normalized.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }
}
