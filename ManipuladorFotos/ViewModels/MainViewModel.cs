using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    private readonly DuplicateAnalysisService _duplicateAnalysisService = new();
    private readonly UndoHistoryService _undoHistoryService = new();
    private readonly ReviewRecoveryService _reviewRecoveryService = new();

    private readonly RelayCommand _scanFilesCommand;
    private readonly RelayCommand _scanUnwantedCommand;
    private readonly RelayCommand _markAllDisplayedItemsCommand;
    private readonly RelayCommand _unmarkAllDisplayedItemsCommand;
    private readonly RelayCommand _markAllUnwantedItemsCommand;
    private readonly RelayCommand _unmarkAllUnwantedItemsCommand;
    private readonly RelayCommand _generateDeletionListCommand;
    private readonly RelayCommand _deleteMarkedCandidatesCommand;
    private readonly RelayCommand _cancelCurrentOperationCommand;
    private readonly RelayCommand _moveMarkedPhotosCommand;
    private readonly RelayCommand _separateMediaCommand;
    private readonly RelayCommand _organizePhotosByDateCommand;
    private readonly RelayCommand _deleteMarkedUnwantedCommand;
    private readonly RelayCommand _exportDeletionListCommand;
    private readonly RelayCommand _exportDeletionListTxtCommand;
    private readonly RelayCommand _applyCleanupModeCommand;
    private readonly RelayCommand _autoSelectByGroupCommand;
    private readonly RelayCommand _startFocusedReviewCommand;
    private readonly RelayCommand _exitFocusedReviewCommand;
    private readonly RelayCommand _focusedReviewPreviousCommand;
    private readonly RelayCommand _focusedReviewNextCommand;
    private readonly RelayCommand _focusedReviewKeepCommand;
    private readonly RelayCommand _focusedReviewDeleteCommand;
    private readonly RelayCommand _focusedReviewSetOriginalCommand;
    private readonly RelayCommand _focusedReviewDeleteGroupCommand;
    private readonly RelayCommand _startListReviewCommand;
    private readonly RelayCommand _exitListReviewCommand;
    private readonly RelayCommand _listReviewSetOriginalCommand;
    private readonly RelayCommand _listReviewDeleteGroupCommand;
    private readonly RelayCommand _showDeletionExplanationCommand;
    private readonly RelayCommand _undoLastOperationCommand;
    private readonly RelayCommand _undoSelectedOperationCommand;
    private readonly RelayCommand _toggleOrganizeModeCommand;
    private readonly RelayCommand _organizeSimpleNextStepCommand;
    private readonly RelayCommand _organizeSimplePreviousStepCommand;
    private readonly RelayCommand _executeSimpleOrganizeActionCommand;
    private readonly RelayCommand _toggleAdvancedOrganizeOptionsCommand;
    private readonly RelayCommand _toggleOrganizeAssistantVisibilityCommand;
    private readonly RelayCommand _workflowNextStepCommand;
    private readonly RelayCommand _workflowPreviousStepCommand;
    private readonly RelayCommand _workflowExecuteStepCommand;
    private readonly RelayCommand _workflowOpenFoundItemsCommand;
    private readonly RelayCommand _workflowCloseFoundItemsCommand;
    private readonly RelayCommand _workflowFoundItemsNextStepCommand;
    private readonly RelayCommand _workflowRestartCommand;
    private readonly RelayCommand _workflowOpenDeletionTabCommand;
    private readonly RelayCommand _workflowOpenUnwantedTabCommand;

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
    private BitmapImage? _keepDeletionPreviewImage;
    private string _statusMessage = "Selecione uma pasta e clique em Escanear.";
    private string _unwantedExtensions = ".tmp,.db,.ini,.log,.bak,.old,.thm,.aae,.xmp,.lrv,.sfk,.pkf,.dop";
    private bool _useSameNameRule = true;
    private bool _useSameSizeRule = true;
    private bool _useSimilarInSequenceRule;
    private string _similarSecondsWindow = "10";
    private string _similarDistanceThreshold = "8";
    private string _keepPreference = "Maior resolução";
    private int _markedDeletionCount;
    private long _markedDeletionBytes;
    private CancellationTokenSource? _operationCts;
    private double _operationProgressPercent;
    private bool _isProgressIndeterminate;
    private string _operationProgressLabel = string.Empty;
    private bool _isOperationCancellationRequested;
    private string _newMoveFolderPath = "Selecionadas";
    private string _cleanupMode = "Balanceado";
    private string _mediaOrganizationMode = "Separar Fotos e Vídeos";
    private string _mediaOperationFolderName = "MidiaUnificada";
    private string _deletionApplyType = "Todos";
    private string _dateOrganizationMode = "Ano/Mês";
    private bool _flattenSubfoldersByDate;
    private string _dateOrganizationBaseFolder = "OrganizadoPorData";
    private bool _isSimpleOrganizeMode = true;
    private int _organizeSimpleStep = 1;
    private string _selectedSimpleOrganizeAction = "Separar fotos e vídeos em pastas";
    private bool _isAdvancedOrganizeOptionsOpen;
    private bool _isOrganizeAssistantVisible = true;
    private int _selectedMainTabIndex;
    private int _workflowStep = 1;
    private string _workflowObjective = "Fazer limpeza de fotos";
    private string _workflowOrganizeAction = "Separar Fotos e Vídeos";
    private bool _isWorkflowFocusedVisualization;
    private bool _hasWorkflowScanData;
    private string _workflowScanSummary = "Ainda não escaneado.";
    private string _workflowExecutionSummary = string.Empty;
    private bool _compareModeEnabled;
    private bool _isFocusedReviewMode;
    private bool _isListReviewMode;
    private List<DeletionCandidate> _focusedReviewItems = [];
    private int _focusedReviewIndex = -1;
    private DeletionCandidate? _selectedFocusedGroupItem;
    private DeletionCandidate? _selectedListReviewCandidate;
    private string _focusedGroupSummary = "Grupo: 0 itens";
    private List<UndoBatch> _undoBatches = [];
    private string? _selectedUndoBatchId;
    private string _lastScanFolder = string.Empty;
    private bool _lastScanIncludeSubfolders;
    private int _lastScanFileCount = -1;
    private long _lastScanLatestWriteUtcTicks = -1;
    private CancellationTokenSource? _selectedItemPreviewCts;
    private CancellationTokenSource? _deletionPreviewCts;
    private string _lastHeicCodecWarningPath = string.Empty;
    private ReviewRecoverySnapshot? _pendingRecoverySnapshot;
    private bool _isRestoringRecovery;
    private CancellationTokenSource? _recoverySaveCts;
    private const int PreviewDecodePixelWidth = 1920;
    private const int PreviewDebounceMs = 120;
    private const int MaxFocusedGroupItems = 200;
    private const string InternalFolderName = ".manipuladorfotos";

    public MainViewModel()
    {
        DisplayedItems = [];
        UnwantedItems = [];
        DeletionCandidates = [];
        FocusedGroupItems = [];
        ListReviewItems = [];
        TypeOptions = ["Todos", "Foto", "Video", "Outro"];
        KeepPreferenceOptions = ["Maior resolução", "Maior tamanho", "Mais recente", "Mais antiga"];
        DateOrganizationModes = ["Ano", "Ano/Mês", "Ano/Mês/Dia"];
        CleanupModes = ["Conservador", "Balanceado", "Agressivo"];
        MediaOrganizationModes = ["Separar Fotos e Vídeos", "Unificar Fotos e Vídeos", "Extrair Somente Vídeos"];
        DeletionApplyTypeOptions = ["Todos", "Somente Fotos", "Somente Vídeos"];
        SimpleOrganizeActions = ["Separar fotos e vídeos em pastas", "Juntar tudo em uma pasta", "Extrair apenas vídeos", "Organizar fotos por data", "Mover fotos marcadas"];
        WorkflowObjectives = ["Remover arquivos indesejados", "Fazer limpeza de fotos", "Organizar fotos e vídeos"];
        WorkflowOrganizeActions = ["Separar Fotos e Vídeos", "Unificar Fotos e Vídeos", "Extrair Somente Vídeos", "Organizar por Data", "Mover Fotos Marcadas"];

        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        _scanFilesCommand = new RelayCommand(() => _ = ScanFilesAsync(), () => !IsBusy);
        _scanUnwantedCommand = new RelayCommand(() => _ = ScanUnwantedAsync(), () => !IsBusy);
        _markAllDisplayedItemsCommand = new RelayCommand(() => SetAllDisplayedItemsMarked(true), () => !IsBusy && DisplayedItems.Count > 0);
        _unmarkAllDisplayedItemsCommand = new RelayCommand(() => SetAllDisplayedItemsMarked(false), () => !IsBusy && DisplayedItems.Count > 0);
        _markAllUnwantedItemsCommand = new RelayCommand(() => SetAllUnwantedItemsMarked(true), () => !IsBusy && UnwantedItems.Count > 0);
        _unmarkAllUnwantedItemsCommand = new RelayCommand(() => SetAllUnwantedItemsMarked(false), () => !IsBusy && UnwantedItems.Count > 0);
        _generateDeletionListCommand = new RelayCommand(() => _ = GenerateDeletionListAsync(), () => !IsBusy);
        _deleteMarkedCandidatesCommand = new RelayCommand(() => _ = DeleteMarkedCandidatesAsync(), () => !IsBusy && DeletionCandidates.Any(x => x.IsMarked && x.CanDelete));
        _cancelCurrentOperationCommand = new RelayCommand(CancelCurrentOperation, () => IsBusy);
        _moveMarkedPhotosCommand = new RelayCommand(() => _ = MoveMarkedPhotosAsync(), () => !IsBusy);
        _separateMediaCommand = new RelayCommand(() => _ = SeparateMediaAsync(), () => !IsBusy);
        _organizePhotosByDateCommand = new RelayCommand(() => _ = OrganizePhotosByDateAsync(), () => !IsBusy);
        _deleteMarkedUnwantedCommand = new RelayCommand(() => _ = DeleteMarkedUnwantedAsync(), () => !IsBusy);
        _exportDeletionListCommand = new RelayCommand(ExportDeletionListCsv, () => DeletionCandidates.Count > 0);
        _exportDeletionListTxtCommand = new RelayCommand(ExportDeletionListTxt, () => DeletionCandidates.Count > 0);
        _applyCleanupModeCommand = new RelayCommand(ApplyCleanupMode);
        _autoSelectByGroupCommand = new RelayCommand(AutoSelectByGroup, () => DeletionCandidates.Any(x => x.CanDelete));
        _startFocusedReviewCommand = new RelayCommand(StartFocusedReviewMode, () => !IsBusy && DeletionCandidates.Any(x => x.CanDelete));
        _exitFocusedReviewCommand = new RelayCommand(ExitFocusedReviewMode, () => IsFocusedReviewMode);
        _focusedReviewPreviousCommand = new RelayCommand(() => MoveFocusedReview(-1), () => IsFocusedReviewMode && _focusedReviewItems.Count > 0);
        _focusedReviewNextCommand = new RelayCommand(() => MoveFocusedReview(1), () => IsFocusedReviewMode && _focusedReviewItems.Count > 0);
        _focusedReviewKeepCommand = new RelayCommand(() => SetFocusedReviewMark(false), () => IsFocusedReviewMode && CurrentFocusedReviewCandidate is not null);
        _focusedReviewDeleteCommand = new RelayCommand(() => SetFocusedReviewMark(true), () => IsFocusedReviewMode && CurrentFocusedReviewCandidate is not null);
        _focusedReviewSetOriginalCommand = new RelayCommand(SetFocusedReviewItemAsOriginal, () => IsFocusedReviewMode && SelectedFocusedGroupItem is not null);
        _focusedReviewDeleteGroupCommand = new RelayCommand(() => _ = DeleteCurrentGroupAsync(), () => !IsBusy && IsFocusedReviewMode && CurrentFocusedReviewGroupAllItems.Count > 0);
        _startListReviewCommand = new RelayCommand(StartListReviewMode, () => !IsBusy && DeletionCandidates.Count > 0);
        _exitListReviewCommand = new RelayCommand(ExitListReviewMode, () => IsListReviewMode);
        _listReviewSetOriginalCommand = new RelayCommand(SetListReviewItemAsOriginal, () => IsListReviewMode && SelectedListReviewCandidate is not null);
        _listReviewDeleteGroupCommand = new RelayCommand(() => _ = DeleteSelectedListGroupAsync(), () => !IsBusy && IsListReviewMode && SelectedListReviewCandidate is not null);
        _showDeletionExplanationCommand = new RelayCommand(ShowDeletionExplanation);
        _undoLastOperationCommand = new RelayCommand(() => _ = UndoLastOperationAsync(), () => !IsBusy && _undoBatches.Count > 0);
        _undoSelectedOperationCommand = new RelayCommand(() => _ = UndoSelectedOperationAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedUndoBatchId));
        _toggleOrganizeModeCommand = new RelayCommand(ToggleOrganizeMode, () => !IsBusy);
        _organizeSimpleNextStepCommand = new RelayCommand(AdvanceSimpleOrganizeStep, () => !IsBusy && OrganizeSimpleStep < 3);
        _organizeSimplePreviousStepCommand = new RelayCommand(RewindSimpleOrganizeStep, () => !IsBusy && OrganizeSimpleStep > 1);
        _executeSimpleOrganizeActionCommand = new RelayCommand(() => _ = ExecuteSimpleOrganizeActionAsync(), () => !IsBusy && IsSimpleOrganizeMode && OrganizeSimpleStep == 3);
        _toggleAdvancedOrganizeOptionsCommand = new RelayCommand(() => IsAdvancedOrganizeOptionsOpen = !IsAdvancedOrganizeOptionsOpen, () => !IsBusy && !IsSimpleOrganizeMode);
        _toggleOrganizeAssistantVisibilityCommand = new RelayCommand(() => IsOrganizeAssistantVisible = !IsOrganizeAssistantVisible, () => !IsBusy && IsSimpleOrganizeMode);
        _workflowNextStepCommand = new RelayCommand(() => _ = NextWorkflowStepAsync(), () => !IsBusy && !IsWorkflowFocusedVisualization && WorkflowStep < 4);
        _workflowPreviousStepCommand = new RelayCommand(PreviousWorkflowStep, () => !IsBusy && !IsWorkflowFocusedVisualization && WorkflowStep > 1);
        _workflowExecuteStepCommand = new RelayCommand(() => _ = ExecuteWorkflowStepAsync(), () => !IsBusy && !IsWorkflowFocusedVisualization && WorkflowStep == 4);
        _workflowOpenFoundItemsCommand = new RelayCommand(OpenWorkflowFocusedVisualization, () => !IsBusy && WorkflowCanVisualizeFoundItems);
        _workflowCloseFoundItemsCommand = new RelayCommand(CloseWorkflowFocusedVisualization, () => !IsBusy && IsWorkflowFocusedVisualization);
        _workflowFoundItemsNextStepCommand = new RelayCommand(() => _ = ProceedFromFocusedVisualizationAsync(), () => !IsBusy && IsWorkflowFocusedVisualization && WorkflowStep is 3 or 4);
        _workflowRestartCommand = new RelayCommand(RestartWorkflow, () => !IsBusy && WorkflowStep == 5);
        _workflowOpenDeletionTabCommand = new RelayCommand(OpenDeletionListTabFromWorkflow, () => !IsBusy && CanOpenDeletionListFromWorkflow);
        _workflowOpenUnwantedTabCommand = new RelayCommand(OpenUnwantedTabFromWorkflow, () => !IsBusy && CanOpenUnwantedFromWorkflow);

        ScanFilesCommand = _scanFilesCommand;
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ScanUnwantedCommand = _scanUnwantedCommand;
        MarkAllDisplayedItemsCommand = _markAllDisplayedItemsCommand;
        UnmarkAllDisplayedItemsCommand = _unmarkAllDisplayedItemsCommand;
        MarkAllUnwantedItemsCommand = _markAllUnwantedItemsCommand;
        UnmarkAllUnwantedItemsCommand = _unmarkAllUnwantedItemsCommand;
        GenerateDeletionListCommand = _generateDeletionListCommand;
        MarkAllDeletionCandidatesCommand = new RelayCommand(() => SetAllCandidatesMarked(true), () => DeletionCandidates.Count > 0);
        UnmarkAllDeletionCandidatesCommand = new RelayCommand(() => SetAllCandidatesMarked(false), () => DeletionCandidates.Count > 0);
        DeleteMarkedCandidatesCommand = _deleteMarkedCandidatesCommand;
        CancelCurrentOperationCommand = _cancelCurrentOperationCommand;
        MoveMarkedPhotosCommand = _moveMarkedPhotosCommand;
        SeparateMediaCommand = _separateMediaCommand;
        OrganizePhotosByDateCommand = _organizePhotosByDateCommand;
        DeleteMarkedUnwantedCommand = _deleteMarkedUnwantedCommand;
        ExportDeletionListCommand = _exportDeletionListCommand;
        ExportDeletionListTxtCommand = _exportDeletionListTxtCommand;
        ApplyCleanupModeCommand = _applyCleanupModeCommand;
        AutoSelectByGroupCommand = _autoSelectByGroupCommand;
        StartFocusedReviewCommand = _startFocusedReviewCommand;
        ExitFocusedReviewCommand = _exitFocusedReviewCommand;
        FocusedReviewPreviousCommand = _focusedReviewPreviousCommand;
        FocusedReviewNextCommand = _focusedReviewNextCommand;
        FocusedReviewKeepCommand = _focusedReviewKeepCommand;
        FocusedReviewDeleteCommand = _focusedReviewDeleteCommand;
        FocusedReviewSetOriginalCommand = _focusedReviewSetOriginalCommand;
        FocusedReviewDeleteGroupCommand = _focusedReviewDeleteGroupCommand;
        StartListReviewCommand = _startListReviewCommand;
        ExitListReviewCommand = _exitListReviewCommand;
        ListReviewSetOriginalCommand = _listReviewSetOriginalCommand;
        ListReviewDeleteGroupCommand = _listReviewDeleteGroupCommand;
        ShowDeletionExplanationCommand = _showDeletionExplanationCommand;
        UndoLastOperationCommand = _undoLastOperationCommand;
        UndoSelectedOperationCommand = _undoSelectedOperationCommand;
        ToggleOrganizeModeCommand = _toggleOrganizeModeCommand;
        OrganizeSimpleNextStepCommand = _organizeSimpleNextStepCommand;
        OrganizeSimplePreviousStepCommand = _organizeSimplePreviousStepCommand;
        ExecuteSimpleOrganizeActionCommand = _executeSimpleOrganizeActionCommand;
        ToggleAdvancedOrganizeOptionsCommand = _toggleAdvancedOrganizeOptionsCommand;
        ToggleOrganizeAssistantVisibilityCommand = _toggleOrganizeAssistantVisibilityCommand;
        WorkflowNextStepCommand = _workflowNextStepCommand;
        WorkflowPreviousStepCommand = _workflowPreviousStepCommand;
        WorkflowExecuteStepCommand = _workflowExecuteStepCommand;
        WorkflowOpenFoundItemsCommand = _workflowOpenFoundItemsCommand;
        WorkflowCloseFoundItemsCommand = _workflowCloseFoundItemsCommand;
        WorkflowFoundItemsNextStepCommand = _workflowFoundItemsNextStepCommand;
        WorkflowRestartCommand = _workflowRestartCommand;
        WorkflowOpenDeletionTabCommand = _workflowOpenDeletionTabCommand;
        WorkflowOpenUnwantedTabCommand = _workflowOpenUnwantedTabCommand;

        _undoBatches = _undoHistoryService.Load();
        RefreshUndoSelection();

        _pendingRecoverySnapshot = _reviewRecoveryService.Load();
        if (_pendingRecoverySnapshot is not null && !string.IsNullOrWhiteSpace(_pendingRecoverySnapshot.Folder))
        {
            CurrentFolder = _pendingRecoverySnapshot.Folder;
            IncludeSubfolders = _pendingRecoverySnapshot.IncludeSubfolders;
            StatusMessage = $"Recuperação disponível de {(_pendingRecoverySnapshot.CreatedAt):dd/MM HH:mm}. Escaneando para restaurar revisão...";
        }

        _ = ScanFilesAsync();
    }

    public ObservableCollection<MediaItem> DisplayedItems { get; }
    public ObservableCollection<MediaItem> UnwantedItems { get; }
    public ObservableCollection<DeletionCandidate> DeletionCandidates { get; }
    public ObservableCollection<DeletionCandidate> FocusedGroupItems { get; }
    public ObservableCollection<DeletionCandidate> ListReviewItems { get; }
    public IReadOnlyList<string> TypeOptions { get; }
    public IReadOnlyList<string> KeepPreferenceOptions { get; }
    public IReadOnlyList<string> DateOrganizationModes { get; }
    public IReadOnlyList<string> CleanupModes { get; }
    public IReadOnlyList<string> MediaOrganizationModes { get; }
    public IReadOnlyList<string> DeletionApplyTypeOptions { get; }
    public IReadOnlyList<string> SimpleOrganizeActions { get; }
    public IReadOnlyList<string> WorkflowObjectives { get; }
    public IReadOnlyList<string> WorkflowOrganizeActions { get; }

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ScanFilesCommand { get; }
    public RelayCommand ApplyFiltersCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ScanUnwantedCommand { get; }
    public RelayCommand MarkAllDisplayedItemsCommand { get; }
    public RelayCommand UnmarkAllDisplayedItemsCommand { get; }
    public RelayCommand MarkAllUnwantedItemsCommand { get; }
    public RelayCommand UnmarkAllUnwantedItemsCommand { get; }
    public RelayCommand GenerateDeletionListCommand { get; }
    public RelayCommand MarkAllDeletionCandidatesCommand { get; }
    public RelayCommand UnmarkAllDeletionCandidatesCommand { get; }
    public RelayCommand DeleteMarkedCandidatesCommand { get; }
    public RelayCommand CancelCurrentOperationCommand { get; }
    public RelayCommand MoveMarkedPhotosCommand { get; }
    public RelayCommand SeparateMediaCommand { get; }
    public RelayCommand OrganizePhotosByDateCommand { get; }
    public RelayCommand DeleteMarkedUnwantedCommand { get; }
    public RelayCommand ExportDeletionListCommand { get; }
    public RelayCommand ExportDeletionListTxtCommand { get; }
    public RelayCommand ApplyCleanupModeCommand { get; }
    public RelayCommand AutoSelectByGroupCommand { get; }
    public RelayCommand StartFocusedReviewCommand { get; }
    public RelayCommand ExitFocusedReviewCommand { get; }
    public RelayCommand FocusedReviewPreviousCommand { get; }
    public RelayCommand FocusedReviewNextCommand { get; }
    public RelayCommand FocusedReviewKeepCommand { get; }
    public RelayCommand FocusedReviewDeleteCommand { get; }
    public RelayCommand FocusedReviewSetOriginalCommand { get; }
    public RelayCommand FocusedReviewDeleteGroupCommand { get; }
    public RelayCommand StartListReviewCommand { get; }
    public RelayCommand ExitListReviewCommand { get; }
    public RelayCommand ListReviewSetOriginalCommand { get; }
    public RelayCommand ListReviewDeleteGroupCommand { get; }
    public RelayCommand ShowDeletionExplanationCommand { get; }
    public RelayCommand UndoLastOperationCommand { get; }
    public RelayCommand UndoSelectedOperationCommand { get; }
    public RelayCommand ToggleOrganizeModeCommand { get; }
    public RelayCommand OrganizeSimpleNextStepCommand { get; }
    public RelayCommand OrganizeSimplePreviousStepCommand { get; }
    public RelayCommand ExecuteSimpleOrganizeActionCommand { get; }
    public RelayCommand ToggleAdvancedOrganizeOptionsCommand { get; }
    public RelayCommand ToggleOrganizeAssistantVisibilityCommand { get; }
    public RelayCommand WorkflowNextStepCommand { get; }
    public RelayCommand WorkflowPreviousStepCommand { get; }
    public RelayCommand WorkflowExecuteStepCommand { get; }
    public RelayCommand WorkflowOpenFoundItemsCommand { get; }
    public RelayCommand WorkflowCloseFoundItemsCommand { get; }
    public RelayCommand WorkflowFoundItemsNextStepCommand { get; }
    public RelayCommand WorkflowRestartCommand { get; }
    public RelayCommand WorkflowOpenDeletionTabCommand { get; }
    public RelayCommand WorkflowOpenUnwantedTabCommand { get; }

    public IReadOnlyList<UndoBatch> UndoBatchOptions => _undoBatches
        .OrderByDescending(x => x.CreatedAt)
        .ToList();

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
        set
        {
            if (SetProperty(ref _includeSubfolders, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
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
                _ = LoadSelectedItemPreviewAsync(value);
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
                _ = LoadDeletionPreviewAsync(value);
                RefreshFocusedGroupContext();
                if (IsListReviewMode && value is not null && !ReferenceEquals(_selectedListReviewCandidate, value))
                {
                    _selectedListReviewCandidate = value;
                    OnPropertyChanged(nameof(SelectedListReviewCandidate));
                }
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

    public BitmapImage? KeepDeletionPreviewImage
    {
        get => _keepDeletionPreviewImage;
        private set => SetProperty(ref _keepDeletionPreviewImage, value);
    }

    public bool CompareModeEnabled
    {
        get => _compareModeEnabled;
        set => SetProperty(ref _compareModeEnabled, value);
    }

    public bool IsFocusedReviewMode
    {
        get => _isFocusedReviewMode;
        private set
        {
            if (SetProperty(ref _isFocusedReviewMode, value))
            {
                OnPropertyChanged(nameof(FocusedReviewProgressLabel));
                RaiseCommandStates();
            }
        }
    }

    public bool IsListReviewMode
    {
        get => _isListReviewMode;
        private set
        {
            if (SetProperty(ref _isListReviewMode, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string FocusedReviewProgressLabel
    {
        get
        {
            if (!IsFocusedReviewMode || _focusedReviewItems.Count == 0 || _focusedReviewIndex < 0)
            {
                return "0/0";
            }

            return $"{_focusedReviewIndex + 1}/{_focusedReviewItems.Count}";
        }
    }

    public string FocusedGroupSummary
    {
        get => _focusedGroupSummary;
        private set => SetProperty(ref _focusedGroupSummary, value);
    }

    public DeletionCandidate? SelectedFocusedGroupItem
    {
        get => _selectedFocusedGroupItem;
        set
        {
            if (!SetProperty(ref _selectedFocusedGroupItem, value))
            {
                return;
            }

            if (value is not null && !ReferenceEquals(SelectedDeletionCandidate, value))
            {
                SelectedDeletionCandidate = value;
            }

            if (value is not null && IsFocusedReviewMode && _focusedReviewItems.Count > 0)
            {
                var index = _focusedReviewItems.FindIndex(x => ReferenceEquals(x, value));
                if (index >= 0 && index != _focusedReviewIndex)
                {
                    _focusedReviewIndex = index;
                    OnPropertyChanged(nameof(FocusedReviewProgressLabel));
                }
            }

            RaiseCommandStates();
        }
    }

    public DeletionCandidate? SelectedListReviewCandidate
    {
        get => _selectedListReviewCandidate;
        set
        {
            if (!SetProperty(ref _selectedListReviewCandidate, value))
            {
                return;
            }

            if (value is not null && !ReferenceEquals(SelectedDeletionCandidate, value))
            {
                SelectedDeletionCandidate = value;
            }

            RaiseCommandStates();
        }
    }

    public string ListReviewSummary =>
        $"Itens: {ListReviewItems.Count} | Grupos: {ListReviewItems.SelectMany(x => SplitGroups(x.GroupLabel)).Distinct(StringComparer.OrdinalIgnoreCase).Count()}";

    private DeletionCandidate? CurrentFocusedReviewCandidate =>
        _focusedReviewIndex >= 0 && _focusedReviewIndex < _focusedReviewItems.Count
            ? _focusedReviewItems[_focusedReviewIndex]
            : null;

    private List<DeletionCandidate> CurrentFocusedReviewGroupAllItems =>
        GetGroupCandidates(CurrentFocusedReviewCandidate ?? SelectedDeletionCandidate);

    public string? SelectedUndoBatchId
    {
        get => _selectedUndoBatchId;
        set
        {
            if (SetProperty(ref _selectedUndoBatchId, value))
            {
                RaiseCommandStates();
            }
        }
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
        set
        {
            if (SetProperty(ref _newMoveFolderPath, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public bool IsDryRun => false;

    public string DateOrganizationMode
    {
        get => _dateOrganizationMode;
        set
        {
            if (SetProperty(ref _dateOrganizationMode, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public bool FlattenSubfoldersByDate
    {
        get => _flattenSubfoldersByDate;
        set
        {
            if (SetProperty(ref _flattenSubfoldersByDate, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public string DateOrganizationBaseFolder
    {
        get => _dateOrganizationBaseFolder;
        set
        {
            if (SetProperty(ref _dateOrganizationBaseFolder, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public bool IsSimpleOrganizeMode
    {
        get => _isSimpleOrganizeMode;
        set
        {
            if (!SetProperty(ref _isSimpleOrganizeMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAdvancedOrganizeMode));
            OnPropertyChanged(nameof(OrganizeModeToggleLabel));
            OnPropertyChanged(nameof(OrganizeAssistantToggleLabel));
            OnPropertyChanged(nameof(IsSimpleAssistPanelVisible));
            OnPropertyChanged(nameof(IsSimpleAssistHintVisible));
            RaiseCommandStates();
        }
    }

    public bool IsAdvancedOrganizeMode => !IsSimpleOrganizeMode;
    public bool IsSimpleAssistPanelVisible => IsSimpleOrganizeMode && IsOrganizeAssistantVisible;
    public bool IsSimpleAssistHintVisible => IsSimpleOrganizeMode && !IsOrganizeAssistantVisible;

    public string OrganizeModeToggleLabel => IsSimpleOrganizeMode ? "Abrir modo avançado" : "Voltar para modo guiado";

    public int OrganizeSimpleStep
    {
        get => _organizeSimpleStep;
        set
        {
            var next = Math.Clamp(value, 1, 3);
            if (!SetProperty(ref _organizeSimpleStep, next))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSimpleStep1));
            OnPropertyChanged(nameof(IsSimpleStep2));
            OnPropertyChanged(nameof(IsSimpleStep3));
            OnPropertyChanged(nameof(OrganizeSimpleStepLabel));
            OnPropertyChanged(nameof(OrganizeSimpleStepTitle));
            RaiseCommandStates();
        }
    }

    public bool IsSimpleStep1 => OrganizeSimpleStep == 1;
    public bool IsSimpleStep2 => OrganizeSimpleStep == 2;
    public bool IsSimpleStep3 => OrganizeSimpleStep == 3;

    public string OrganizeSimpleStepLabel => $"Etapa {OrganizeSimpleStep}/3";

    public string OrganizeSimpleStepTitle => OrganizeSimpleStep switch
    {
        1 => "Escolha o que fazer",
        2 => "Ajuste rápido",
        _ => "Confirmar execução"
    };

    public string SelectedSimpleOrganizeAction
    {
        get => _selectedSimpleOrganizeAction;
        set
        {
            if (!SetProperty(ref _selectedSimpleOrganizeAction, value))
            {
                return;
            }

            SyncSimpleActionToOperationMode();
            OnPropertyChanged(nameof(IsSimpleMediaAction));
            OnPropertyChanged(nameof(IsSimpleDateAction));
            OnPropertyChanged(nameof(IsSimpleMoveAction));
            OnPropertyChanged(nameof(SimpleMediaNeedsFolderName));
            OnPropertyChanged(nameof(SimpleOrganizeSummary));
        }
    }

    public bool IsSimpleMediaAction => SelectedSimpleOrganizeAction is "Separar fotos e vídeos em pastas" or "Juntar tudo em uma pasta" or "Extrair apenas vídeos";
    public bool IsSimpleDateAction => SelectedSimpleOrganizeAction == "Organizar fotos por data";
    public bool IsSimpleMoveAction => SelectedSimpleOrganizeAction == "Mover fotos marcadas";
    public bool SimpleMediaNeedsFolderName => SelectedSimpleOrganizeAction is "Juntar tudo em uma pasta" or "Extrair apenas vídeos";

    public string SimpleOrganizeSummary
    {
        get
        {
            if (IsSimpleMediaAction)
            {
                var scope = IncludeSubfolders ? "incluindo subpastas" : "somente a pasta principal";
                var folderPart = SimpleMediaNeedsFolderName
                    ? $" Pasta de destino: {MediaOperationFolderName}."
                    : string.Empty;
                return $"Você vai: {SelectedSimpleOrganizeAction}. Escopo: {scope}.{folderPart}";
            }

            if (IsSimpleDateAction)
            {
                var basePart = FlattenSubfoldersByDate ? $" Pasta base: {DateOrganizationBaseFolder}." : " Mantendo estrutura por pasta de origem.";
                return $"Você vai: Organizar fotos por data ({DateOrganizationMode}).{basePart}";
            }

            return $"Você vai: mover fotos marcadas. Destino: {NewMoveFolderPath}.";
        }
    }

    public bool IsAdvancedOrganizeOptionsOpen
    {
        get => _isAdvancedOrganizeOptionsOpen;
        set
        {
            if (SetProperty(ref _isAdvancedOrganizeOptionsOpen, value))
            {
                OnPropertyChanged(nameof(AdvancedOrganizeOptionsToggleLabel));
                RaiseCommandStates();
            }
        }
    }

    public string AdvancedOrganizeOptionsToggleLabel => IsAdvancedOrganizeOptionsOpen ? "Ocultar opções avançadas" : "Mostrar opções avançadas";

    public bool IsOrganizeAssistantVisible
    {
        get => _isOrganizeAssistantVisible;
        set
        {
            if (!SetProperty(ref _isOrganizeAssistantVisible, value))
            {
                return;
            }

            OnPropertyChanged(nameof(OrganizeAssistantToggleLabel));
            OnPropertyChanged(nameof(IsSimpleAssistPanelVisible));
            OnPropertyChanged(nameof(IsSimpleAssistHintVisible));
            RaiseCommandStates();
        }
    }

    public string OrganizeAssistantToggleLabel => IsOrganizeAssistantVisible ? "Focar na visualização" : "Mostrar assistente";

    public string CleanupMode
    {
        get => _cleanupMode;
        set => SetProperty(ref _cleanupMode, value);
    }

    public string MediaOrganizationMode
    {
        get => _mediaOrganizationMode;
        set
        {
            if (SetProperty(ref _mediaOrganizationMode, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public string MediaOperationFolderName
    {
        get => _mediaOperationFolderName;
        set
        {
            if (SetProperty(ref _mediaOperationFolderName, value))
            {
                OnPropertyChanged(nameof(SimpleOrganizeSummary));
            }
        }
    }

    public string DeletionApplyType
    {
        get => _deletionApplyType;
        set => SetProperty(ref _deletionApplyType, value);
    }

    public int SelectedMainTabIndex
    {
        get => _selectedMainTabIndex;
        set
        {
            if (!SetProperty(ref _selectedMainTabIndex, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowGlobalFolderBar));
        }
    }

    public bool ShowGlobalFolderBar => SelectedMainTabIndex != 0;

    public int WorkflowStep
    {
        get => _workflowStep;
        set
        {
            var next = Math.Clamp(value, 1, 5);
            if (!SetProperty(ref _workflowStep, next))
            {
                return;
            }

            OnPropertyChanged(nameof(IsWorkflowStep1));
            OnPropertyChanged(nameof(IsWorkflowStep2));
            OnPropertyChanged(nameof(IsWorkflowStep3));
            OnPropertyChanged(nameof(IsWorkflowStep4));
            OnPropertyChanged(nameof(IsWorkflowStep5));
            OnPropertyChanged(nameof(WorkflowStepTitle));
            OnPropertyChanged(nameof(WorkflowStepHint));
            OnPropertyChanged(nameof(IsWorkflowPreviousVisible));
            OnPropertyChanged(nameof(IsWorkflowNextVisible));
            OnPropertyChanged(nameof(IsWorkflowExecuteVisible));
            OnPropertyChanged(nameof(IsWorkflowFoundItemsNextVisible));
            OnPropertyChanged(nameof(WorkflowFoundItemsNextLabel));
            RaiseCommandStates();
        }
    }

    public string WorkflowObjective
    {
        get => _workflowObjective;
        set
        {
            if (!SetProperty(ref _workflowObjective, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsWorkflowObjectiveCleanup));
            OnPropertyChanged(nameof(IsWorkflowObjectiveUnwanted));
            OnPropertyChanged(nameof(IsWorkflowObjectiveOrganize));
            OnPropertyChanged(nameof(WorkflowStepHint));
            OnPropertyChanged(nameof(WorkflowExecuteLabel));
            OnPropertyChanged(nameof(WorkflowFoundItemsNextLabel));
            OnPropertyChanged(nameof(CanStartFocusedReviewFromWorkflow));

            if (IsWorkflowObjectiveUnwanted && DeletionApplyType != "Todos")
            {
                DeletionApplyType = "Todos";
            }
        }
    }

    public string WorkflowOrganizeAction
    {
        get => _workflowOrganizeAction;
        set
        {
            if (!SetProperty(ref _workflowOrganizeAction, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsWorkflowOrganizeActionMedia));
            OnPropertyChanged(nameof(WorkflowOrganizeNeedsFolderName));
            OnPropertyChanged(nameof(IsWorkflowOrganizeActionDate));
            OnPropertyChanged(nameof(IsWorkflowOrganizeActionMove));
        }
    }

    public bool IsWorkflowFocusedVisualization
    {
        get => _isWorkflowFocusedVisualization;
        set
        {
            if (!SetProperty(ref _isWorkflowFocusedVisualization, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsWorkflowWizardVisible));
            RaiseCommandStates();
        }
    }

    public bool IsWorkflowWizardVisible => !IsWorkflowFocusedVisualization;

    public bool HasWorkflowScanData
    {
        get => _hasWorkflowScanData;
        private set
        {
            if (!SetProperty(ref _hasWorkflowScanData, value))
            {
                return;
            }

            OnPropertyChanged(nameof(WorkflowCanVisualizeFoundItems));
            RaiseCommandStates();
        }
    }

    public string WorkflowScanSummary
    {
        get => _workflowScanSummary;
        private set => SetProperty(ref _workflowScanSummary, value);
    }

    public string WorkflowExecutionSummary
    {
        get => _workflowExecutionSummary;
        private set
        {
            if (!SetProperty(ref _workflowExecutionSummary, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanOpenDeletionListFromWorkflow));
            OnPropertyChanged(nameof(CanOpenUnwantedFromWorkflow));
            RaiseCommandStates();
        }
    }

    public bool IsWorkflowStep1 => WorkflowStep == 1;
    public bool IsWorkflowStep2 => WorkflowStep == 2;
    public bool IsWorkflowStep3 => WorkflowStep == 3;
    public bool IsWorkflowStep4 => WorkflowStep == 4;
    public bool IsWorkflowStep5 => WorkflowStep == 5;

    public bool IsWorkflowPreviousVisible => WorkflowStep > 1;
    public bool IsWorkflowNextVisible => WorkflowStep is 1 or 2 or 3;
    public bool IsWorkflowExecuteVisible => WorkflowStep == 4;
    public bool IsWorkflowFoundItemsNextVisible => IsWorkflowFocusedVisualization && WorkflowStep is 3 or 4;

    public string WorkflowStepTitle => WorkflowStep switch
    {
        1 => "Step 1: Escolher objetivo",
        2 => "Step 2: Selecionar pasta e escanear",
        3 => "Step 3: Ajustar opções",
        4 => "Step 4: Executar processo",
        _ => "Step 5: Resultado final"
    };

    public string WorkflowStepHint
    {
        get
        {
            if (WorkflowStep == 1)
            {
                return "Escolha apenas uma tarefa principal para o fluxo.";
            }

            if (WorkflowStep == 2)
            {
                return "Defina a pasta. Ao clicar em Próximo, o sistema escaneia automaticamente.";
            }

            if (WorkflowStep == 3)
            {
                return WorkflowObjective switch
                {
                    "Remover arquivos indesejados" => "Escolha as extensões. A limpeza será baseada nelas.",
                    "Fazer limpeza de fotos" => "Escolha as regras de limpeza e como manter a melhor foto.",
                    _ => "Escolha como organizar fotos e vídeos para a execução."
                };
            }

            if (WorkflowStep == 4)
            {
                return WorkflowObjective switch
                {
                    "Fazer limpeza de fotos" => "Revise as opções e gere a lista para revisão visual.",
                    "Remover arquivos indesejados" => "Revise as extensões e execute a busca dos indesejados.",
                    _ => "Revise as opções e execute a organização (sem etapa de revisão visual)."
                };
            }

            return "Confira o resultado e, se quiser, abra a revisão detalhada.";
        }
    }

    public bool IsWorkflowObjectiveCleanup => WorkflowObjective == "Fazer limpeza de fotos";
    public bool IsWorkflowObjectiveUnwanted => WorkflowObjective == "Remover arquivos indesejados";
    public bool IsWorkflowObjectiveOrganize => WorkflowObjective == "Organizar fotos e vídeos";

    public bool IsWorkflowOrganizeActionMedia => WorkflowOrganizeAction is "Separar Fotos e Vídeos" or "Unificar Fotos e Vídeos" or "Extrair Somente Vídeos";
    public bool WorkflowOrganizeNeedsFolderName => WorkflowOrganizeAction is "Unificar Fotos e Vídeos" or "Extrair Somente Vídeos";
    public bool IsWorkflowOrganizeActionDate => WorkflowOrganizeAction == "Organizar por Data";
    public bool IsWorkflowOrganizeActionMove => WorkflowOrganizeAction == "Mover Fotos Marcadas";

    public bool WorkflowCanVisualizeFoundItems => HasWorkflowScanData && _allItems.Count > 0;

    public string WorkflowExecuteLabel => WorkflowObjective switch
    {
        "Remover arquivos indesejados" => "Buscar indesejados",
        "Fazer limpeza de fotos" => "Gerar lista de exclusão",
        _ => "Executar organização"
    };

    public string WorkflowFoundItemsNextLabel => WorkflowStep == 4
        ? $"{WorkflowExecuteLabel} →"
        : "Próximo passo →";

    public bool CanOpenDeletionListFromWorkflow => IsWorkflowObjectiveCleanup && DeletionCandidates.Count > 0;
    public bool CanOpenUnwantedFromWorkflow => IsWorkflowObjectiveUnwanted && UnwantedItems.Count > 0;
    public bool CanStartFocusedReviewFromWorkflow => IsWorkflowObjectiveCleanup && DeletionCandidates.Any(x => x.CanDelete);

    private async Task NextWorkflowStepAsync()
    {
        if (WorkflowStep == 1)
        {
            WorkflowStep = 2;
            return;
        }

        if (WorkflowStep == 2)
        {
            await ScanFilesAsync();
            if (_allItems.Count == 0)
            {
                WorkflowScanSummary = "Nenhum arquivo encontrado para seguir ao próximo passo.";
                HasWorkflowScanData = true;
                return;
            }

            WorkflowStep = 3;
            return;
        }

        if (WorkflowStep == 3)
        {
            WorkflowStep = 4;
        }
    }

    private void PreviousWorkflowStep()
    {
        if (WorkflowStep > 1)
        {
            WorkflowStep--;
        }
    }

    private async Task ExecuteWorkflowStepAsync()
    {
        if (WorkflowStep != 4)
        {
            return;
        }

        var undoCountBefore = _undoBatches.Count;

        if (IsWorkflowObjectiveCleanup)
        {
            await GenerateDeletionListAsync();
            WorkflowExecutionSummary = DeletionCandidates.Count == 0
                ? "Nenhum candidato de exclusão encontrado."
                : $"Lista de exclusão gerada com {DeletionCandidates.Count} itens. Você pode abrir a aba de revisão.";
        }
        else if (IsWorkflowObjectiveUnwanted)
        {
            await ScanUnwantedAsync();
            WorkflowExecutionSummary = UnwantedItems.Count == 0
                ? "Nenhum arquivo indesejado encontrado com as extensões informadas."
                : $"Foram encontrados {UnwantedItems.Count} arquivos indesejados. Clique em 'Revisar indesejados' para marcar e excluir.";
        }
        else
        {
            await ExecuteWorkflowOrganizationAsync();
            var undoHint = _undoBatches.Count > undoCountBefore
                ? " Você pode usar 'Desfazer Última' para voltar a operação."
                : string.Empty;
            WorkflowExecutionSummary = $"{StatusMessage}{undoHint}";
        }

        WorkflowStep = 5;
        OpenWorkflowResultAutomatically();
    }

    private async Task ExecuteWorkflowOrganizationAsync()
    {
        switch (WorkflowOrganizeAction)
        {
            case "Organizar por Data":
                await OrganizePhotosByDateAsync();
                break;
            case "Mover Fotos Marcadas":
                await MoveMarkedPhotosAsync();
                break;
            default:
                MediaOrganizationMode = WorkflowOrganizeAction;
                await SeparateMediaAsync();
                break;
        }
    }

    private void RestartWorkflow()
    {
        WorkflowStep = 1;
        IsWorkflowFocusedVisualization = false;
        WorkflowExecutionSummary = string.Empty;
        StatusMessage = "Fluxo reiniciado. Escolha o objetivo no Step 1.";
    }

    private void OpenWorkflowFocusedVisualization()
    {
        if (!WorkflowCanVisualizeFoundItems)
        {
            return;
        }

        IsWorkflowFocusedVisualization = true;
    }

    private void CloseWorkflowFocusedVisualization()
    {
        IsWorkflowFocusedVisualization = false;
    }

    private async Task ProceedFromFocusedVisualizationAsync()
    {
        if (!IsWorkflowFocusedVisualization)
        {
            return;
        }

        IsWorkflowFocusedVisualization = false;

        if (WorkflowStep == 3)
        {
            WorkflowStep = 4;
            return;
        }

        if (WorkflowStep == 4)
        {
            await ExecuteWorkflowStepAsync();
        }
    }

    private void OpenDeletionListTabFromWorkflow()
    {
        SelectedMainTabIndex = 2;
    }

    private void OpenUnwantedTabFromWorkflow()
    {
        SelectedMainTabIndex = 1;
    }

    private void OpenWorkflowResultAutomatically()
    {
        if (IsWorkflowObjectiveCleanup && DeletionCandidates.Count > 0)
        {
            SelectedMainTabIndex = 2;
            StartListReviewMode();
            return;
        }

        if (IsWorkflowObjectiveUnwanted && UnwantedItems.Count > 0)
        {
            SelectedMainTabIndex = 1;
            return;
        }

        if (IsWorkflowObjectiveOrganize && DisplayedItems.Count > 0)
        {
            SelectedMainTabIndex = 0;
            OpenWorkflowFocusedVisualization();
        }
    }

    private void ToggleOrganizeMode()
    {
        IsSimpleOrganizeMode = !IsSimpleOrganizeMode;
        if (IsSimpleOrganizeMode)
        {
            OrganizeSimpleStep = 1;
            IsOrganizeAssistantVisible = true;
            StatusMessage = "Modo guiado ativado: siga as etapas para organizar.";
            return;
        }

        IsAdvancedOrganizeOptionsOpen = false;
        IsOrganizeAssistantVisible = true;
        StatusMessage = "Modo avançado ativado.";
    }

    private void AdvanceSimpleOrganizeStep()
    {
        OrganizeSimpleStep++;
    }

    private void RewindSimpleOrganizeStep()
    {
        OrganizeSimpleStep--;
    }

    private void SyncSimpleActionToOperationMode()
    {
        if (SelectedSimpleOrganizeAction == "Separar fotos e vídeos em pastas")
        {
            MediaOrganizationMode = "Separar Fotos e Vídeos";
            return;
        }

        if (SelectedSimpleOrganizeAction == "Juntar tudo em uma pasta")
        {
            MediaOrganizationMode = "Unificar Fotos e Vídeos";
            return;
        }

        if (SelectedSimpleOrganizeAction == "Extrair apenas vídeos")
        {
            MediaOrganizationMode = "Extrair Somente Vídeos";
        }
    }

    private async Task ExecuteSimpleOrganizeActionAsync()
    {
        SyncSimpleActionToOperationMode();

        switch (SelectedSimpleOrganizeAction)
        {
            case "Organizar fotos por data":
                await OrganizePhotosByDateAsync();
                break;
            case "Mover fotos marcadas":
                await MoveMarkedPhotosAsync();
                break;
            default:
                await SeparateMediaAsync();
                break;
        }
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
            if (SelectedMainTabIndex == 0 && WorkflowStep == 2)
            {
                HasWorkflowScanData = false;
                WorkflowScanSummary = "Pasta selecionada. Clique em Avançar para executar o scanner.";
                StatusMessage = "Pasta selecionada no Step 2. Clique em Avançar para escanear.";
                return;
            }

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
            var scanStart = Stopwatch.StartNew();
            var progress = new Progress<ScanProgressInfo>(p =>
            {
                if (p.Total <= 0)
                {
                    UpdateProgress(OperationProgressPercent, true, $"{p.Stage}: {p.Processed}");
                    StatusMessage = $"Escaneando arquivos... {p.Stage}: {p.Processed}";
                    return;
                }

                var percent = (double)p.Processed / p.Total * 100d;
                var etaLabel = BuildEtaLabel(percent, scanStart.Elapsed);
                UpdateProgress(percent, false, $"{p.Stage}: {p.Processed}/{p.Total} | {etaLabel}");
                StatusMessage = $"Escaneando arquivos... {p.Stage}: {p.Processed}/{p.Total} | {etaLabel}";
            });

            var scanned = await Task.Run(() => _scanner.Scan(CurrentFolder, IncludeSubfolders, cts.Token, progress), cts.Token);
            _allItems = scanned;
            RegisterScanSignature(scanned);
            ApplyFilters();
            ClearDeletionCandidates();
            StatusMessage = $"{_allItems.Count} arquivos encontrados.";
            UpdateWorkflowScanSummary();
            TryRestorePendingReviewAfterScan();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Escaneamento cancelado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao escanear: {ex.Message}";
            WorkflowScanSummary = $"Erro no escaneamento: {ex.Message}";
            HasWorkflowScanData = false;
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
            var scanStart = Stopwatch.StartNew();
            var progress = new Progress<ScanProgressInfo>(p =>
            {
                if (p.Total <= 0)
                {
                    UpdateProgress(OperationProgressPercent, true, $"{p.Stage}: {p.Processed}");
                    StatusMessage = $"Buscando arquivos indesejados... {p.Stage}: {p.Processed}";
                    return;
                }

                var percent = (double)p.Processed / p.Total * 100d;
                var etaLabel = BuildEtaLabel(percent, scanStart.Elapsed);
                UpdateProgress(percent, false, $"{p.Stage}: {p.Processed}/{p.Total} | {etaLabel}");
                StatusMessage = $"Buscando arquivos indesejados... {p.Stage}: {p.Processed}/{p.Total} | {etaLabel}";
            });

            var unwanted = await Task.Run(() => _scanner.ScanUnwantedByExtensions(CurrentFolder, IncludeSubfolders, extensions, cts.Token, progress), cts.Token);
            foreach (var item in unwanted.OrderBy(x => x.Extension).ThenBy(x => x.Name))
            {
                UnwantedItems.Add(item);
            }

            StatusMessage = $"{UnwantedItems.Count} arquivos indesejados encontrados.";
            OnPropertyChanged(nameof(CanOpenUnwantedFromWorkflow));
            RaiseCommandStates();
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
        if (!Directory.Exists(CurrentFolder))
        {
            StatusMessage = "Pasta inválida. Escolha uma pasta existente.";
            return;
        }

        var requiresAutoScan = await RequiresAutoScanForDeletionAsync();
        if (requiresAutoScan)
        {
            StatusMessage = "Executando escaneamento automático antes da lista de exclusão...";
            await ScanFilesAsync();
            if (_allItems.Count == 0)
            {
                StatusMessage = "Nenhum arquivo encontrado após o escaneamento.";
                return;
            }
        }
        else
        {
            StatusMessage = "Usando o último escaneamento (sem alterações detectadas).";
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

            var needsImageMetadata = options.UseSimilarInSequenceRule ||
                                     string.Equals(options.KeepPreference, "Maior resolução", StringComparison.OrdinalIgnoreCase);
            if (needsImageMetadata)
            {
                var enrichStart = Stopwatch.StartNew();
                var enrichProgress = new Progress<ScanProgressInfo>(p =>
                {
                    if (p.Total <= 0)
                    {
                        UpdateProgress(OperationProgressPercent, true, $"{p.Stage}: {p.Processed}");
                        StatusMessage = $"Preparando análise... {p.Stage}: {p.Processed}";
                        return;
                    }

                    var percent = Math.Clamp((double)p.Processed / p.Total, 0d, 1d) * 100d;
                    var etaLabel = BuildEtaLabel(percent, enrichStart.Elapsed);
                    UpdateProgress(percent, false, $"{p.Stage}: {p.Processed}/{p.Total} | {etaLabel}");
                    StatusMessage = $"Preparando análise... {p.Stage}: {p.Processed}/{p.Total} | {etaLabel}";
                });

                await Task.Run(() => _scanner.EnrichImageMetadata(_allItems, cts.Token, enrichProgress), cts.Token);
            }

            var currentStageIndex = -1;
            var currentStageStart = Stopwatch.StartNew();
            var progress = new Progress<AnalysisProgressInfo>(p =>
            {
                if (p.StageIndex != currentStageIndex)
                {
                    currentStageIndex = p.StageIndex;
                    currentStageStart.Restart();
                }

                var stageRatio = p.StageTotal > 0 ? (double)p.StageProcessed / p.StageTotal : 1d;
                var totalStages = Math.Max(1, p.TotalStages);
                var percent = (((double)p.StageIndex - 1d) + Math.Clamp(stageRatio, 0d, 1d)) / totalStages * 100d;
                var stageEtaLabel = BuildStageEtaLabel(p.StageProcessed, p.StageTotal, currentStageStart.Elapsed);
                UpdateProgress(percent, false, $"Etapa {p.StageIndex}/{p.TotalStages}: {p.Stage} | ETA da etapa: {stageEtaLabel}");
                StatusMessage = $"Gerando lista de exclusão... Etapa {p.StageIndex}/{p.TotalStages} - {p.Stage} ({p.StageProcessed}/{p.StageTotal}) | ETA desta etapa: {stageEtaLabel}";
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
            OnPropertyChanged(nameof(CanOpenDeletionListFromWorkflow));
            ScheduleReviewSnapshotSave();
            RaiseCommandStates();
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
        var marked = DeletionCandidates
            .Where(x => x.IsMarked && x.CanDelete && MatchesDeletionType(x.Item.Kind, DeletionApplyType))
            .ToList();
        if (marked.Count == 0)
        {
            StatusMessage = $"Nenhum item marcado para exclusão no filtro: {DeletionApplyType}.";
            return;
        }

        var bytes = marked.Sum(x => x.Item.SizeBytes);
        var modeLabel = IsDryRun ? " (Dry Run)" : string.Empty;
        var firstConfirmation = $"{modeLabel} Processar {marked.Count} arquivos da lista de exclusão?\nEspaço estimado: {FormatBytes(bytes)}.\n\nOs arquivos serão movidos para uma área segura para permitir desfazer.";
        if (!ConfirmDestructiveAction(firstConfirmation, "Confirmar exclusão"))
        {
            StatusMessage = "Exclusão cancelada pelo usuário.";
            return;
        }

        var cts = BeginOperation("Movendo arquivos para área de exclusão segura...", false);

        var deleted = 0;
        var failed = 0;
        string? firstErrorMessage = null;
        var logLines = new ConcurrentBag<string>();
        var undoBatch = CreateUndoBatch($"Exclusão lista ({marked.Count} arquivos)");
        var deletedPathsMap = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var undoEntries = new ConcurrentBag<UndoEntry>();

        try
        {
            var deleteStart = Stopwatch.StartNew();
            IProgress<(int processed, int total)> progress = new Progress<(int processed, int total)>(p =>
            {
                var percent = p.total > 0 ? (double)p.processed / p.total * 100d : 0d;
                var etaLabel = BuildEtaLabel(percent, deleteStart.Elapsed);
                var status = BuildOperationStatusLabel("Exclusão:", p.processed, p.total, etaLabel);
                UpdateProgress(percent, false, status);
                StatusMessage = _isOperationCancellationRequested
                    ? $"Cancelando exclusão... finalizando {p.processed}/{p.total}"
                    : $"Excluindo arquivos... {p.processed}/{p.total} | {etaLabel}";
            });

            await Task.Run(() =>
            {
                var total = marked.Count;
                var processed = 0;
                var trashFolder = GetUndoTrashFolder(undoBatch.Id);
                if (!IsDryRun)
                {
                    Directory.CreateDirectory(trashFolder);
                }

                Parallel.ForEach(
                    marked,
                    new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Math.Min(3, Math.Max(1, Environment.ProcessorCount / 4))
                    },
                    candidate =>
                {
                    try
                    {
                        if (File.Exists(candidate.FullPath))
                        {
                            if (!IsDryRun)
                            {
                                var trashPath = BuildUniqueTrashPath(trashFolder, candidate.FullPath);
                                File.Move(candidate.FullPath, trashPath);
                                undoEntries.Add(new UndoEntry
                                {
                                    OriginalPath = candidate.FullPath,
                                    CurrentPath = trashPath
                                });
                                deletedPathsMap.TryAdd(candidate.FullPath, 0);
                            }

                            Interlocked.Increment(ref deleted);
                            logLines.Add($"{DateTime.Now:HH:mm:ss} | {(IsDryRun ? "DRYRUN_DELETE_CANDIDATE" : "DELETE_CANDIDATE")} | {candidate.FullPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.CompareExchange(ref firstErrorMessage, ex.Message, null);
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref processed);
                        if (done % 20 == 0 || done == total)
                        {
                            progress.Report((done, total));
                        }
                    }
                });
            }, cts.Token);

            if (!IsDryRun)
            {
                foreach (var entry in undoEntries)
                {
                    undoBatch.Entries.Add(entry);
                }

                var deletedPaths = deletedPathsMap.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

                RegisterUndoBatchIfNeeded(undoBatch);
            }

            UpdateMarkedDeletionSummary();
            WriteOperationLog(logLines.ToList());
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            StatusMessage = $"{(IsDryRun ? "Dry Run concluído" : "Exclusão concluída")}. Processados: {deleted}. Falhas: {failed}.{errorSuffix}";
        }
        catch (OperationCanceledException)
        {
            if (!IsDryRun)
            {
                foreach (var entry in undoEntries)
                {
                    undoBatch.Entries.Add(entry);
                }
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            StatusMessage = BuildInterruptedOperationFallbackMessage("Exclusão", deleted, failed);
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

        var modeLabel = IsDryRun ? " (Dry Run)" : string.Empty;
        var confirm = System.Windows.MessageBox.Show(
            $"{modeLabel} Mover {markedPhotos.Count} fotos para:\n{destinationFolder}\n\nDeseja continuar?",
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
        string? firstErrorMessage = null;
        var logLines = new List<string>();
        var movedSourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var undoBatch = CreateUndoBatch($"Movimentação manual ({markedPhotos.Count} fotos)");

        try
        {
            await Task.Run(() =>
            {
                if (!IsDryRun)
                {
                    Directory.CreateDirectory(destinationFolder);
                }
                var total = markedPhotos.Count;
                var processed = 0;

                foreach (var photo in markedPhotos)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (!File.Exists(photo.FullPath))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(photo.FullPath);
                        var targetPath = GetUniqueDestinationPath(destinationFolder, fileName);
                        if (!IsDryRun)
                        {
                            File.Move(photo.FullPath, targetPath);
                            movedSourcePaths.Add(photo.FullPath);
                            undoBatch.Entries.Add(new UndoEntry
                            {
                                OriginalPath = photo.FullPath,
                                CurrentPath = targetPath
                            });
                        }

                        moved++;
                        logLines.Add($"{DateTime.Now:HH:mm:ss} | {(IsDryRun ? "DRYRUN_MOVE_PHOTO" : "MOVE_PHOTO")} | {photo.FullPath} => {targetPath}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        firstErrorMessage ??= ex.Message;
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"Movendo: {processed}/{total}");
                    }
                }
            }, cts.Token);

            if (!IsDryRun)
            {
                var movedPaths = movedSourcePaths;
                _allItems = _allItems.Where(x => !movedPaths.Contains(x.FullPath)).ToList();
                RemoveByPath(DisplayedItems, movedPaths);
                RemoveByPath(UnwantedItems, movedPaths);
                RemoveCandidatesByPath(movedPaths);
                SelectedItem = null;
                RegisterUndoBatchIfNeeded(undoBatch);
            }

            WriteOperationLog(logLines);
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            StatusMessage = $"{(IsDryRun ? "Dry Run concluído" : "Movimentação concluída")}. Movidas: {moved}. Falhas: {failed}.{errorSuffix}";
        }
        catch (OperationCanceledException)
        {
            if (!IsDryRun)
            {
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            StatusMessage = BuildInterruptedOperationFallbackMessage("Movimentação", moved, failed);
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task DeleteMarkedUnwantedAsync()
    {
        var marked = UnwantedItems
            .Where(x => x.IsMarked)
            .ToList();
        if (marked.Count == 0)
        {
            StatusMessage = "Nenhum arquivo indesejado marcado para exclusão.";
            return;
        }

        var bytes = marked.Sum(x => x.SizeBytes);
        var modeLabel = IsDryRun ? " (Dry Run)" : string.Empty;
        var firstConfirmation = $"{modeLabel} Processar {marked.Count} arquivos indesejados?\nEspaço estimado: {FormatBytes(bytes)}.\n\nOs arquivos serão movidos para área segura para permitir desfazer.";
        if (!ConfirmDestructiveAction(firstConfirmation, "Excluir indesejados"))
        {
            StatusMessage = "Exclusão de indesejados cancelada.";
            return;
        }

        var cts = BeginOperation("Processando arquivos indesejados...", false);
        var deleted = 0;
        var failed = 0;
        string? firstErrorMessage = null;
        var total = marked.Count;
        var logLines = new ConcurrentBag<string>();
        var deletedPathsMap = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var undoEntries = new ConcurrentBag<UndoEntry>();
        var undoBatch = CreateUndoBatch($"Exclusão indesejados ({marked.Count} arquivos)");

        try
        {
            var deleteStart = Stopwatch.StartNew();
            IProgress<(int processed, int total)> progress = new Progress<(int processed, int total)>(p =>
            {
                var percent = p.total > 0 ? (double)p.processed / p.total * 100d : 0d;
                var etaLabel = BuildEtaLabel(percent, deleteStart.Elapsed);
                var status = BuildOperationStatusLabel("Indesejados:", p.processed, p.total, etaLabel);
                UpdateProgress(percent, false, status);
                StatusMessage = _isOperationCancellationRequested
                    ? $"Cancelando indesejados... finalizando {p.processed}/{p.total}"
                    : $"Excluindo indesejados... {p.processed}/{p.total} | {etaLabel}";
            });

            await Task.Run(() =>
            {
                var processed = 0;
                var trashFolder = GetUndoTrashFolder(undoBatch.Id);
                if (!IsDryRun)
                {
                    Directory.CreateDirectory(trashFolder);
                }
                Parallel.ForEach(
                    marked,
                    new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Math.Min(3, Math.Max(1, Environment.ProcessorCount / 4))
                    },
                    item =>
                {
                    try
                    {
                        if (File.Exists(item.FullPath))
                        {
                            if (!IsDryRun)
                            {
                                var trashPath = BuildUniqueTrashPath(trashFolder, item.FullPath);
                                File.Move(item.FullPath, trashPath);
                                undoEntries.Add(new UndoEntry
                                {
                                    OriginalPath = item.FullPath,
                                    CurrentPath = trashPath
                                });
                                deletedPathsMap.TryAdd(item.FullPath, 0);
                            }

                            Interlocked.Increment(ref deleted);
                            logLines.Add($"{DateTime.Now:HH:mm:ss} | {(IsDryRun ? "DRYRUN_DELETE_UNWANTED" : "DELETE_UNWANTED")} | {item.FullPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.CompareExchange(ref firstErrorMessage, ex.Message, null);
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref processed);
                        if (done % 20 == 0 || done == total)
                        {
                            progress.Report((done, total));
                        }
                    }
                });
            }, cts.Token);

            if (!IsDryRun)
            {
                foreach (var entry in undoEntries)
                {
                    undoBatch.Entries.Add(entry);
                }

                var deletedPaths = deletedPathsMap.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                _allItems = _allItems.Where(x => !deletedPaths.Contains(x.FullPath)).ToList();
                RemoveByPath(DisplayedItems, deletedPaths);
                RemoveByPath(UnwantedItems, deletedPaths);
                RemoveCandidatesByPath(deletedPaths);
                RegisterUndoBatchIfNeeded(undoBatch);
            }

            WriteOperationLog(logLines.ToList());
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            StatusMessage = $"{(IsDryRun ? "Dry Run concluído" : "Exclusão concluída")} em indesejados. Processados: {deleted}. Falhas: {failed}.{errorSuffix}";
        }
        catch (OperationCanceledException)
        {
            if (!IsDryRun)
            {
                foreach (var entry in undoEntries)
                {
                    undoBatch.Entries.Add(entry);
                }
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            StatusMessage = BuildInterruptedOperationFallbackMessage("Exclusão de indesejados", deleted, failed);
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task SeparateMediaAsync()
    {
        var photos = _allItems.Where(x => x.Kind == MediaKind.Foto && File.Exists(x.FullPath)).ToList();
        var videos = _allItems.Where(x => x.Kind == MediaKind.Video && File.Exists(x.FullPath)).ToList();
        var all = photos.Concat(videos).ToList();

        if (all.Count == 0)
        {
            StatusMessage = "Nenhuma foto ou vídeo disponível para separar.";
            return;
        }

        var modeLabel = IsDryRun ? " (Dry Run)" : string.Empty;
        var normalizedFolderName = string.IsNullOrWhiteSpace(MediaOperationFolderName)
            ? "MidiaUnificada"
            : MediaOperationFolderName.Trim();

        var unifiedFolder = Path.Combine(CurrentFolder, normalizedFolderName);
        var extractedVideosFolder = Path.Combine(CurrentFolder, normalizedFolderName);
        var selectedMode = MediaOrganizationMode;

        List<MediaItem> itemsToProcess;
        Func<MediaItem, string> resolveDestinationRoot;
        string confirmText;
        string operationLabel;

        switch (selectedMode)
        {
            case "Unificar Fotos e Vídeos":
                itemsToProcess = all;
                resolveDestinationRoot = _ => unifiedFolder;
                confirmText = $"{modeLabel} Unificar {photos.Count} fotos e {videos.Count} vídeos em:\n{unifiedFolder}\n\nDeseja continuar?";
                operationLabel = "Unificação";
                break;
            case "Extrair Somente Vídeos":
                itemsToProcess = videos;
                if (itemsToProcess.Count == 0)
                {
                    StatusMessage = "Nenhum vídeo encontrado para extrair.";
                    return;
                }

                resolveDestinationRoot = _ => extractedVideosFolder;
                confirmText = $"{modeLabel} Extrair {videos.Count} vídeos para:\n{extractedVideosFolder}\n\nDeseja continuar?";
                operationLabel = "Extração de vídeos";
                break;
            default:
                var currentFolderFull = Path.GetFullPath(CurrentFolder);
                itemsToProcess = IncludeSubfolders
                    ? all.OrderByDescending(x => x.DirectoryPath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)).ToList()
                    : all.Where(x => Path.GetFullPath(x.DirectoryPath).Equals(currentFolderFull, StringComparison.OrdinalIgnoreCase)).ToList();

                if (itemsToProcess.Count == 0)
                {
                    StatusMessage = "Não há fotos ou vídeos na pasta atual para separar.";
                    return;
                }

                resolveDestinationRoot = item =>
                {
                    var localRoot = item.DirectoryPath;
                    var localFolderName = item.Kind == MediaKind.Foto ? "Fotos" : "Videos";
                    return Path.Combine(localRoot, localFolderName);
                };
                var scopeLabel = IncludeSubfolders ? "em todas as subpastas" : "apenas na pasta raiz selecionada";
                confirmText = $"{modeLabel} Separar {itemsToProcess.Count} arquivos por pasta de origem ({scopeLabel}).\nA pasta de destino será local em cada ramo (Fotos/Videos).\n\nDeseja continuar?";
                operationLabel = "Separação";
                break;
        }

        var confirm = System.Windows.MessageBox.Show(
            confirmText,
            $"Organizar mídia - {selectedMode}",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Separação cancelada pelo usuário.";
            return;
        }

        var cts = BeginOperation($"{operationLabel} de mídia...", false);
        var moved = 0;
        var failed = 0;
        string? firstErrorMessage = null;
        var logLines = new List<string>();
        var movedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var undoBatch = CreateUndoBatch($"{selectedMode} ({itemsToProcess.Count} arquivos)");

        try
        {
            await Task.Run(() =>
            {
                if (!IsDryRun)
                {
                    if (selectedMode == "Unificar Fotos e Vídeos")
                    {
                        Directory.CreateDirectory(unifiedFolder);
                    }
                    else if (selectedMode == "Extrair Somente Vídeos")
                    {
                        Directory.CreateDirectory(extractedVideosFolder);
                    }
                }

                var total = itemsToProcess.Count;
                var processed = 0;

                foreach (var item in itemsToProcess)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        var destinationRoot = resolveDestinationRoot(item);
                        var sourceDirFull = Path.GetFullPath(item.DirectoryPath);
                        var destinationRootFull = Path.GetFullPath(destinationRoot);
                        if (sourceDirFull.Equals(destinationRootFull, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var fileName = Path.GetFileName(item.FullPath);
                        var targetPath = GetUniqueDestinationPath(destinationRoot, fileName);

                        var sourceFull = Path.GetFullPath(item.FullPath);
                        var targetFull = Path.GetFullPath(targetPath);
                        if (sourceFull.Equals(targetFull, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!IsDryRun)
                        {
                            Directory.CreateDirectory(destinationRoot);
                            File.Move(item.FullPath, targetPath);
                            movedPaths.Add(item.FullPath);
                            undoBatch.Entries.Add(new UndoEntry
                            {
                                OriginalPath = item.FullPath,
                                CurrentPath = targetPath
                            });
                        }

                        moved++;
                        logLines.Add($"{DateTime.Now:HH:mm:ss} | {(IsDryRun ? "DRYRUN_ORGANIZE_MEDIA" : "ORGANIZE_MEDIA")} | Modo:{selectedMode} | {item.FullPath} => {targetPath}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        firstErrorMessage ??= ex.Message;
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"{operationLabel}: {processed}/{total}");
                    }
                }
            }, cts.Token);

            if (!IsDryRun)
            {
                _allItems = _allItems.Where(x => !movedPaths.Contains(x.FullPath)).ToList();
                RemoveByPath(DisplayedItems, movedPaths);
                RemoveByPath(UnwantedItems, movedPaths);
                RemoveCandidatesByPath(movedPaths);
                SelectedItem = null;
                RegisterUndoBatchIfNeeded(undoBatch);
            }

            WriteOperationLog(logLines);
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            StatusMessage = $"{(IsDryRun ? "Dry Run concluído" : $"{operationLabel} concluída")}. Processados: {moved}. Falhas: {failed}.{errorSuffix}";
        }
        catch (OperationCanceledException)
        {
            if (!IsDryRun)
            {
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            StatusMessage = BuildInterruptedOperationFallbackMessage("Separação", moved, failed);
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task OrganizePhotosByDateAsync()
    {
        var photos = _allItems.Where(x => x.Kind == MediaKind.Foto && File.Exists(x.FullPath)).ToList();
        if (photos.Count == 0)
        {
            StatusMessage = "Nenhuma foto encontrada para organizar por data.";
            return;
        }

        var modeLabel = IsDryRun ? " (Dry Run)" : string.Empty;
        var flattenLabel = FlattenSubfoldersByDate ? "Consolidando subpastas em base única" : "Mantendo organização por pasta de origem";
        var confirm = System.Windows.MessageBox.Show(
            $"{modeLabel} Organizar {photos.Count} fotos por {DateOrganizationMode}.\n{flattenLabel}.\n\nDeseja continuar?",
            "Organizar fotos por data",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Organização por data cancelada pelo usuário.";
            return;
        }

        var cts = BeginOperation("Organizando fotos por data...", false);
        var moved = 0;
        var failed = 0;
        string? firstErrorMessage = null;
        var logLines = new List<string>();
        var shouldRefreshAfter = false;
        var undoBatch = CreateUndoBatch($"Organizar por data ({photos.Count} fotos)");

        try
        {
            await Task.Run(() =>
            {
                var total = photos.Count;
                var processed = 0;

                string? flattenRoot = null;
                if (FlattenSubfoldersByDate)
                {
                    var baseName = string.IsNullOrWhiteSpace(DateOrganizationBaseFolder) ? "OrganizadoPorData" : DateOrganizationBaseFolder.Trim();
                    flattenRoot = Path.Combine(CurrentFolder, baseName);
                }

                foreach (var photo in photos)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        var baseRoot = flattenRoot ?? photo.DirectoryPath;
                        var destinationFolder = ResolveDateDestinationFolder(
                            photo.DirectoryPath,
                            baseRoot,
                            photo.PrimaryPhotoDate,
                            DateOrganizationMode,
                            FlattenSubfoldersByDate);
                        var fileName = Path.GetFileName(photo.FullPath);
                        var sourceFull = Path.GetFullPath(photo.FullPath);
                        var directTarget = Path.GetFullPath(Path.Combine(destinationFolder, fileName));
                        if (sourceFull.Equals(directTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var targetPath = GetUniqueDestinationPath(destinationFolder, fileName);
                        var targetFull = Path.GetFullPath(targetPath);
                        if (sourceFull.Equals(targetFull, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!IsDryRun)
                        {
                            Directory.CreateDirectory(destinationFolder);
                            File.Move(photo.FullPath, targetPath);
                            undoBatch.Entries.Add(new UndoEntry
                            {
                                OriginalPath = photo.FullPath,
                                CurrentPath = targetPath
                            });
                        }

                        moved++;
                        logLines.Add($"{DateTime.Now:HH:mm:ss} | {(IsDryRun ? "DRYRUN_ORGANIZE_BY_DATE" : "ORGANIZE_BY_DATE")} | {photo.FullPath} => {targetPath}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        firstErrorMessage ??= ex.Message;
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"Organização por data: {processed}/{total}");
                    }
                }
            }, cts.Token);

            WriteOperationLog(logLines);
            if (!IsDryRun)
            {
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            var undoHint = !IsDryRun && moved == 0 ? " Nenhum arquivo foi movido; não há desfazer para esta operação." : string.Empty;
            StatusMessage = $"{(IsDryRun ? "Dry Run concluído" : "Organização concluída")} por data. Processados: {moved}. Falhas: {failed}.{errorSuffix}{undoHint}";
            shouldRefreshAfter = !IsDryRun;
        }
        catch (OperationCanceledException)
        {
            if (!IsDryRun)
            {
                RegisterUndoBatchIfNeeded(undoBatch);
            }
            StatusMessage = BuildInterruptedOperationFallbackMessage("Organização por data", moved, failed);
        }
        finally
        {
            EndOperation(cts);
            if (shouldRefreshAfter)
            {
                _ = ScanFilesAsync();
            }
        }
    }

    private void ExportDeletionListCsv()
    {
        if (DeletionCandidates.Count == 0)
        {
            StatusMessage = "Não há itens na lista de exclusão para exportar.";
            return;
        }

        var exportDir = Path.Combine(Environment.CurrentDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var filePath = Path.Combine(exportDir, $"deletion-list-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Marcado;PodeExcluir;Estado;Nome;Extensao;Tamanho;Similaridade;Grupo;Regra;Motivo;ArquivoManter;Caminho");
        foreach (var c in DeletionCandidates)
        {
            sb.AppendLine(string.Join(";",
                c.IsMarked ? "Sim" : "Nao",
                c.CanDelete ? "Sim" : "Nao",
                EscapeCsv(c.DeletionStateLabel),
                EscapeCsv(c.Name),
                EscapeCsv(c.Extension),
                EscapeCsv(c.SizeLabel),
                EscapeCsv(c.SimilarityLabel),
                EscapeCsv(c.GroupLabel),
                EscapeCsv(c.Rule),
                EscapeCsv(c.Reason),
                EscapeCsv(c.KeepFilePath),
                EscapeCsv(c.FullPath)));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        StatusMessage = $"Lista de exclusão exportada em: {filePath}";
    }

    private void ExportDeletionListTxt()
    {
        if (DeletionCandidates.Count == 0)
        {
            StatusMessage = "Não há itens na lista de exclusão para exportar.";
            return;
        }

        var exportDir = Path.Combine(Environment.CurrentDirectory, "exports");
        Directory.CreateDirectory(exportDir);
        var filePath = Path.Combine(exportDir, $"deletion-list-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var sb = new StringBuilder();
        sb.AppendLine("LISTA DE EXCLUSAO");
        sb.AppendLine($"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine(new string('-', 80));

        foreach (var c in DeletionCandidates)
        {
            sb.AppendLine($"Marcado: {(c.IsMarked ? "Sim" : "Nao")} | PodeExcluir: {(c.CanDelete ? "Sim" : "Nao")} | Estado: {c.DeletionStateLabel}");
            sb.AppendLine($"Nome: {c.Name}{c.Extension}");
            sb.AppendLine($"Tamanho: {c.SizeLabel}");
            sb.AppendLine($"Similaridade: {c.SimilarityLabel}");
            sb.AppendLine($"Grupo: {c.GroupLabel}");
            sb.AppendLine($"Regra: {c.Rule}");
            sb.AppendLine($"Motivo: {c.Reason}");
            sb.AppendLine($"Manter: {c.KeepFilePath}");
            sb.AppendLine($"Caminho: {c.FullPath}");
            sb.AppendLine(new string('-', 80));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        StatusMessage = $"Lista de exclusão TXT exportada em: {filePath}";
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

    private void StartFocusedReviewMode()
    {
        if (IsListReviewMode)
        {
            ExitListReviewMode(silent: true);
        }

        var candidates = DeletionCandidates
            .Where(x => x.CanDelete)
            .ToList();
        if (candidates.Count == 0)
        {
            StatusMessage = "Não há candidatos para revisão focada.";
            return;
        }

        _focusedReviewItems = candidates;
        var selectedIndex = SelectedDeletionCandidate is null
            ? -1
            : _focusedReviewItems.FindIndex(x => ReferenceEquals(x, SelectedDeletionCandidate));
        _focusedReviewIndex = selectedIndex >= 0 ? selectedIndex : 0;
        IsFocusedReviewMode = true;
        SyncFocusedReviewSelection();
        ScheduleReviewSnapshotSave();
        StatusMessage = "Modo revisão focada iniciado.";
    }

    private void StartListReviewMode()
    {
        if (IsFocusedReviewMode)
        {
            ExitFocusedReviewMode(silent: true);
        }

        if (DeletionCandidates.Count == 0)
        {
            StatusMessage = "Não há itens na lista de exclusão para revisão em lista.";
            return;
        }

        IsListReviewMode = true;
        RebuildListReviewItems();
        SelectedListReviewCandidate = ListReviewItems.FirstOrDefault();
        ScheduleReviewSnapshotSave();
        StatusMessage = "Modo revisão em lista iniciado.";
    }

    private void ExitFocusedReviewMode()
    {
        ExitFocusedReviewMode(false);
    }

    private void ExitFocusedReviewMode(bool silent)
    {
        IsFocusedReviewMode = false;
        _focusedReviewItems = [];
        _focusedReviewIndex = -1;
        FocusedGroupItems.Clear();
        SelectedFocusedGroupItem = null;
        FocusedGroupSummary = "Grupo: 0 itens";
        OnPropertyChanged(nameof(FocusedReviewProgressLabel));
        if (!silent)
        {
            StatusMessage = "Modo revisão focada finalizado.";
        }
        ScheduleReviewSnapshotSave();
        RaiseCommandStates();
    }

    private void ExitListReviewMode()
    {
        ExitListReviewMode(false);
    }

    private void ExitListReviewMode(bool silent)
    {
        IsListReviewMode = false;
        ListReviewItems.Clear();
        SelectedListReviewCandidate = null;
        OnPropertyChanged(nameof(ListReviewSummary));
        if (!silent)
        {
            StatusMessage = "Modo revisão em lista finalizado.";
        }
        ScheduleReviewSnapshotSave();
        RaiseCommandStates();
    }

    private void MoveFocusedReview(int delta)
    {
        if (!IsFocusedReviewMode || _focusedReviewItems.Count == 0)
        {
            return;
        }

        var next = _focusedReviewIndex + delta;
        if (next < 0)
        {
            next = 0;
        }
        else if (next >= _focusedReviewItems.Count)
        {
            next = _focusedReviewItems.Count - 1;
        }

        _focusedReviewIndex = next;
        SyncFocusedReviewSelection();
    }

    private void SetFocusedReviewMark(bool markDelete)
    {
        var current = CurrentFocusedReviewCandidate;
        if (current is null)
        {
            return;
        }

        current.IsMarked = markDelete && current.CanDelete;
        StatusMessage = markDelete ? "Foto marcada para exclusão." : "Foto marcada para manter.";
        if (_focusedReviewIndex < _focusedReviewItems.Count - 1)
        {
            _focusedReviewIndex++;
            SyncFocusedReviewSelection();
            return;
        }

        OnPropertyChanged(nameof(FocusedReviewProgressLabel));
    }

    private void SyncFocusedReviewSelection()
    {
        var current = CurrentFocusedReviewCandidate;
        if (current is not null)
        {
            SelectedDeletionCandidate = current;
        }

        OnPropertyChanged(nameof(FocusedReviewProgressLabel));
        RaiseCommandStates();
    }

    private void RefreshFocusedGroupContext()
    {
        if (!IsFocusedReviewMode)
        {
            return;
        }

        var groupItems = CurrentFocusedReviewGroupAllItems
            .OrderBy(x => x.CanDelete) // original protegida primeiro
            .ThenByDescending(x => x.Item.PrimaryPhotoDate)
            .ToList();

        FocusedGroupItems.Clear();
        foreach (var item in groupItems.Take(MaxFocusedGroupItems))
        {
            FocusedGroupItems.Add(item);
        }

        SelectedFocusedGroupItem = groupItems.Count == 0
            ? null
            : groupItems.FirstOrDefault(x => ReferenceEquals(x, SelectedDeletionCandidate)) ?? groupItems[0];

        var truncated = groupItems.Count > MaxFocusedGroupItems
            ? $" (mostrando {MaxFocusedGroupItems})"
            : string.Empty;
        FocusedGroupSummary = $"Grupo atual: {groupItems.Count} item(ns){truncated}";
    }

    private void RebuildListReviewItems()
    {
        ListReviewItems.Clear();
        foreach (var item in DeletionCandidates
                     .OrderBy(x => GetPrimaryGroupKey(x))
                     .ThenBy(x => x.CanDelete)
                     .ThenByDescending(x => x.Item.PrimaryPhotoDate)
                     .ThenBy(x => x.Name))
        {
            ListReviewItems.Add(item);
        }

        OnPropertyChanged(nameof(ListReviewSummary));
    }

    private void SetFocusedReviewItemAsOriginal()
    {
        var candidate = SelectedFocusedGroupItem;
        if (candidate is null)
        {
            return;
        }

        if (!TrySetOriginalForGroup(candidate))
        {
            return;
        }

        StatusMessage = "Original do grupo alterada na revisão focada.";
        RefreshFocusedGroupContext();
    }

    private void SetListReviewItemAsOriginal()
    {
        var candidate = SelectedListReviewCandidate;
        if (candidate is null)
        {
            return;
        }

        if (!TrySetOriginalForGroup(candidate))
        {
            return;
        }

        StatusMessage = "Original do grupo alterada na revisão em lista.";
        RebuildListReviewItems();
        SelectedListReviewCandidate = candidate;
    }

    private async Task DeleteCurrentGroupAsync()
    {
        var groupItems = CurrentFocusedReviewGroupAllItems;
        if (groupItems.Count == 0)
        {
            StatusMessage = "Nenhum grupo disponível para exclusão.";
            return;
        }

        await DeleteEntireGroupAsync(groupItems, "revisão focada");
        RefreshFocusedGroupContext();
    }

    private async Task DeleteSelectedListGroupAsync()
    {
        var groupItems = GetGroupCandidates(SelectedListReviewCandidate);
        if (groupItems.Count == 0)
        {
            StatusMessage = "Nenhum grupo disponível para exclusão.";
            return;
        }

        await DeleteEntireGroupAsync(groupItems, "revisão em lista");
        RebuildListReviewItems();
        SelectedListReviewCandidate = ListReviewItems.FirstOrDefault();
    }

    private async Task DeleteEntireGroupAsync(IReadOnlyList<DeletionCandidate> groupItems, string sourceLabel)
    {
        var total = groupItems.Count;
        var totalBytes = groupItems.Sum(x => x.Item.SizeBytes);
        var confirm = System.Windows.MessageBox.Show(
            $"Você vai excluir TODAS as {total} fotos deste grupo.\nEspaço estimado: {FormatBytes(totalBytes)}.\n\nAtenção: este conjunto inteiro será perdido.\nDeseja continuar?",
            "Excluir grupo inteiro",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Exclusão do grupo cancelada.";
            return;
        }

        var confirmFinal = System.Windows.MessageBox.Show(
            "Confirma novamente a exclusão de todas as fotos do grupo?",
            "Excluir grupo inteiro - confirmação final",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmFinal != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Exclusão do grupo cancelada.";
            return;
        }

        await DeleteCandidatesDirectAsync(groupItems, $"Exclusão de grupo ({sourceLabel})");
    }

    private List<DeletionCandidate> GetGroupCandidates(DeletionCandidate? anchor)
    {
        if (anchor is null)
        {
            return [];
        }

        var key = GetPrimaryGroupKey(anchor);
        if (string.IsNullOrWhiteSpace(key))
        {
            return [anchor];
        }

        return DeletionCandidates
            .Where(x => SplitGroups(x.GroupLabel).Any(g => g.Equals(key, StringComparison.OrdinalIgnoreCase)))
            .Distinct()
            .ToList();
    }

    private static string GetPrimaryGroupKey(DeletionCandidate candidate)
    {
        return SplitGroups(candidate.GroupLabel).FirstOrDefault() ?? candidate.GroupLabel;
    }

    private bool TrySetOriginalForGroup(DeletionCandidate newOriginal)
    {
        var groupItems = GetGroupCandidates(newOriginal);
        if (groupItems.Count <= 1)
        {
            StatusMessage = "Este item não possui grupo para troca de original.";
            return false;
        }

        foreach (var item in groupItems)
        {
            item.KeepFilePath = newOriginal.FullPath;
            item.CanDelete = !ReferenceEquals(item, newOriginal);
            if (!item.CanDelete)
            {
                item.IsMarked = false;
            }
        }

        UpdateMarkedDeletionSummary();
        ScheduleReviewSnapshotSave();
        RaiseCommandStates();
        return true;
    }

    private void SetAllDisplayedItemsMarked(bool marked)
    {
        foreach (var item in DisplayedItems)
        {
            item.IsMarked = marked;
        }

        StatusMessage = marked
            ? $"{DisplayedItems.Count} itens da aba Organizar marcados."
            : "Todos os itens da aba Organizar foram desmarcados.";
        RaiseCommandStates();
    }

    private void SetAllUnwantedItemsMarked(bool marked)
    {
        foreach (var item in UnwantedItems)
        {
            item.IsMarked = marked;
        }

        StatusMessage = marked
            ? $"{UnwantedItems.Count} itens da aba Indesejados marcados."
            : "Todos os itens da aba Indesejados foram desmarcados.";
        RaiseCommandStates();
    }

    private void ApplyCleanupMode()
    {
        switch (CleanupMode)
        {
            case "Conservador":
                UseSameNameRule = true;
                UseSameSizeRule = false;
                UseSimilarInSequenceRule = false;
                SimilarSecondsWindow = "5";
                SimilarDistanceThreshold = "5";
                break;
            case "Agressivo":
                UseSameNameRule = true;
                UseSameSizeRule = true;
                UseSimilarInSequenceRule = true;
                SimilarSecondsWindow = "30";
                SimilarDistanceThreshold = "12";
                break;
            default:
                UseSameNameRule = true;
                UseSameSizeRule = true;
                UseSimilarInSequenceRule = false;
                SimilarSecondsWindow = "10";
                SimilarDistanceThreshold = "8";
                break;
        }

        StatusMessage = $"Modo de limpeza aplicado: {CleanupMode}.";
    }

    private void AutoSelectByGroup()
    {
        foreach (var candidate in DeletionCandidates)
        {
            candidate.IsMarked = false;
        }

        var groups = DeletionCandidates
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupLabel))
            .SelectMany(x => SplitGroups(x.GroupLabel).Select(g => (Group: g, Item: x)))
            .GroupBy(x => x.Group, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var all = group.Select(x => x.Item).Distinct().ToList();
            var keeper = all.FirstOrDefault(x => !x.CanDelete)
                         ?? all.OrderByDescending(x => x.Item.ResolutionPixels)
                               .ThenByDescending(x => x.Item.SizeBytes)
                               .ThenByDescending(x => x.Item.PrimaryPhotoDate)
                               .FirstOrDefault();

            foreach (var item in all)
            {
                item.IsMarked = item.CanDelete && !ReferenceEquals(item, keeper);
            }
        }

        UpdateMarkedDeletionSummary();
        StatusMessage = "Seleção automática aplicada por conjunto: 1 mantida e restantes marcadas.";
    }

    private void ShowDeletionExplanation()
    {
        var candidate = SelectedDeletionCandidate;
        if (candidate is null)
        {
            System.Windows.MessageBox.Show(
                "Selecione um item da lista para visualizar a explicação.",
                "Explicação",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var text = new StringBuilder();
        text.AppendLine($"Nome: {candidate.Name}{candidate.Extension}");
        text.AppendLine($"Estado: {candidate.DeletionStateLabel}");
        text.AppendLine($"Similaridade: {candidate.SimilarityLabel}");
        text.AppendLine($"Regra: {candidate.Rule}");
        text.AppendLine($"Motivo: {candidate.Reason}");
        text.AppendLine($"Arquivo manter: {candidate.KeepFilePath}");
        text.AppendLine($"Caminho: {candidate.FullPath}");

        System.Windows.MessageBox.Show(
            text.ToString(),
            "Explicação",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
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
            ScheduleReviewSnapshotSave();
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
            .OrderByDescending(x => x.PrimaryPhotoDate)
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

    private async Task LoadSelectedItemPreviewAsync(MediaItem? item)
    {
        CancelPreviewLoad(ref _selectedItemPreviewCts);
        var cts = new CancellationTokenSource();
        _selectedItemPreviewCts = cts;
        PreviewImage = null;

        try
        {
            await Task.Delay(PreviewDebounceMs, cts.Token);
            if (item?.IsImage == true && (!item.Width.HasValue || !item.Height.HasValue || !item.OriginalTakenTime.HasValue))
            {
                await Task.Run(() => _scanner.TryEnrichImageMetadata(item, cts.Token), cts.Token);
            }

            var image = await Task.Run(() => LoadPreviewImage(item?.FullPath, item?.IsImage == true, cts.Token), cts.Token);
            if (!ReferenceEquals(_selectedItemPreviewCts, cts) || cts.IsCancellationRequested)
            {
                return;
            }

            PreviewImage = image;
            MaybeNotifyHeicCodecMissing(item?.FullPath, image);
        }
        catch (OperationCanceledException)
        {
            // seleção mudou: ignora
        }
    }

    private async Task DeleteCandidatesDirectAsync(IReadOnlyList<DeletionCandidate> targets, string operationLabel)
    {
        if (targets.Count == 0)
        {
            StatusMessage = "Nenhum arquivo para excluir.";
            return;
        }

        var cts = BeginOperation("Movendo arquivos do grupo para área de exclusão segura...", false);
        var deleted = 0;
        var failed = 0;
        string? firstErrorMessage = null;
        var logLines = new ConcurrentBag<string>();
        var undoBatch = CreateUndoBatch($"{operationLabel} ({targets.Count} arquivos)");
        var deletedPathsMap = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var undoEntries = new ConcurrentBag<UndoEntry>();

        try
        {
            var deleteStart = Stopwatch.StartNew();
            IProgress<(int processed, int total)> progress = new Progress<(int processed, int total)>(p =>
            {
                var percent = p.total > 0 ? (double)p.processed / p.total * 100d : 0d;
                var etaLabel = BuildEtaLabel(percent, deleteStart.Elapsed);
                var status = BuildOperationStatusLabel("Grupo:", p.processed, p.total, etaLabel);
                UpdateProgress(percent, false, status);
                StatusMessage = _isOperationCancellationRequested
                    ? $"Cancelando exclusão do grupo... finalizando {p.processed}/{p.total}"
                    : $"Excluindo grupo... {p.processed}/{p.total} | {etaLabel}";
            });

            await Task.Run(() =>
            {
                var total = targets.Count;
                var processed = 0;
                var trashFolder = GetUndoTrashFolder(undoBatch.Id);
                Directory.CreateDirectory(trashFolder);

                Parallel.ForEach(
                    targets,
                    new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Math.Min(3, Math.Max(1, Environment.ProcessorCount / 4))
                    },
                    candidate =>
                {
                        try
                        {
                            if (!File.Exists(candidate.FullPath))
                            {
                                return;
                            }

                            var trashPath = BuildUniqueTrashPath(trashFolder, candidate.FullPath);
                            File.Move(candidate.FullPath, trashPath);
                            undoEntries.Add(new UndoEntry
                            {
                                OriginalPath = candidate.FullPath,
                                CurrentPath = trashPath
                            });
                            deletedPathsMap.TryAdd(candidate.FullPath, 0);
                            Interlocked.Increment(ref deleted);
                            logLines.Add($"{DateTime.Now:HH:mm:ss} | DELETE_GROUP | {candidate.FullPath}");
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);
                            Interlocked.CompareExchange(ref firstErrorMessage, ex.Message, null);
                        }
                        finally
                        {
                            var done = Interlocked.Increment(ref processed);
                            if (done % 10 == 0 || done == total)
                            {
                                progress.Report((done, total));
                            }
                        }
                    });
            }, cts.Token);

            foreach (var entry in undoEntries)
            {
                undoBatch.Entries.Add(entry);
            }

            var deletedPaths = deletedPathsMap.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

            RegisterUndoBatchIfNeeded(undoBatch);
            UpdateMarkedDeletionSummary();
            WriteOperationLog(logLines.ToList());
            var errorSuffix = !string.IsNullOrWhiteSpace(firstErrorMessage) ? $" Primeiro erro: {firstErrorMessage}" : string.Empty;
            StatusMessage = $"Exclusão do grupo concluída. Processados: {deleted}. Falhas: {failed}.{errorSuffix}";
        }
        catch (OperationCanceledException)
        {
            foreach (var entry in undoEntries)
            {
                undoBatch.Entries.Add(entry);
            }

            RegisterUndoBatchIfNeeded(undoBatch);
            StatusMessage = BuildInterruptedOperationFallbackMessage("Exclusão do grupo", deleted, failed);
        }
        finally
        {
            EndOperation(cts);
        }
    }

    private async Task LoadDeletionPreviewAsync(DeletionCandidate? candidate)
    {
        CancelPreviewLoad(ref _deletionPreviewCts);
        var cts = new CancellationTokenSource();
        _deletionPreviewCts = cts;
        DeletionPreviewImage = null;
        KeepDeletionPreviewImage = null;

        try
        {
            await Task.Delay(PreviewDebounceMs, cts.Token);
            var selectedPath = candidate?.FullPath;
            var canLoadSelected = candidate?.Item.IsImage == true;
            var keepPath = candidate?.KeepFilePath;
            var canLoadKeep = candidate?.Item.IsImage == true && !string.IsNullOrWhiteSpace(keepPath);

            if (candidate?.Item.IsImage == true && (!candidate.Item.Width.HasValue || !candidate.Item.Height.HasValue || !candidate.Item.OriginalTakenTime.HasValue))
            {
                await Task.Run(() => _scanner.TryEnrichImageMetadata(candidate.Item, cts.Token), cts.Token);
            }

            if (!string.IsNullOrWhiteSpace(keepPath))
            {
                var keepItem = _allItems.FirstOrDefault(x => x.FullPath.Equals(keepPath, StringComparison.OrdinalIgnoreCase));
                if (keepItem is not null && keepItem.IsImage && (!keepItem.Width.HasValue || !keepItem.Height.HasValue || !keepItem.OriginalTakenTime.HasValue))
                {
                    await Task.Run(() => _scanner.TryEnrichImageMetadata(keepItem, cts.Token), cts.Token);
                }
            }

            var selectedTask = Task.Run(() => LoadPreviewImage(selectedPath, canLoadSelected, cts.Token), cts.Token);
            var keepTask = Task.Run(() => LoadPreviewImage(keepPath, canLoadKeep, cts.Token), cts.Token);
            await Task.WhenAll(selectedTask, keepTask);

            if (!ReferenceEquals(_deletionPreviewCts, cts) || cts.IsCancellationRequested)
            {
                return;
            }

            DeletionPreviewImage = selectedTask.Result;
            KeepDeletionPreviewImage = keepTask.Result;
            MaybeNotifyHeicCodecMissing(selectedPath, DeletionPreviewImage);
        }
        catch (OperationCanceledException)
        {
            // seleção mudou: ignora
        }
    }

    private void MaybeNotifyHeicCodecMissing(string? path, BitmapImage? loadedImage)
    {
        if (loadedImage is not null || !IsHeicLikePath(path))
        {
            return;
        }

        var normalized = Path.GetFullPath(path!);
        if (string.Equals(_lastHeicCodecWarningPath, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastHeicCodecWarningPath = normalized;
        StatusMessage = "Não foi possível visualizar HEIC/HEIF. Instale no Windows: 'HEIF Image Extensions' (e, se necessário, 'HEVC Video Extensions').";
    }

    private static BitmapImage? LoadPreviewImage(string? path, bool canLoad, CancellationToken cancellationToken)
    {
        if (!canLoad || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = PreviewDecodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static void CancelPreviewLoad(ref CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
            // ignora
        }
        finally
        {
            cts.Dispose();
            cts = null;
        }
    }

    private void ClearDeletionCandidates()
    {
        ExitFocusedReviewMode(silent: true);
        ExitListReviewMode(silent: true);
        CancelPreviewLoad(ref _deletionPreviewCts);
        foreach (var item in DeletionCandidates)
        {
            item.PropertyChanged -= CandidateOnPropertyChanged;
        }

        DeletionCandidates.Clear();
        SelectedDeletionCandidate = null;
        DeletionPreviewImage = null;
        KeepDeletionPreviewImage = null;
        UpdateMarkedDeletionSummary();
        _reviewRecoveryService.Clear();
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

        if (IsFocusedReviewMode && _focusedReviewItems.Count > 0)
        {
            _focusedReviewItems = _focusedReviewItems
                .Where(x => !deletedPaths.Contains(x.FullPath))
                .ToList();

            if (_focusedReviewItems.Count == 0)
            {
                ExitFocusedReviewMode(silent: true);
            }
            else
            {
                if (_focusedReviewIndex >= _focusedReviewItems.Count)
                {
                    _focusedReviewIndex = _focusedReviewItems.Count - 1;
                }

                SyncFocusedReviewSelection();
            }
        }

        if (IsListReviewMode)
        {
            RebuildListReviewItems();
            if (SelectedListReviewCandidate is not null && deletedPaths.Contains(SelectedListReviewCandidate.FullPath))
            {
                SelectedListReviewCandidate = ListReviewItems.FirstOrDefault();
            }
        }

        UpdateMarkedDeletionSummary();
        ScheduleReviewSnapshotSave();
    }

    private void TryRestorePendingReviewAfterScan()
    {
        if (_pendingRecoverySnapshot is null || _isRestoringRecovery)
        {
            return;
        }

        if (!string.Equals(_pendingRecoverySnapshot.Folder, CurrentFolder, StringComparison.OrdinalIgnoreCase) ||
            _pendingRecoverySnapshot.IncludeSubfolders != IncludeSubfolders)
        {
            return;
        }

        var snapshot = _pendingRecoverySnapshot;
        _pendingRecoverySnapshot = null;

        var confirm = System.Windows.MessageBox.Show(
            $"Foi encontrada uma revisão salva de {snapshot.CreatedAt:dd/MM/yyyy HH:mm:ss}.\nDeseja restaurar agora?",
            "Recuperar revisão anterior",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            _reviewRecoveryService.Clear();
            return;
        }

        RestoreReviewSnapshot(snapshot);
    }

    private void RestoreReviewSnapshot(ReviewRecoverySnapshot snapshot)
    {
        _isRestoringRecovery = true;
        try
        {
            ClearDeletionCandidates();
            var byPath = _allItems.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);
            foreach (var saved in snapshot.Candidates)
            {
                if (!byPath.TryGetValue(saved.FullPath, out var item))
                {
                    continue;
                }

                var candidate = new DeletionCandidate
                {
                    Item = item,
                    Reason = saved.Reason,
                    Rule = saved.Rule,
                    KeepFilePath = saved.KeepFilePath,
                    GroupLabel = saved.GroupLabel,
                    CanDelete = saved.CanDelete,
                    SimilarityPercent = saved.SimilarityPercent,
                    IsMarked = saved.IsMarked
                };

                candidate.PropertyChanged += CandidateOnPropertyChanged;
                DeletionCandidates.Add(candidate);
            }

            if (DeletionCandidates.Count == 0)
            {
                _reviewRecoveryService.Clear();
                StatusMessage = "Snapshot de revisão encontrado, mas nenhum item pôde ser restaurado.";
                return;
            }

            var selected = !string.IsNullOrWhiteSpace(snapshot.SelectedCandidatePath)
                ? DeletionCandidates.FirstOrDefault(x => x.FullPath.Equals(snapshot.SelectedCandidatePath, StringComparison.OrdinalIgnoreCase))
                : null;
            SelectedDeletionCandidate = selected ?? DeletionCandidates[0];

            if (snapshot.IsListReviewMode)
            {
                StartListReviewMode();
            }
            else if (snapshot.IsFocusedReviewMode)
            {
                StartFocusedReviewMode();
            }

            UpdateMarkedDeletionSummary();
            RaiseCommandStates();
            StatusMessage = $"Revisão restaurada com {DeletionCandidates.Count} itens.";
            ScheduleReviewSnapshotSave();
        }
        finally
        {
            _isRestoringRecovery = false;
        }
    }

    private void ScheduleReviewSnapshotSave()
    {
        if (_isRestoringRecovery)
        {
            return;
        }

        _recoverySaveCts?.Cancel();
        _recoverySaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _recoverySaveCts = cts;
        _ = SaveReviewSnapshotDebouncedAsync(cts);
    }

    private async Task SaveReviewSnapshotDebouncedAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(1200, cts.Token);
            if (!ReferenceEquals(_recoverySaveCts, cts) || cts.IsCancellationRequested)
            {
                return;
            }

            SaveOrClearReviewSnapshot();
        }
        catch (OperationCanceledException)
        {
            // ignora debounce cancelado
        }
    }

    private void SaveOrClearReviewSnapshot()
    {
        if (_isRestoringRecovery || DeletionCandidates.Count == 0)
        {
            _reviewRecoveryService.Clear();
            return;
        }

        var snapshot = new ReviewRecoverySnapshot
        {
            Folder = CurrentFolder,
            IncludeSubfolders = IncludeSubfolders,
            CreatedAt = DateTime.Now,
            IsFocusedReviewMode = IsFocusedReviewMode,
            IsListReviewMode = IsListReviewMode,
            SelectedCandidatePath = SelectedDeletionCandidate?.FullPath,
            Candidates = DeletionCandidates.Select(x => new ReviewRecoveryCandidate
            {
                FullPath = x.FullPath,
                Reason = x.Reason,
                Rule = x.Rule,
                KeepFilePath = x.KeepFilePath,
                GroupLabel = x.GroupLabel,
                CanDelete = x.CanDelete,
                IsMarked = x.IsMarked,
                SimilarityPercent = x.SimilarityPercent
            }).ToList()
        };

        _reviewRecoveryService.Save(snapshot);
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
        _isOperationCancellationRequested = false;
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
        _isOperationCancellationRequested = false;
        UpdateProgress(0, false, string.Empty);
        IsBusy = false;
        RaiseCommandStates();
    }

    private void CancelCurrentOperation()
    {
        if (_operationCts is null || _isOperationCancellationRequested)
        {
            return;
        }

        _isOperationCancellationRequested = true;
        _operationCts?.Cancel();
        StatusMessage = "Cancelando operação... finalizando itens em andamento.";
        UpdateProgress(OperationProgressPercent, true, "Cancelando... finalizando operações em andamento.");
    }

    private void UpdateProgress(double percent, bool indeterminate, string label)
    {
        OperationProgressPercent = Math.Clamp(percent, 0, 100);
        IsProgressIndeterminate = indeterminate;
        OperationProgressLabel = label;
    }

    private static string BuildInterruptedOperationFallbackMessage(string operationName, int processed, int failed)
    {
        return $"{operationName} interrompida. Processados até o momento: {processed}. Falhas: {failed}. Use 'Desfazer Última' para recuperar itens já movidos para área segura.";
    }

    private string BuildOperationStatusLabel(string runningLabel, int processed, int total, string etaLabel)
    {
        if (_isOperationCancellationRequested)
        {
            return $"Cancelando... finalizando {processed}/{total}";
        }

        return $"{runningLabel} {processed}/{total} | {etaLabel}";
    }

    private static string BuildEtaLabel(double percent, TimeSpan elapsed)
    {
        if (percent >= 99.9)
        {
            return $"Tempo decorrido: {FormatTimeSpan(elapsed)} | ETA: concluindo";
        }

        if (percent <= 1d || elapsed.TotalSeconds < 2)
        {
            return $"Tempo decorrido: {FormatTimeSpan(elapsed)} | ETA: calculando";
        }

        var progressFraction = percent / 100d;
        var estimatedTotal = TimeSpan.FromTicks((long)(elapsed.Ticks / progressFraction));
        var remaining = estimatedTotal - elapsed;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return $"Tempo decorrido: {FormatTimeSpan(elapsed)} | ETA: {FormatTimeSpan(remaining)}";
    }

    private static string BuildStageEtaLabel(int processed, int total, TimeSpan elapsed)
    {
        if (total <= 0 || processed <= 0 || elapsed.TotalSeconds < 2)
        {
            return "calculando";
        }

        if (processed >= total)
        {
            return "concluindo";
        }

        var fraction = (double)processed / total;
        var estimatedTotal = TimeSpan.FromTicks((long)(elapsed.Ticks / fraction));
        var remaining = estimatedTotal - elapsed;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return FormatTimeSpan(remaining);
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return value.ToString(@"hh\:mm\:ss");
        }

        return value.ToString(@"mm\:ss");
    }

    private static bool ConfirmDestructiveAction(string firstMessage, string title)
    {
        var confirm1 = System.Windows.MessageBox.Show(
            firstMessage,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm1 != System.Windows.MessageBoxResult.Yes)
        {
            return false;
        }

        var confirm2 = System.Windows.MessageBox.Show(
            "Confirma novamente esta exclusão em massa? Esta ação poderá ser desfeita, mas recomendamos revisar a lista antes.",
            $"{title} - confirmação final",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return confirm2 == System.Windows.MessageBoxResult.Yes;
    }

    private void RaiseCommandStates()
    {
        _scanFilesCommand.RaiseCanExecuteChanged();
        _scanUnwantedCommand.RaiseCanExecuteChanged();
        _markAllDisplayedItemsCommand.RaiseCanExecuteChanged();
        _unmarkAllDisplayedItemsCommand.RaiseCanExecuteChanged();
        _markAllUnwantedItemsCommand.RaiseCanExecuteChanged();
        _unmarkAllUnwantedItemsCommand.RaiseCanExecuteChanged();
        _generateDeletionListCommand.RaiseCanExecuteChanged();
        _deleteMarkedCandidatesCommand.RaiseCanExecuteChanged();
        _moveMarkedPhotosCommand.RaiseCanExecuteChanged();
        _separateMediaCommand.RaiseCanExecuteChanged();
        _organizePhotosByDateCommand.RaiseCanExecuteChanged();
        _deleteMarkedUnwantedCommand.RaiseCanExecuteChanged();
        _exportDeletionListCommand.RaiseCanExecuteChanged();
        _exportDeletionListTxtCommand.RaiseCanExecuteChanged();
        _autoSelectByGroupCommand.RaiseCanExecuteChanged();
        _startFocusedReviewCommand.RaiseCanExecuteChanged();
        _exitFocusedReviewCommand.RaiseCanExecuteChanged();
        _focusedReviewPreviousCommand.RaiseCanExecuteChanged();
        _focusedReviewNextCommand.RaiseCanExecuteChanged();
        _focusedReviewKeepCommand.RaiseCanExecuteChanged();
        _focusedReviewDeleteCommand.RaiseCanExecuteChanged();
        _focusedReviewSetOriginalCommand.RaiseCanExecuteChanged();
        _focusedReviewDeleteGroupCommand.RaiseCanExecuteChanged();
        _startListReviewCommand.RaiseCanExecuteChanged();
        _exitListReviewCommand.RaiseCanExecuteChanged();
        _listReviewSetOriginalCommand.RaiseCanExecuteChanged();
        _listReviewDeleteGroupCommand.RaiseCanExecuteChanged();
        _undoLastOperationCommand.RaiseCanExecuteChanged();
        _undoSelectedOperationCommand.RaiseCanExecuteChanged();
        _toggleOrganizeModeCommand.RaiseCanExecuteChanged();
        _organizeSimpleNextStepCommand.RaiseCanExecuteChanged();
        _organizeSimplePreviousStepCommand.RaiseCanExecuteChanged();
        _executeSimpleOrganizeActionCommand.RaiseCanExecuteChanged();
        _toggleAdvancedOrganizeOptionsCommand.RaiseCanExecuteChanged();
        _toggleOrganizeAssistantVisibilityCommand.RaiseCanExecuteChanged();
        _workflowNextStepCommand.RaiseCanExecuteChanged();
        _workflowPreviousStepCommand.RaiseCanExecuteChanged();
        _workflowExecuteStepCommand.RaiseCanExecuteChanged();
        _workflowOpenFoundItemsCommand.RaiseCanExecuteChanged();
        _workflowCloseFoundItemsCommand.RaiseCanExecuteChanged();
        _workflowFoundItemsNextStepCommand.RaiseCanExecuteChanged();
        _workflowRestartCommand.RaiseCanExecuteChanged();
        _workflowOpenDeletionTabCommand.RaiseCanExecuteChanged();
        _workflowOpenUnwantedTabCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(CanOpenDeletionListFromWorkflow));
        OnPropertyChanged(nameof(CanStartFocusedReviewFromWorkflow));
    }

    private void UpdateWorkflowScanSummary()
    {
        var total = _allItems.Count;
        var photos = _allItems.Count(x => x.Kind == MediaKind.Foto);
        var videos = _allItems.Count(x => x.Kind == MediaKind.Video);
        var others = total - photos - videos;
        WorkflowScanSummary = $"Encontrados {total} arquivos (Fotos: {photos}, Vídeos: {videos}, Outros: {others}).";
        HasWorkflowScanData = true;
        OnPropertyChanged(nameof(WorkflowCanVisualizeFoundItems));
        RaiseCommandStates();
    }

    private UndoBatch CreateUndoBatch(string description)
    {
        return new UndoBatch
        {
            Description = description
        };
    }

    private static string GetUndoTrashFolder(string batchId)
    {
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManipuladorFotos",
            "undo-trash");
        return Path.Combine(baseFolder, batchId);
    }

    private void RegisterUndoBatchIfNeeded(UndoBatch batch)
    {
        if (batch.Entries.Count == 0)
        {
            return;
        }

        _undoBatches.Add(batch);
        _undoHistoryService.Save(_undoBatches);
        RefreshUndoSelection();
        RaiseCommandStates();
    }

    private async Task UndoLastOperationAsync()
    {
        if (_undoBatches.Count == 0)
        {
            StatusMessage = "Nenhuma operação disponível para desfazer.";
            return;
        }

        var batch = _undoBatches[^1];
        await UndoBatchAsync(batch, "Desfazer a última operação?");
    }

    private async Task UndoSelectedOperationAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedUndoBatchId))
        {
            StatusMessage = "Selecione um lote para desfazer.";
            return;
        }

        var batch = _undoBatches.FirstOrDefault(x => x.Id.Equals(SelectedUndoBatchId, StringComparison.OrdinalIgnoreCase));
        if (batch is null)
        {
            StatusMessage = "Lote selecionado não encontrado no histórico.";
            return;
        }

        await UndoBatchAsync(batch, "Desfazer o lote selecionado?");
    }

    private async Task UndoBatchAsync(UndoBatch batch, string prompt)
    {
        var confirm = System.Windows.MessageBox.Show(
            $"{prompt}\n{batch.Description}\nItens: {batch.Entries.Count}",
            "Desfazer operação",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "Desfazer cancelado.";
            return;
        }

        var cts = BeginOperation("Desfazendo última operação...", false);
        var restored = 0;
        var failed = 0;
        var logLines = new List<string>();
        var remainingEntries = new List<UndoEntry>();

        try
        {
            await Task.Run(() =>
            {
                var total = batch.Entries.Count;
                var processed = 0;

                for (var i = batch.Entries.Count - 1; i >= 0; i--)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var entry = batch.Entries[i];
                    try
                    {
                        if (!File.Exists(entry.CurrentPath))
                        {
                            failed++;
                            remainingEntries.Add(entry);
                            continue;
                        }

                        var originalDir = Path.GetDirectoryName(entry.OriginalPath);
                        if (string.IsNullOrWhiteSpace(originalDir))
                        {
                            failed++;
                            remainingEntries.Add(entry);
                            continue;
                        }

                        Directory.CreateDirectory(originalDir);
                        var restorePath = entry.OriginalPath;
                        if (File.Exists(restorePath))
                        {
                            restorePath = GetUniqueDestinationPath(originalDir, Path.GetFileName(entry.OriginalPath));
                        }

                        File.Move(entry.CurrentPath, restorePath);
                        restored++;
                        logLines.Add($"{DateTime.Now:HH:mm:ss} | UNDO | {entry.CurrentPath} => {restorePath}");
                    }
                    catch
                    {
                        failed++;
                        remainingEntries.Add(entry);
                    }
                    finally
                    {
                        processed++;
                        var percent = total > 0 ? (double)processed / total * 100d : 0d;
                        UpdateProgress(percent, false, $"Desfazendo: {processed}/{total}");
                    }
                }
            }, cts.Token);

            var batchIndex = _undoBatches.FindIndex(x => x.Id.Equals(batch.Id, StringComparison.OrdinalIgnoreCase));
            if (batchIndex < 0)
            {
                StatusMessage = "Lote não encontrado no histórico durante o desfazer.";
                return;
            }

            if (remainingEntries.Count == 0)
            {
                _undoBatches.RemoveAt(batchIndex);
            }
            else
            {
                remainingEntries.Reverse();
                batch.Entries = remainingEntries;
                _undoBatches[batchIndex] = batch;
            }

            _undoHistoryService.Save(_undoBatches);
            RefreshUndoSelection();
            WriteOperationLog(logLines);
            StatusMessage = $"Desfazer concluído. Restaurados: {restored}. Falhas: {failed}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Desfazer cancelado.";
        }
        finally
        {
            EndOperation(cts);
            RaiseCommandStates();
            _ = ScanFilesAsync();
        }
    }

    private void RefreshUndoSelection()
    {
        OnPropertyChanged(nameof(UndoBatchOptions));
        if (_undoBatches.Count == 0)
        {
            SelectedUndoBatchId = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedUndoBatchId) &&
            _undoBatches.Any(x => x.Id.Equals(SelectedUndoBatchId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedUndoBatchId = _undoBatches[^1].Id;
    }

    private void RegisterScanSignature(IReadOnlyCollection<MediaItem> scannedItems)
    {
        _lastScanFolder = CurrentFolder;
        _lastScanIncludeSubfolders = IncludeSubfolders;
        _lastScanFileCount = scannedItems.Count;
        _lastScanLatestWriteUtcTicks = scannedItems.Count == 0
            ? 0
            : scannedItems.Max(x => x.LastWriteTime.ToUniversalTime().Ticks);
    }

    private async Task<bool> RequiresAutoScanForDeletionAsync()
    {
        if (_allItems.Count == 0)
        {
            return true;
        }

        if (!string.Equals(_lastScanFolder, CurrentFolder, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_lastScanIncludeSubfolders != IncludeSubfolders)
        {
            return true;
        }

        if (_lastScanFileCount < 0 || _lastScanLatestWriteUtcTicks < 0)
        {
            return true;
        }

        try
        {
            var snapshot = await Task.Run(() => CaptureCurrentFolderSnapshot(CurrentFolder, IncludeSubfolders));
            return snapshot.FileCount != _lastScanFileCount ||
                   snapshot.LatestWriteUtcTicks != _lastScanLatestWriteUtcTicks;
        }
        catch
        {
            return true;
        }
    }

    private static FolderSnapshot CaptureCurrentFolderSnapshot(string folderPath, bool includeSubfolders)
    {
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var count = 0;
        long latestWrite = 0;

        foreach (var path in Directory.EnumerateFiles(folderPath, "*.*", searchOption))
        {
            if (IsInternalAppPath(path))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                _ = info.Length;
                count++;
                var ticks = info.LastWriteTimeUtc.Ticks;
                if (ticks > latestWrite)
                {
                    latestWrite = ticks;
                }
            }
            catch
            {
                // Ignora arquivos sem acesso durante verificação.
            }
        }

        return new FolderSnapshot(count, latestWrite);
    }

    private static bool IsInternalAppPath(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}{InternalFolderName}{Path.DirectorySeparatorChar}";
        return normalized.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct FolderSnapshot(int FileCount, long LatestWriteUtcTicks);

    private static string EscapeCsv(string? value)
    {
        var raw = value ?? string.Empty;
        var escaped = raw.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static void WriteOperationLog(IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, $"operations-{DateTime.Now:yyyy-MM-dd}.log");
        File.AppendAllLines(logFile, list);
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

    private static string BuildUniqueTrashPath(string folder, string sourcePath)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        return Path.Combine(folder, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static string BuildDatePath(DateTime date, string mode)
    {
        return Path.Combine(BuildDatePathSegments(date, mode).ToArray());
    }

    private static IEnumerable<string> BuildDatePathSegments(DateTime date, string mode)
    {
        yield return date.ToString("yyyy");

        if (mode is "Ano/Mês" or "Ano/Mês/Dia")
        {
            yield return date.ToString("MM");
        }

        if (mode == "Ano/Mês/Dia")
        {
            yield return date.ToString("dd");
        }
    }

    private static string ResolveDateDestinationFolder(
        string sourceDirectory,
        string baseRoot,
        DateTime photoDate,
        string mode,
        bool flattenSubfoldersByDate)
    {
        var segments = BuildDatePathSegments(photoDate, mode).ToArray();
        if (segments.Length == 0)
        {
            return baseRoot;
        }

        if (!flattenSubfoldersByDate && DirectoryEndsWithSegments(sourceDirectory, segments))
        {
            return sourceDirectory;
        }

        return Path.Combine(baseRoot, Path.Combine(segments));
    }

    private static bool DirectoryEndsWithSegments(string directoryPath, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            return true;
        }

        var normalized = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < segments.Count)
        {
            return false;
        }

        var offset = parts.Length - segments.Count;
        for (var i = 0; i < segments.Count; i++)
        {
            if (!string.Equals(parts[offset + i], segments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> SplitGroups(string groupLabel)
    {
        return groupLabel
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static bool MatchesDeletionType(MediaKind kind, string applyType)
    {
        return applyType switch
        {
            "Somente Fotos" => kind == MediaKind.Foto,
            "Somente Vídeos" => kind == MediaKind.Video,
            _ => true
        };
    }

    private static bool IsHeicLikePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);
    }
}
