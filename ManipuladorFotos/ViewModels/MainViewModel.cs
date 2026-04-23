using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Infrastructure;
using ManipuladorFotos.Models;
using ManipuladorFotos.Services;
using WinForms = System.Windows.Forms;

namespace ManipuladorFotos.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly FileScannerService _scanner = new();
    private readonly FilterService _filterService = new();

    private readonly RelayCommand _scanFilesCommand;
    private readonly RelayCommand _scanUnwantedCommand;

    private List<MediaItem> _allItems = [];
    private bool _isBusy;
    private string _currentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    private bool _includeSubfolders;
    private string _nameFilter = string.Empty;
    private string _extensionFilter = string.Empty;
    private string _typeFilter = "Todos";
    private DateTime? _createdFrom;
    private DateTime? _createdTo;
    private DateTime? _modifiedFrom;
    private DateTime? _modifiedTo;
    private string _minSizeKb = string.Empty;
    private string _maxSizeKb = string.Empty;
    private MediaItem? _selectedItem;
    private BitmapImage? _previewImage;
    private string _statusMessage = "Selecione uma pasta e clique em Escanear.";
    private string _unwantedExtensions = ".tmp,.db,.ini,.log";

    public MainViewModel()
    {
        DisplayedItems = [];
        UnwantedItems = [];
        TypeOptions = ["Todos", "Foto", "Video", "Outro"];

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        _scanFilesCommand = new RelayCommand(() => _ = ScanFilesAsync(), () => !IsBusy);
        _scanUnwantedCommand = new RelayCommand(() => _ = ScanUnwantedAsync(), () => !IsBusy);
        ScanFilesCommand = _scanFilesCommand;
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ScanUnwantedCommand = _scanUnwantedCommand;

        _ = ScanFilesAsync();
    }

    public ObservableCollection<MediaItem> DisplayedItems { get; }
    public ObservableCollection<MediaItem> UnwantedItems { get; }
    public IReadOnlyList<string> TypeOptions { get; }

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ScanFilesCommand { get; }
    public RelayCommand ApplyFiltersCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ScanUnwantedCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _scanFilesCommand.RaiseCanExecuteChanged();
                _scanUnwantedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CurrentFolder
    {
        get => _currentFolder;
        set => SetProperty(ref _currentFolder, value);
    }

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public string NameFilter
    {
        get => _nameFilter;
        set => SetProperty(ref _nameFilter, value);
    }

    public string ExtensionFilter
    {
        get => _extensionFilter;
        set => SetProperty(ref _extensionFilter, value);
    }

    public string TypeFilter
    {
        get => _typeFilter;
        set => SetProperty(ref _typeFilter, value);
    }

    public DateTime? CreatedFrom
    {
        get => _createdFrom;
        set => SetProperty(ref _createdFrom, value);
    }

    public DateTime? CreatedTo
    {
        get => _createdTo;
        set => SetProperty(ref _createdTo, value);
    }

    public DateTime? ModifiedFrom
    {
        get => _modifiedFrom;
        set => SetProperty(ref _modifiedFrom, value);
    }

    public DateTime? ModifiedTo
    {
        get => _modifiedTo;
        set => SetProperty(ref _modifiedTo, value);
    }

    public string MinSizeKb
    {
        get => _minSizeKb;
        set => SetProperty(ref _minSizeKb, value);
    }

    public string MaxSizeKb
    {
        get => _maxSizeKb;
        set => SetProperty(ref _maxSizeKb, value);
    }

    public MediaItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                UpdatePreviewImage(value);
            }
        }
    }

    public BitmapImage? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string UnwantedExtensions
    {
        get => _unwantedExtensions;
        set => SetProperty(ref _unwantedExtensions, value);
    }

    private void BrowseFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Selecione a pasta que deseja organizar",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(CurrentFolder) ? CurrentFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            CurrentFolder = dialog.SelectedPath;
            _ = ScanFilesAsync();
        }
    }

    private async Task ScanFilesAsync()
    {
        if (!Directory.Exists(CurrentFolder))
        {
            StatusMessage = "Pasta inválida. Escolha uma pasta existente.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Escaneando arquivos...";

        try
        {
            var scanned = await Task.Run(() => _scanner.Scan(CurrentFolder, IncludeSubfolders));
            _allItems = scanned;
            ApplyFilters();
            StatusMessage = $"{_allItems.Count} arquivos encontrados.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao escanear: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScanUnwantedAsync()
    {
        if (!Directory.Exists(CurrentFolder))
        {
            StatusMessage = "Pasta inválida para busca de indesejados.";
            return;
        }

        var extensions = UnwantedExtensions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IsBusy = true;
        StatusMessage = "Buscando arquivos indesejados...";

        try
        {
            var unwanted = await Task.Run(() => _scanner.ScanUnwantedByExtensions(CurrentFolder, IncludeSubfolders, extensions));
            UnwantedItems.Clear();
            foreach (var item in unwanted.OrderBy(x => x.Extension).ThenBy(x => x.Name))
            {
                UnwantedItems.Add(item);
            }

            StatusMessage = $"{UnwantedItems.Count} arquivos indesejados encontrados.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro na busca de indesejados: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearFilters()
    {
        NameFilter = string.Empty;
        ExtensionFilter = string.Empty;
        TypeFilter = "Todos";
        CreatedFrom = null;
        CreatedTo = null;
        ModifiedFrom = null;
        ModifiedTo = null;
        MinSizeKb = string.Empty;
        MaxSizeKb = string.Empty;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var criteria = new FilterCriteria
        {
            NameContains = NameFilter,
            ExtensionContains = ExtensionFilter,
            TypeFilter = TypeFilter,
            CreatedFrom = CreatedFrom,
            CreatedTo = CreatedTo,
            ModifiedFrom = ModifiedFrom,
            ModifiedTo = ModifiedTo,
            MinSizeBytes = ParseKbToBytes(MinSizeKb),
            MaxSizeBytes = ParseKbToBytes(MaxSizeKb)
        };

        var filtered = _filterService.Apply(_allItems, criteria)
            .OrderByDescending(x => x.CreationTime)
            .ToList();

        DisplayedItems.Clear();
        foreach (var item in filtered)
        {
            DisplayedItems.Add(item);
        }

        if (SelectedItem is not null && !DisplayedItems.Contains(SelectedItem))
        {
            SelectedItem = null;
        }

        if (_allItems.Count > 0)
        {
            StatusMessage = $"Mostrando {DisplayedItems.Count} de {_allItems.Count} arquivos.";
        }
    }

    private void UpdatePreviewImage(MediaItem? selected)
    {
        PreviewImage = null;
        if (selected is null || !selected.IsImage || !File.Exists(selected.FullPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(selected.FullPath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            PreviewImage = image;
        }
        catch
        {
            PreviewImage = null;
        }
    }

    private static long? ParseKbToBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!double.TryParse(value, out var kb) || kb < 0)
        {
            return null;
        }

        return (long)(kb * 1024);
    }
}

