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

    public List<DeletionCandidate> BuildDeletionCandidates(
        IReadOnlyCollection<MediaItem> items,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken = default,
        IProgress<AnalysisProgressInfo>? progress = null)
    {
        var candidates = new Dictionary<string, CandidateAccumulator>(StringComparer.OrdinalIgnoreCase);
        var totalSteps = 1;
        if (options.UseSameNameRule)
        {
            totalSteps++;
        }

        if (options.UseSameSizeRule)
        {
            totalSteps++;
        }

        if (options.UseSimilarInSequenceRule)
        {
            totalSteps++;
        }

        var completedSteps = 0;

        AddExactHashSuggestions(items, candidates, options, cancellationToken);
        completedSteps++;
        progress?.Report(new AnalysisProgressInfo(completedSteps, totalSteps, "Analisando hash exato"));
        cancellationToken.ThrowIfCancellationRequested();

        if (options.UseSameNameRule)
        {
            AddSameNameSuggestions(items, candidates, options, cancellationToken);
            completedSteps++;
            progress?.Report(new AnalysisProgressInfo(completedSteps, totalSteps, "Analisando mesmo nome"));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (options.UseSameSizeRule)
        {
            AddSameSizeSuggestions(items, candidates, options, cancellationToken);
            completedSteps++;
            progress?.Report(new AnalysisProgressInfo(completedSteps, totalSteps, "Analisando mesmo tamanho"));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (options.UseSimilarInSequenceRule)
        {
            AddSimilarSequenceSuggestions(items, candidates, options, cancellationToken);
            completedSteps++;
            progress?.Report(new AnalysisProgressInfo(completedSteps, totalSteps, "Analisando fotos semelhantes"));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return candidates.Values
            .Select(ToDeletionCandidate)
            .OrderBy(x => x.GroupLabel)
            .ThenBy(x => x.CanDelete) // original protegida primeiro no grupo
            .ThenByDescending(x => x.CreationTime)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private void AddExactHashSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var groups = items
            .Where(x => x.IsImage && File.Exists(x.FullPath))
            .GroupBy(GetSha256)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = group.OrderByDescending(x => ScoreForKeep(x, options)).ToList();
            var keeper = ordered.First();
            var groupLabel = $"Hash:{group.Key[..8]}";

            AddKeeper(
                candidates,
                keeper,
                groupLabel,
                "Hash",
                "Foto original mantida para garantir que o grupo não fique sem imagem.");

            foreach (var duplicate in ordered.Skip(1))
            {
                AddDuplicate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    groupLabel,
                    "Hash",
                    "Duplicada por mesmo conteúdo (hash exato).");
            }
        }
    }

    private void AddSameNameSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var groups = items
            .Where(x => x.IsImage)
            .GroupBy(x => $"{x.Name}|{x.Extension}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = group.OrderByDescending(x => ScoreForKeep(x, options)).ToList();
            var keeper = ordered.First();
            var groupLabel = $"Nome:{keeper.Name}{keeper.Extension}";

            AddKeeper(
                candidates,
                keeper,
                groupLabel,
                "Nome",
                "Foto original mantida no grupo por nome para evitar exclusão total.");

            foreach (var duplicate in ordered.Skip(1))
            {
                AddDuplicate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    groupLabel,
                    "Nome",
                    "Possível duplicada por mesmo nome e extensão.");
            }
        }
    }

    private void AddSameSizeSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var groups = items
            .Where(x => x.IsImage && x.SizeBytes > 0)
            .GroupBy(x => x.SizeBytes)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordered = group.OrderByDescending(x => ScoreForKeep(x, options)).ToList();
            var keeper = ordered.First();
            var groupLabel = $"Tam:{keeper.SizeLabel}";

            AddKeeper(
                candidates,
                keeper,
                groupLabel,
                "Tamanho",
                "Foto original mantida no grupo por tamanho para evitar exclusão total.");

            foreach (var duplicate in ordered.Skip(1))
            {
                AddDuplicate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    groupLabel,
                    "Tamanho",
                    "Possível duplicada por mesmo tamanho (revisar com atenção).");
            }
        }
    }

    private void AddSimilarSequenceSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var photos = items
            .Where(x => x.IsImage && File.Exists(x.FullPath))
            .OrderBy(x => x.CreationTime)
            .ToList();

        for (var i = 0; i < photos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var left = photos[i];
            for (var j = i + 1; j < photos.Count; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var right = photos[j];
                var delta = (right.CreationTime - left.CreationTime).TotalSeconds;
                if (delta > options.SimilarSecondsWindow)
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
                if (distance > options.SimilarDistanceThreshold)
                {
                    continue;
                }

                var keeper = ScoreForKeep(left, options) >= ScoreForKeep(right, options) ? left : right;
                var duplicate = keeper == left ? right : left;
                var groupLabel = $"Seq:{keeper.CreationTime:yyyyMMdd-HHmmss}";

                AddKeeper(
                    candidates,
                    keeper,
                    groupLabel,
                    "Semelhante",
                    "Foto base protegida para manter pelo menos uma imagem da sequência.");

                AddDuplicate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    groupLabel,
                    "Semelhante",
                    $"Foto semelhante na sequência ({delta:F0}s de diferença, distância {distance}).");
            }
        }
    }

    private static double ScoreForKeep(MediaItem item, DuplicateAnalysisOptions options)
    {
        return options.KeepPreference switch
        {
            "Maior tamanho" => item.SizeBytes,
            "Mais recente" => item.LastWriteTime.Ticks,
            "Mais antiga" => -item.CreationTime.Ticks,
            _ => item.ResolutionPixels * 1_000_000d + item.SizeBytes
        };
    }

    private static void AddDuplicate(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string keepFilePath,
        string groupLabel,
        string rule,
        string reason)
    {
        var acc = GetOrCreate(candidates, item, keepFilePath);
        acc.Rules.Add(rule);
        acc.Reasons.Add(reason);
        acc.GroupLabels.Add(groupLabel);
    }

    private static void AddKeeper(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string groupLabel,
        string rule,
        string reason)
    {
        var acc = GetOrCreate(candidates, item, item.FullPath);
        acc.CanDelete = false;
        acc.Rules.Add($"{rule}-Original");
        acc.Reasons.Add(reason);
        acc.GroupLabels.Add(groupLabel);
        acc.KeepFilePath = item.FullPath;
    }

    private static CandidateAccumulator GetOrCreate(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string keepFilePath)
    {
        if (!candidates.TryGetValue(item.FullPath, out var existing))
        {
            existing = new CandidateAccumulator(item, keepFilePath);
            candidates[item.FullPath] = existing;
        }

        return existing;
    }

    private static DeletionCandidate ToDeletionCandidate(CandidateAccumulator x)
    {
        var candidate = new DeletionCandidate
        {
            Item = x.Item,
            Rule = string.Join(" | ", x.Rules.OrderBy(r => r)),
            Reason = string.Join("; ", x.Reasons.Distinct()),
            KeepFilePath = x.KeepFilePath,
            GroupLabel = string.Join(" | ", x.GroupLabels.OrderBy(g => g)),
            CanDelete = x.CanDelete
        };

        candidate.IsMarked = candidate.CanDelete;
        return candidate;
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
        public CandidateAccumulator(MediaItem item, string keepFilePath)
        {
            Item = item;
            KeepFilePath = keepFilePath;
            CanDelete = true;
            Rules = [];
            Reasons = [];
            GroupLabels = [];
        }

        public MediaItem Item { get; }
        public string KeepFilePath { get; set; }
        public bool CanDelete { get; set; }
        public HashSet<string> Rules { get; }
        public List<string> Reasons { get; }
        public HashSet<string> GroupLabels { get; }
    }
}
