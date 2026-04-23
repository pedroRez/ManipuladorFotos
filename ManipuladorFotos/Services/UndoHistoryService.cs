using System.Text.Json;
using System.IO;
using ManipuladorFotos.Models;

namespace ManipuladorFotos.Services;

public sealed class UndoHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _stateDir;
    private readonly string _stateFile;

    public UndoHistoryService()
    {
        _stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManipuladorFotos",
            "undo");
        _stateFile = Path.Combine(_stateDir, "undo-state.json");
    }

    public List<UndoBatch> Load()
    {
        try
        {
            if (!File.Exists(_stateFile))
            {
                return [];
            }

            var json = File.ReadAllText(_stateFile);
            var items = JsonSerializer.Deserialize<List<UndoBatch>>(json, JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<UndoBatch> batches)
    {
        Directory.CreateDirectory(_stateDir);
        var json = JsonSerializer.Serialize(batches, JsonOptions);
        File.WriteAllText(_stateFile, json);
    }
}
