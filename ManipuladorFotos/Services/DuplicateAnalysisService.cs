using System.IO;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class DuplicateAnalysisService
{
    private readonly ConcurrentDictionary<string, string> _sha256Cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ulong> _dHashCache = new(StringComparer.OrdinalIgnoreCase);

    public List<DeletionCandidate> BuildDeletionCandidates(
        IReadOnlyCollection<MediaItem> items,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken = default,
        IProgress<AnalysisProgressInfo>? progress = null)
    {
        var candidates = new Dictionary<string, CandidateAccumulator>(StringComparer.OrdinalIgnoreCase);
        var totalStages = 1;
        if (options.UseSameNameRule)
        {
            totalStages++;
        }

        if (options.UseSameSizeRule)
        {
            totalStages++;
        }

        if (options.UseSimilarInSequenceRule)
        {
            totalStages++;
        }

        var stageIndex = 1;
        AddExactHashSuggestions(
            items,
            candidates,
            options,
            cancellationToken,
            CreateProgressReporter(progress, "Analisando hash exato", stageIndex, totalStages));
        cancellationToken.ThrowIfCancellationRequested();
        stageIndex++;

        if (options.UseSameNameRule)
        {
            AddSameNameSuggestions(
                items,
                candidates,
                options,
                cancellationToken,
                CreateProgressReporter(progress, "Analisando mesmo nome", stageIndex, totalStages));
            cancellationToken.ThrowIfCancellationRequested();
            stageIndex++;
        }

        if (options.UseSameSizeRule)
        {
            AddSameSizeSuggestions(
                items,
                candidates,
                options,
                cancellationToken,
                CreateProgressReporter(progress, "Analisando mesmo tamanho", stageIndex, totalStages));
            cancellationToken.ThrowIfCancellationRequested();
            stageIndex++;
        }

        if (options.UseSimilarInSequenceRule)
        {
            AddSimilarSequenceSuggestions(
                items,
                candidates,
                options,
                cancellationToken,
                CreateProgressReporter(progress, "Analisando fotos semelhantes", stageIndex, totalStages));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return candidates.Values
            .Select(ToDeletionCandidate)
            .OrderBy(x => x.GroupLabel)
            .ThenBy(x => x.CanDelete) // original protegida primeiro no grupo
            .ThenByDescending(x => x.Item.PrimaryPhotoDate)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static Action<int, int> CreateProgressReporter(
        IProgress<AnalysisProgressInfo>? progress,
        string stage,
        int stageIndex,
        int totalStages)
    {
        return (processed, total) =>
        {
            progress?.Report(new AnalysisProgressInfo(stage, stageIndex, totalStages, processed, total));
        };
    }

    private void AddExactHashSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress)
    {
        var photos = items
            .Where(x => x.IsImage && File.Exists(x.FullPath))
            .ToList();
        if (photos.Count == 0)
        {
            reportProgress?.Invoke(1, 1);
            return;
        }

        var processedHashes = 0;
        var hashTotal = photos.Count;
        reportProgress?.Invoke(0, hashTotal);
        Parallel.ForEach(
            photos,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            },
            photo =>
            {
                _ = GetSha256(photo, cancellationToken);
                var processed = Interlocked.Increment(ref processedHashes);
                if (processed == hashTotal || processed % 64 == 0)
                {
                    reportProgress?.Invoke(processed, hashTotal);
                }
            });

        var groups = photos
            .GroupBy(x => GetSha256(x, cancellationToken))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .ToList();

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

        reportProgress?.Invoke(hashTotal, hashTotal);
    }

    private void AddSameNameSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress)
    {
        var groups = items
            .Where(x => x.IsImage)
            .GroupBy(x => $"{x.Name}|{x.Extension}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        var total = Math.Max(1, groups.Count);
        var processed = 0;
        reportProgress?.Invoke(0, total);

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

            processed++;
            if (processed == total || processed % 32 == 0)
            {
                reportProgress?.Invoke(processed, total);
            }
        }

        reportProgress?.Invoke(total, total);
    }

    private void AddSameSizeSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress)
    {
        var groups = items
            .Where(x => x.IsImage && x.SizeBytes > 0)
            .GroupBy(x => x.SizeBytes)
            .Where(g => g.Count() > 1)
            .ToList();

        var total = Math.Max(1, groups.Count);
        var processed = 0;
        reportProgress?.Invoke(0, total);

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

            processed++;
            if (processed == total || processed % 32 == 0)
            {
                reportProgress?.Invoke(processed, total);
            }
        }

        reportProgress?.Invoke(total, total);
    }

    private void AddSimilarSequenceSuggestions(
        IReadOnlyCollection<MediaItem> items,
        IDictionary<string, CandidateAccumulator> candidates,
        DuplicateAnalysisOptions options,
        CancellationToken cancellationToken,
        Action<int, int>? reportProgress)
    {
        var photos = items
            .Where(x => x.IsImage && File.Exists(x.FullPath))
            .OrderBy(x => x.PrimaryPhotoDate)
            .ToList();
        if (photos.Count < 2)
        {
            reportProgress?.Invoke(1, 1);
            return;
        }

        // Pré-cálculo paralelo para aproveitar melhor CPU antes de comparar pares.
        var hashProcessed = 0;
        var hashTotal = photos.Count;
        var estimatedPairCount = EstimatePairComparisons(photos, options.SimilarSecondsWindow);
        var comparisonTotal = Math.Max(1, estimatedPairCount);
        var totalUnits = hashTotal + comparisonTotal;
        reportProgress?.Invoke(0, totalUnits);
        Parallel.ForEach(
            photos,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            },
            photo =>
            {
                _ = GetPerceptualHash(photo, cancellationToken);
                var processed = Interlocked.Increment(ref hashProcessed);
                if (processed == hashTotal || processed % 64 == 0)
                {
                    reportProgress?.Invoke(processed, totalUnits);
                }
            });
        var comparedPairs = 0;

        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var minDistanceByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in photos)
        {
            adjacency[p.FullPath] = [];
        }

        for (var i = 0; i < photos.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var left = photos[i];
            var leftHash = GetPerceptualHash(left, cancellationToken);
            if (leftHash == 0)
            {
                continue;
            }

            for (var j = i + 1; j < photos.Count; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var right = photos[j];
                var delta = (right.PrimaryPhotoDate - left.PrimaryPhotoDate).TotalSeconds;
                if (delta > options.SimilarSecondsWindow)
                {
                    break;
                }

                var rightHash = GetPerceptualHash(right, cancellationToken);
                if (rightHash == 0)
                {
                    comparedPairs++;
                    if (comparedPairs == comparisonTotal || comparedPairs % 128 == 0)
                    {
                        reportProgress?.Invoke(hashTotal + Math.Min(comparedPairs, comparisonTotal), totalUnits);
                    }
                    continue;
                }

                var distance = HammingDistance(leftHash, rightHash);
                if (distance <= options.SimilarDistanceThreshold)
                {
                    adjacency[left.FullPath].Add(right.FullPath);
                    adjacency[right.FullPath].Add(left.FullPath);
                    RegisterMinDistance(minDistanceByItem, left.FullPath, distance);
                    RegisterMinDistance(minDistanceByItem, right.FullPath, distance);
                }

                comparedPairs++;
                if (comparedPairs == comparisonTotal || comparedPairs % 128 == 0)
                {
                    reportProgress?.Invoke(hashTotal + Math.Min(comparedPairs, comparisonTotal), totalUnits);
                }
            }
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var photoByPath = photos.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);
        var groupIndex = 1;

        foreach (var photo in photos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visited.Contains(photo.FullPath))
            {
                continue;
            }

            var componentPaths = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(photo.FullPath);
            visited.Add(photo.FullPath);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                componentPaths.Add(current);
                foreach (var neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (componentPaths.Count <= 1)
            {
                continue;
            }

            var componentItems = componentPaths.Select(path => photoByPath[path]).ToList();
            var keeper = componentItems.OrderByDescending(x => ScoreForKeep(x, options)).First();
            var groupLabel = $"Semelhante:{groupIndex:D4}";
            groupIndex++;

            AddKeeper(
                candidates,
                keeper,
                groupLabel,
                "Semelhante",
                $"Foto base protegida do conjunto de similaridade ({componentItems.Count} itens).",
                GetMinDistance(minDistanceByItem, keeper.FullPath));

            foreach (var duplicate in componentItems.Where(x => !x.FullPath.Equals(keeper.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                AddDuplicate(
                    candidates,
                    duplicate,
                    keeper.FullPath,
                    groupLabel,
                    "Semelhante",
                    "Foto semelhante no mesmo conjunto de sequência.",
                    GetMinDistance(minDistanceByItem, duplicate.FullPath));
            }
        }

        reportProgress?.Invoke(totalUnits, totalUnits);
    }

    private static double ScoreForKeep(MediaItem item, DuplicateAnalysisOptions options)
    {
        return options.KeepPreference switch
        {
            "Maior tamanho" => item.SizeBytes,
            "Mais recente" => item.LastWriteTime.Ticks,
            "Mais antiga" => -item.PrimaryPhotoDate.Ticks,
            _ => item.ResolutionPixels * 1_000_000d + item.SizeBytes
        };
    }

    private static void AddDuplicate(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string keepFilePath,
        string groupLabel,
        string rule,
        string reason,
        int? similarityDistance = null)
    {
        var acc = GetOrCreate(candidates, item, keepFilePath);
        acc.Rules.Add(rule);
        acc.Reasons.Add(reason);
        acc.GroupLabels.Add(groupLabel);
        acc.UpdateSimilarityDistance(similarityDistance);
    }

    private static void AddKeeper(
        IDictionary<string, CandidateAccumulator> candidates,
        MediaItem item,
        string groupLabel,
        string rule,
        string reason,
        int? similarityDistance = null)
    {
        var acc = GetOrCreate(candidates, item, item.FullPath);
        acc.CanDelete = false;
        acc.Rules.Add($"{rule}-Original");
        acc.Reasons.Add(reason);
        acc.GroupLabels.Add(groupLabel);
        acc.KeepFilePath = item.FullPath;
        acc.UpdateSimilarityDistance(similarityDistance);
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
            CanDelete = x.CanDelete,
            SimilarityPercent = x.BestSimilarityDistance.HasValue
                ? Math.Clamp((int)Math.Round((1d - (x.BestSimilarityDistance.Value / 64d)) * 100d), 0, 100)
                : null
        };

        candidate.IsMarked = candidate.CanDelete;
        return candidate;
    }

    private static int EstimatePairComparisons(IReadOnlyList<MediaItem> photos, int windowSeconds)
    {
        if (photos.Count < 2)
        {
            return 0;
        }

        long totalPairs = 0;
        var end = 0;
        for (var i = 0; i < photos.Count; i++)
        {
            if (end < i + 1)
            {
                end = i + 1;
            }

            while (end < photos.Count &&
                   (photos[end].PrimaryPhotoDate - photos[i].PrimaryPhotoDate).TotalSeconds <= windowSeconds)
            {
                end++;
            }

            totalPairs += end - i - 1;
        }

        return totalPairs > int.MaxValue ? int.MaxValue : (int)totalPairs;
    }

    private string GetSha256(MediaItem item, CancellationToken cancellationToken)
    {
        if (_sha256Cache.TryGetValue(item.FullPath, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = File.OpenRead(item.FullPath);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 128];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                sha.AppendData(buffer, 0, read);
            }

            var hash = sha.GetHashAndReset();
            var text = Convert.ToHexString(hash);
            _sha256Cache[item.FullPath] = text;
            return text;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private ulong GetPerceptualHash(MediaItem item, CancellationToken cancellationToken)
    {
        if (_dHashCache.TryGetValue(item.FullPath, out var cached))
        {
            return cached;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException)
        {
            throw;
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

    private static void RegisterMinDistance(IDictionary<string, int> map, string path, int distance)
    {
        if (map.TryGetValue(path, out var current))
        {
            if (distance < current)
            {
                map[path] = distance;
            }
            return;
        }

        map[path] = distance;
    }

    private static int? GetMinDistance(IReadOnlyDictionary<string, int> map, string path)
    {
        return map.TryGetValue(path, out var value) ? value : null;
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
        public int? BestSimilarityDistance { get; private set; }
        public HashSet<string> Rules { get; }
        public List<string> Reasons { get; }
        public HashSet<string> GroupLabels { get; }

        public void UpdateSimilarityDistance(int? distance)
        {
            if (!distance.HasValue)
            {
                return;
            }

            if (!BestSimilarityDistance.HasValue || distance.Value < BestSimilarityDistance.Value)
            {
                BestSimilarityDistance = distance.Value;
            }
        }
    }
}
