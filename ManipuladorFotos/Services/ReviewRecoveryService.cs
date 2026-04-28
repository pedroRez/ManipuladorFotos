using System.IO;
using System.Text.Json;

namespace ManipuladorFotos.Services;

public sealed class ReviewRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _recoveryDir;
    private readonly string _recoveryFile;

    public ReviewRecoveryService()
    {
        _recoveryDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManipuladorFotos",
            "recovery");
        _recoveryFile = Path.Combine(_recoveryDir, "review-state.json");
    }

    public ReviewRecoverySnapshot? Load()
    {
        try
        {
            if (!File.Exists(_recoveryFile))
            {
                return null;
            }

            var json = File.ReadAllText(_recoveryFile);
            return JsonSerializer.Deserialize<ReviewRecoverySnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ReviewRecoverySnapshot snapshot)
    {
        Directory.CreateDirectory(_recoveryDir);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        File.WriteAllText(_recoveryFile, json);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_recoveryFile))
            {
                File.Delete(_recoveryFile);
            }
        }
        catch
        {
            // Ignora falha de limpeza de snapshot.
        }
    }
}

public sealed class ReviewRecoverySnapshot
{
    public string Folder { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsFocusedReviewMode { get; set; }
    public bool IsListReviewMode { get; set; }
    public string? SelectedCandidatePath { get; set; }
    public List<ReviewRecoveryCandidate> Candidates { get; set; } = [];
}

public sealed class ReviewRecoveryCandidate
{
    public string FullPath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string KeepFilePath { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public bool CanDelete { get; set; }
    public bool IsMarked { get; set; }
    public int? SimilarityPercent { get; set; }
}
