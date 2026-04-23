using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class DuplicateAnalysisService
{
    private readonly Dictionary<string, string> _sha256Cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ulong> _dHashCache = new(StringComparer.OrdinalIgnoreCase);

    public List<DeletionCandidate> BuildDeletionCandidates(IReadOnlyCollection<MediaItem> items, DuplicateAnalysisOptions options)
    {
        var candidates = new Dictionary<string, CandidateAccumulator>(StringComparer.OrdinalIgnoreCase);

        AddExactHashSuggestions(items, candidates);

        if (options.UseSameNameRule)
        {
            AddSameNameSuggestions(items, candidates);
        }

        if (options.UseSameSizeRule)
        {
            AddSameSizeSuggestions(items, candidates);
        }

        if (options.UseSimilarInSequenceRule)
        {
            AddSimilarSequenceSuggestions(items, candidates, options.SimilarSecondsWindow, options.SimilarDistanceThreshold);
        }

        return candidates.Values
            .Select(x => new DeletionCandidate
            {
                Item = x.Item,
                Rule = string.Join(" | ", x.Rules.OrderBy(r => r)),
                Reason = string.Join("; ", x.Reasons),
                KeepFilePath = x.KeepFilePath
            })
            .OrderByDescending(x => x.CreationTime)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private void AddExactHashSuggestions(IReadOnlyCollection<MediaItem> items, IDictionary<string, CandidateAccumulator> candidates)
    {
        var groups = items
            .Where(x => File.Exists(x.FullPath))
            .GroupBy(GetSha256)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(ScoreKeeper).ToList();
            var keeper = ordered.First();
            foreach (var duplicate in ordered.Skip(1))
            {
                AddCandidate(candidates, duplicate, keeper.FullPath, "Hash", "Duplicada por mesmo conteúdo (hash exato).");
            }
        }
    }

    private void AddSameNameSuggestions(IReadOnlyCollection<MediaItem> items, IDictionary<string, CandidateAccumulator> candidates)
    {
        var groups = items
            .GroupBy(x => $"{x.Name}|{x.Extension}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(ScoreKeeper).ToList();
            var keeper = ordered.First();
            foreach (var duplicate in ordered.Skip(1))
            {
                AddCandidate(candidates, duplicate, keeper.FullPath, "Nome", "Possível duplicada por mesmo nome e extensão.");
            }
        }
    }

    private void AddSameSizeSuggestions(IReadOnlyCollection<MediaItem> items, IDictionary<string, CandidateAccumulator> candidates)
    {
        var groups = items
            .Where(x => x.SizeBytes > 0)
            .GroupBy(x => x.SizeBytes)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(ScoreKeeper).ToList();
            var keeper = ordered.First();
            foreach (var duplicate in ordered.Skip(1))
            {
                AddCandidate(candidates, duplicate, keeper.FullPath, "Tamanho", "Possível duplicada por mesmo tamanho (revisar com atenção).");
            }
        }
    }

    private void AddSimilarSequenceSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        int secondsWindow,
        int distanceThreshold)
    {
        var photos = items
            .Where(x => x.IsImage && File.Exists(x.FullPath))
            .OrderBy(x => x.CreationTime)
            .ToList();

        for (var i = 0; i < photos.Count; i++)
        {
            var left = photos[i];
            for (var j = i + 1; j < photos.Count; j++)
            {
                var right = photos[j];
                var delta = (right.CreationTime - left.CreationTime).TotalSeconds;
                if (delta > secondsWindow)
                {
                    break;
                }

                var leftHash = GetPerceptualHash(left);
                var rightHash = GetPerceptualHash(right);
                if (leftHash == 0 || rightHash == 0)
                {
                    continue;
                }

                var distance = HammingDistance(leftHash, rightHash);
                if (distance > distanceThreshold)
                {
                    continue;
                }

                var keeper = ScoreKeeper(left) >= ScoreKeeper(right) ? left : right;
                var duplicate = keeper == left ? right : left;
                AddCandidate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    "Semelhante",
                    $"Foto semelhante na sequência ({delta:F0}s de diferença, distância {distance}).");
            }
        }
    }

    private static double ScoreKeeper(MediaItem item)
    {
        return item.SizeBytes + item.LastWriteTime.Ticks * 0.0000001;
    }

    private static void AddCandidate(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string keepFilePath,
        string rule,
        string reason)
    {
        if (!candidates.TryGetValue(item.FullPath, out var existing))
        {
            candidates[item.FullPath] = new CandidateAccumulator(item, keepFilePath, rule, reason);
            return;
        }

        existing.Rules.Add(rule);
        existing.Reasons.Add(reason);
    }

    private string GetSha256(MediaItem item)
    {
        if (_sha256Cache.TryGetValue(item.FullPath, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = File.OpenRead(item.FullPath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            var text = Convert.ToHexString(hash);
            _sha256Cache[item.FullPath] = text;
            return text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private ulong GetPerceptualHash(MediaItem item)
    {
        if (_dHashCache.TryGetValue(item.FullPath, out var cached))
        {
            return cached;
        }

        try
        {
            using var fs = File.OpenRead(item.FullPath);
            var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            var scaled = new TransformedBitmap(frame, new ScaleTransform(9.0 / frame.PixelWidth, 8.0 / frame.PixelHeight));
            var gray = new FormatConvertedBitmap(scaled, PixelFormats.Gray8, null, 0);
            var stride = 9;
            var pixels = new byte[stride * 8];
            gray.CopyPixels(pixels, stride, 0);

            ulong hash = 0;
            var bitIndex = 0;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var left = pixels[y * stride + x];
                    var right = pixels[y * stride + x + 1];
                    if (left > right)
                    {
                        hash |= 1UL << bitIndex;
                    }

                    bitIndex++;
                }
            }

            _dHashCache[item.FullPath] = hash;
            return hash;
        }
        catch
        {
            return 0;
        }
    }

    private static int HammingDistance(ulong left, ulong right)
    {
        var value = left ^ right;
        var count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    private sealed class CandidateAccumulator
    {
        public CandidateAccumulator(MediaItem item, string keepFilePath, string rule, string reason)
        {
            Item = item;
            KeepFilePath = keepFilePath;
            Rules = [rule];
            Reasons = [reason];
        }

        public MediaItem Item { get; }
        public string KeepFilePath { get; }
        public HashSet<string> Rules { get; }
        public List<string> Reasons { get; }
    }
}
