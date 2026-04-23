using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using ManipuladorFotos.Infrastructure;
using ManipuladorFotos.Models;
using ManipuladorFotos.Services;
using Microsoft.VisualBasic.FileIO;
using WinForms = System.Windows.Forms;

namespace ManipuladorFotos.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly FileScannerService _scanner = new();
    private readonly FilterService _filterService = new();
    private readonly DuplicateAnalysisService _duplicateAnalysisService = new();

    private readonly RelayCommand _scanFilesCommand;
    private readonly RelayCommand _scanUnwantedCommand;
    private readonly RelayCommand _generateDeletionListCommand;
    private readonly RelayCommand _deleteMarkedCandidatesCommand;
    private readonly RelayCommand _cancelCurrentOperationCommand;
    private readonly RelayCommand _moveMarkedPhotosCommand;

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
    private DeletionCandidate? _selectedDeletionCandidate;
    private BitmapImage? _previewImage;
    private BitmapImage? _deletionPreviewImage;
    private string _statusMessage = "Selecione uma pasta e clique em Escanear.";
    private string _unwantedExtensions = ".tmp,.db,.ini,.log";
    private bool _useSameNameRule = true;
    private bool _useSameSizeRule;
    private bool _useSimilarInSequenceRule = true;
    private string _similarSecondsWindow = "10";
    private string _similarDistanceThreshold = "8";
    private string _keepPreference = "Maior resolução";
    private int _markedDeletionCount;
    private long _markedDeletionBytes;
    private CancellationTokenSource? _operationCts;
    private double _operationProgressPercent;
    private bool _isProgressIndeterminate;
    private string _operationProgressLabel = string.Empty;
    private string _newMoveFolderPath = "Selecionadas";

    public MainViewModel()
    {
        DisplayedItems = [];
        UnwantedItems = [];
        DeletionCandidates = [];
        TypeOptions = ["Todos", "Foto", "Video", "Outro"];
        KeepPreferenceOptions = ["Maior resolução", "Maior tamanho", "Mais recente", "Mais antiga"];

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        _scanFilesCommand = new RelayCommand(() => _ = ScanFilesAsync(), () => !IsBusy);
        _scanUnwantedCommand = new RelayCommand(() => _ = ScanUnwantedAsync(), () => !IsBusy);
        _generateDeletionListCommand = new RelayCommand(() => _ = GenerateDeletionListAsync(), () => !IsBusy && _allItems.Count > 0);
        _deleteMarkedCandidatesCommand = new RelayCommand(() => _ = DeleteMarkedCandidatesAsync(), () => !IsBusy && DeletionCandidates.Any(x => x.IsMarked && x.CanDelete));
        _cancelCurrentOperationCommand = new RelayCommand(CancelCurrentOperation, () => IsBusy);
        _moveMarkedPhotosCommand = new RelayCommand(() => _ = MoveMarkedPhotosAsync(), () => !IsBusy && DisplayedItems.Any(x => x.IsMarked && x.IsImage));

        ScanFilesCommand = _scanFilesCommand;
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ScanUnwantedCommand = _scanUnwantedCommand;
        GenerateDeletionListCommand = _generateDeletionListCommand;
        MarkAllDeletionCandidatesCommand = new RelayCommand(() => SetAllCandidatesMarked(true), () => DeletionCandidates.Count > 0);
        UnmarkAllDeletionCandidatesCommand = new RelayCommand(() => SetAllCandidatesMarked(false), () => DeletionCandidates.Count > 0);
        DeleteMarkedCandidatesCommand = _deleteMarkedCandidatesCommand;
        CancelCurrentOperationCommand = _cancelCurrentOperationCommand;
        MoveMarkedPhotosCommand = _moveMarkedPhotosCommand;

        _ = ScanFilesAsync();
    }

    public ObservableCollection<MediaItem> DisplayedItems { get; }
    public ObservableCollection<MediaItem> UnwantedItems { get; }
    public ObservableCollection<DeletionCandidate> DeletionCandidates { get; }
    public IReadOnlyList<string> TypeOptions { get; }
    public IReadOnlyList<string> KeepPreferenceOptions { get; }

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ScanFilesCommand { get; }
    public RelayCommand ApplyFiltersCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ScanUnwantedCommand { get; }
    public RelayCommand GenerateDeletionListCommand { get; }
    public RelayCommand MarkAllDeletionCandidatesCommand { get; }
    public RelayCommand UnmarkAllDeletionCandidatesCommand { get; }
    public RelayCommand DeleteMarkedCandidatesCommand { get; }
    public RelayCommand CancelCurrentOperationCommand { get; }
    public RelayCommand MoveMarkedPhotosCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
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
                PreviewImage = LoadPreviewImage(value?.FullPath, value?.IsImage == true);
            }
        }
    }

    public DeletionCandidate? SelectedDeletionCandidate
    {
        get => _selectedDeletionCandidate;
        set
        {
            if (SetProperty(ref _selectedDeletionCandidate, value))
            {
                DeletionPreviewImage = LoadPreviewImage(value?.FullPath, value?.Item.IsImage == true);
            }
        }
    }

    public BitmapImage? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public BitmapImage? DeletionPreviewImage
    {
        get => _deletionPreviewImage;
        private set => SetProperty(ref _deletionPreviewImage, value);
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

    public bool UseSameNameRule
    {
        get => _useSameNameRule;
        set => SetProperty(ref _useSameNameRule, value);
    }

    public bool UseSameSizeRule
    {
        get => _useSameSizeRule;
        set => SetProperty(ref _useSameSizeRule, value);
    }

    public bool UseSimilarInSequenceRule
    {
        get => _useSimilarInSequenceRule;
        set => SetProperty(ref _useSimilarInSequenceRule, value);
    }

    public string SimilarSecondsWindow
    {
        get => _similarSecondsWindow;
        set => SetProperty(ref _similarSecondsWindow, value);
    }

    public string SimilarDistanceThreshold
    {
        get => _similarDistanceThreshold;
        set => SetProperty(ref _similarDistanceThreshold, value);
    }

    public string KeepPreference
    {
        get => _keepPreference;
        set => SetProperty(ref _keepPreference, value);
    }

    public int MarkedDeletionCount
    {
        get => _markedDeletionCount;
        private set => SetProperty(ref _markedDeletionCount, value);
    }

    public string MarkedDeletionSizeLabel => FormatBytes(_markedDeletionBytes);

    public double OperationProgressPercent
    {
        get => _operationProgressPercent;
        private set => SetProperty(ref _operationProgressPercent, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public string OperationProgressLabel
    {
        get => _operationProgressLabel;
        private set => SetProperty(ref _operationProgressLabel, value);
    }

    public string NewMoveFolderPath
    {
        get => _newMoveFolderPath;
        set => SetProperty(ref _newMoveFolderPath, value);
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

        DisplayedItems.Clear();
        UnwantedItems.Clear();
        ClearDeletionCandidates();
        SelectedItem = null;

        var cts = BeginOperation("Escaneando arquivos...", true);

        try
        {
            var progress = new Progress<ScanProgressInfo>(p =>
            {
                var percent = p.Total > 0 ? (double)p.Processed / p.Total * 100d : 0d;
                UpdateProgress(percent, false, $"Escaneando: {p.Processed}/{p.Total}");
                StatusMessage = $"Escaneando arquivos... {p.Processed}/{p.Total}";
            });

            var scanned = await Task.Run(() => _scanner.Scan(CurrentFolder, IncludeSubfolders, cts.Token, progress), cts.Token);
            _allItems = scanned;
            ApplyFilters();
            ClearDeletionCandidates();
            StatusMessage = $"{_allItems.Count} arquivos encontrados.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Escaneamento cancelado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao escanear: {ex.Message}";
        }
        finally
        {
            EndOperation(cts);
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

        UnwantedItems.Clear();

        var cts = BeginOperation("Buscando arquivos indesejados...", true);

        try
        {
            var progress = new Progress<ScanProgressInfo>(p =>
            {
                var percent = p.Total > 0 ? (double)p.Processed / p.Total * 100d : 0d;
                UpdateProgress(percent, false, $"Busca indesejados: {p.Processed}/{p.Total}");
                StatusMessage = $"Buscando arquivos indesejados... {p.Processed}/{p.Total}";
            });

            var unwanted = await Task.Run(() => _scanner.ScanUnwantedByExtensions(CurrentFolder, IncludeSubfolders, extensions, cts.Token, progress), cts.Token);
            foreach (var item in unwanted.OrderBy(x => x.Extension).ThenBy(x => x.Name))
            {
                UnwantedItems.Add(item);
            }

            StatusMessage = $"{UnwantedItems.Count} arquivos indesejados encontrados.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Busca de indesejados cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro na busca de indesejados: {ex.Message}";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task GenerateDeletionListAsync()
    {
        if (_allItems.Count == 0)
        {
            StatusMessage = "Escaneie a pasta antes de gerar a lista de exclusão.";
            return;
        }

        ClearDeletionCandidates();

        var cts = BeginOperation("Gerando lista de exclusão com base nas regras...", false);

        try
        {
            var options = new DuplicateAnalysisOptions
            {
                UseSameNameRule = UseSameNameRule,
                UseSameSizeRule = UseSameSizeRule,
                UseSimilarInSequenceRule = UseSimilarInSequenceRule,
                SimilarSecondsWindow = ParseIntOrDefault(SimilarSecondsWindow, 10, 1, 3600),
                SimilarDistanceThreshold = ParseIntOrDefault(SimilarDistanceThreshold, 8, 1, 64),
                KeepPreference = KeepPreference
            };

            var progress = new Progress<AnalysisProgressInfo>(p =>
            {
                var percent = p.TotalSteps > 0 ? (double)p.CompletedSteps / p.TotalSteps * 100d : 0d;
                UpdateProgress(percent, false, $"{p.Stage} ({p.CompletedSteps}/{p.TotalSteps})");
                StatusMessage = $"Gerando lista de exclusão... {p.Stage}";
            });

            var candidates = await Task.Run(() => _duplicateAnalysisService.BuildDeletionCandidates(_allItems, options, cts.Token, progress), cts.Token);

            foreach (var candidate in candidates)
            {
                candidate.PropertyChanged += CandidateOnPropertyChanged;
                DeletionCandidates.Add(candidate);
            }

            UpdateMarkedDeletionSummary();
            var protectedCount = DeletionCandidates.Count(x => !x.CanDelete);
            StatusMessage = $"Lista gerada: {DeletionCandidates.Count} itens ({protectedCount} originais protegidas).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Geração da lista de exclusão cancelada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao gerar lista de exclusão: {ex.Message}";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task DeleteMarkedCandidatesAsync()
    {
        var marked = DeletionCandidates.Where(x => x.IsMarked && x.CanDelete).ToList();
        if (marked.Count == 0)
        {
            StatusMessage = "Nenhum item marcado para exclusão.";
            return;
        }

        var bytes = marked.Sum(x => x.Item.SizeBytes);
        var confirm = System.Windows.MessageBox.Show(
            $"Você está prestes a enviar {marked.Count} arquivos para a lixeira.\nEspaço estimado: {FormatBytes(bytes)}.\n\nDeseja continuar?",
            "Confirmar exclusão",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Exclusão cancelada pelo usuário.";
            return;
        }

        var cts = BeginOperation("Enviando arquivos marcados para a lixeira...", false);

        var deleted = 0;
        var failed = 0;

        try
        {
            await Task.Run(() =>
            {
                var total = marked.Count;
                var processed = 0;

                foreach (var candidate in marked)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(candidate.FullPath))
                        {
                            FileSystem.DeleteFile(
                                candidate.FullPath,
                                UIOption.OnlyErrorDialogs,
                                RecycleOption.SendToRecycleBin,
                                UICancelOption.DoNothing);

                            deleted++;
                        }
                    }
                    catch
                    {
                        failed++;
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"Exclusão: {processed}/{total}");
                    }
                }
            }, cts.Token);

            var deletedPaths = marked.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allItems = _allItems.Where(x => !deletedPaths.Contains(x.FullPath)).ToList();

            RemoveByPath(DisplayedItems, deletedPaths);
            RemoveByPath(UnwantedItems, deletedPaths);
            RemoveCandidatesByPath(deletedPaths);

            if (SelectedItem is not null && deletedPaths.Contains(SelectedItem.FullPath))
            {
                SelectedItem = null;
            }

            if (SelectedDeletionCandidate is not null && deletedPaths.Contains(SelectedDeletionCandidate.FullPath))
            {
                SelectedDeletionCandidate = null;
            }

            UpdateMarkedDeletionSummary();
            StatusMessage = $"Exclusão concluída. Enviados para lixeira: {deleted}. Falhas: {failed}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Exclusão cancelada.";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task MoveMarkedPhotosAsync()
    {
        var markedPhotos = DisplayedItems.Where(x => x.IsMarked && x.IsImage).ToList();
        if (markedPhotos.Count == 0)
        {
            StatusMessage = "Nenhuma foto marcada para mover.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewMoveFolderPath))
        {
            StatusMessage = "Informe o nome ou caminho da nova pasta de destino.";
            return;
        }

        var destinationFolder = Path.IsPathRooted(NewMoveFolderPath)
            ? NewMoveFolderPath
            : Path.Combine(CurrentFolder, NewMoveFolderPath);

        var confirm = System.Windows.MessageBox.Show(
            $"Mover {markedPhotos.Count} fotos para:\n{destinationFolder}\n\nDeseja continuar?",
            "Confirmar movimentação",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Movimentação cancelada pelo usuário.";
            return;
        }

        var cts = BeginOperation("Movendo fotos selecionadas...", false);
        var moved = 0;
        var failed = 0;

        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(destinationFolder);
                var total = markedPhotos.Count;
                var processed = 0;

                foreach (var photo in markedPhotos)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (!File.Exists(photo.FullPath))
                        {
                            processed++;
                            continue;
                        }

                        var fileName = Path.GetFileName(photo.FullPath);
                        var targetPath = GetUniqueDestinationPath(destinationFolder, fileName);
                        File.Move(photo.FullPath, targetPath);
                        moved++;
                    }
                    catch
                    {
                        failed++;
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"Movendo: {processed}/{total}");
                    }
                }
            }, cts.Token);

            var movedPaths = markedPhotos.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _allItems = _allItems.Where(x => !movedPaths.Contains(x.FullPath)).ToList();
            RemoveByPath(DisplayedItems, movedPaths);
            RemoveByPath(UnwantedItems, movedPaths);
            RemoveCandidatesByPath(movedPaths);
            SelectedItem = null;
            StatusMessage = $"Movimentação concluída. Movidas: {moved}. Falhas: {failed}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Movimentação cancelada.";
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private void SetAllCandidatesMarked(bool marked)
    {
        foreach (var candidate in DeletionCandidates)
        {
            candidate.IsMarked = candidate.CanDelete && marked;
        }

        UpdateMarkedDeletionSummary();
        RaiseCommandStates();
    }

    private void CandidateOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeletionCandidate.IsMarked))
        {
            if (sender is DeletionCandidate candidate && !candidate.CanDelete && candidate.IsMarked)
            {
                System.Windows.MessageBox.Show(
                    "Essa imagem está protegida como original do grupo. Manter pelo menos uma foto evita perder todas as versões iguais.",
                    "Original protegida",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                candidate.IsMarked = false;
                StatusMessage = "A foto original protegida não pode ser marcada para exclusão.";
                return;
            }

            UpdateMarkedDeletionSummary();
            RaiseCommandStates();
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

        RaiseCommandStates();
    }

    private static BitmapImage? LoadPreviewImage(string? path, bool canLoad)
    {
        if (!canLoad || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void ClearDeletionCandidates()
    {
        foreach (var item in DeletionCandidates)
        {
            item.PropertyChanged -= CandidateOnPropertyChanged;
        }

        DeletionCandidates.Clear();
        SelectedDeletionCandidate = null;
        DeletionPreviewImage = null;
        UpdateMarkedDeletionSummary();
        RaiseCommandStates();
    }

    private void RemoveCandidatesByPath(HashSet<string> deletedPaths)
    {
        for (var i = DeletionCandidates.Count - 1; i >= 0; i--)
        {
            var candidate = DeletionCandidates[i];
            if (!deletedPaths.Contains(candidate.FullPath))
            {
                continue;
            }

            candidate.PropertyChanged -= CandidateOnPropertyChanged;
            DeletionCandidates.RemoveAt(i);
        }

        UpdateMarkedDeletionSummary();
    }

    private static void RemoveByPath(ObservableCollection<MediaItem> source, HashSet<string> deletedPaths)
    {
        for (var i = source.Count - 1; i >= 0; i--)
        {
            if (deletedPaths.Contains(source[i].FullPath))
            {
                source.RemoveAt(i);
            }
        }
    }

    private CancellationTokenSource BeginOperation(string startMessage, bool indeterminate)
    {
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = startMessage;
        UpdateProgress(0, indeterminate, "Iniciando...");
        return _operationCts;
    }

    private void EndOperation(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_operationCts, cts))
        {
            _operationCts = null;
        }

        cts.Dispose();
        UpdateProgress(0, false, string.Empty);
        IsBusy = false;
        RaiseCommandStates();
    }

    private void CancelCurrentOperation()
    {
        _operationCts?.Cancel();
        StatusMessage = "Cancelando operação...";
        UpdateProgress(OperationProgressPercent, true, "Cancelando...");
    }

    private void UpdateProgress(double percent, bool indeterminate, string label)
    {
        OperationProgressPercent = Math.Clamp(percent, 0, 100);
        IsProgressIndeterminate = indeterminate;
        OperationProgressLabel = label;
    }

    private void RaiseCommandStates()
    {
        _scanFilesCommand.RaiseCanExecuteChanged();
        _scanUnwantedCommand.RaiseCanExecuteChanged();
        _generateDeletionListCommand.RaiseCanExecuteChanged();
        _deleteMarkedCandidatesCommand.RaiseCanExecuteChanged();
        _moveMarkedPhotosCommand.RaiseCanExecuteChanged();
        _cancelCurrentOperationCommand.RaiseCanExecuteChanged();
        MarkAllDeletionCandidatesCommand.RaiseCanExecuteChanged();
        UnmarkAllDeletionCandidatesCommand.RaiseCanExecuteChanged();
    }

    private void UpdateMarkedDeletionSummary()
    {
        var marked = DeletionCandidates.Where(x => x.IsMarked).ToList();
        MarkedDeletionCount = marked.Count;
        _markedDeletionBytes = marked.Sum(x => x.Item.SizeBytes);
        OnPropertyChanged(nameof(MarkedDeletionSizeLabel));
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

    private static int ParseIntOrDefault(string value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        if (parsed < min)
        {
            return min;
        }

        if (parsed > max)
        {
            return max;
        }

        return parsed;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024L * 1024L * 1024L => $"{bytes / 1024.0 / 1024.0:F1} MB",
            _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
        };
    }

    private static string GetUniqueDestinationPath(string folder, string fileName)
    {
        var target = Path.Combine(folder, fileName);
        if (!File.Exists(target))
        {
            return target;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var index = 1;
        while (true)
        {
            var candidate = Path.Combine(folder, $"{name}_{index}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }
}
