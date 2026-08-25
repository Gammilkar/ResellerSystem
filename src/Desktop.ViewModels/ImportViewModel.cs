using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>One target field's mapping row in the UI — pick which column
/// from the uploaded file (if any) fills this field.</summary>
public sealed partial class MappingFieldRow : ObservableObject
{
    public required ImportTargetFieldDto Field { get; init; }
    public required ObservableCollection<string> AvailableColumns { get; init; }

    [ObservableProperty]
    private string? _selectedColumn;
}

/// <summary>
/// Product Specification sections 57-61: pick an .xlsx file, map its
/// columns to the target fields (or load a saved template), upload for
/// staging/validation/preview, then confirm to actually create
/// Purchases/Items/Listings/Sales/Fees/Returns/Expenses.
/// </summary>
public sealed partial class ImportViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly IFilePickerService _filePicker;
    private readonly INavigationService _navigation;

    public ImportViewModel(IServerApiClient apiClient, IFilePickerService filePicker, INavigationService navigation)
    {
        _apiClient = apiClient;
        _filePicker = filePicker;
        _navigation = navigation;
    }

    public ObservableCollection<string> SourceColumns { get; } = new();
    public ObservableCollection<string> SheetNames { get; } = new();
    public ObservableCollection<MappingFieldRow> MappingRows { get; } = new();
    public ObservableCollection<ImportMappingTemplateDto> Templates { get; } = new();
    public ObservableCollection<ImportStagingRowDto> PreviewRows { get; } = new();

    [ObservableProperty] private string? _selectedFilePath;
    [ObservableProperty] private string? _selectedSheetName;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _newTemplateName = string.Empty;
    [ObservableProperty] private ImportMappingTemplateDto? _selectedTemplate;
    [ObservableProperty] private Guid? _currentBatchId;
    [ObservableProperty] private bool _hasColumns;
    [ObservableProperty] private bool _hasBatch;
    [ObservableProperty] private bool _hasMultipleSheets;
    [ObservableProperty] private int _rowCount;
    [ObservableProperty] private int _validRowCount;
    [ObservableProperty] private string? _confirmResultSummary;

    private bool _suppressSheetChangeReload;

    private const string ImportType = "xlsx-full";

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            var fields = await _apiClient.GetImportTargetFieldsAsync();
            MappingRows.Clear();
            foreach (var f in fields)
            {
                MappingRows.Add(new MappingFieldRow { Field = f, AvailableColumns = SourceColumns });
            }

            var templates = await _apiClient.ListImportMappingTemplatesAsync(ImportType);
            Templates.Clear();
            foreach (var t in templates) Templates.Add(t);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        PreviewRows.Clear();
        CurrentBatchId = null;
        HasBatch = false;
        HasColumns = false;
        ConfirmResultSummary = null;

        var path = await _filePicker.PickFileAsync("Choose an Excel file", "xlsx");
        if (path is null) return;

        SelectedFilePath = path;
        await InspectAsync(sheetName: null);
    }

    /// <summary>Re-inspects the already-chosen file, either to auto-pick a
    /// sheet (sheetName null, first call after ChooseFileAsync) or because
    /// the user picked a different sheet from SheetNames.</summary>
    private async Task InspectAsync(string? sheetName)
    {
        if (SelectedFilePath is null) return;

        IsBusy = true;
        try
        {
            var result = await _apiClient.InspectXlsxAsync(SelectedFilePath, sheetName);

            _suppressSheetChangeReload = true;
            SheetNames.Clear();
            foreach (var s in result.SheetNames) SheetNames.Add(s);
            HasMultipleSheets = SheetNames.Count > 1;
            SelectedSheetName = result.SelectedSheet;
            _suppressSheetChangeReload = false;

            SourceColumns.Clear();
            SourceColumns.Add("(none)");
            foreach (var c in result.Columns) SourceColumns.Add(c);

            // System-suggested mapping (see ImportColumnMatcher server-side)
            // — the whole point is the user shouldn't have to configure
            // this by hand for an ordinary tracker export; every row stays
            // fully editable below regardless.
            foreach (var row in MappingRows)
            {
                row.SelectedColumn = result.SuggestedMapping.TryGetValue(row.Field.Key, out var col) ? col : "(none)";
            }

            HasColumns = true;
            var matched = result.SuggestedMapping.Count;
            StatusMessage = $"Sheet '{result.SelectedSheet}': found {result.Columns.Count} column(s), auto-matched {matched}. Review below, then Upload.";
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSheetNameChanged(string? value)
    {
        if (_suppressSheetChangeReload || value is null) return;
        _ = InspectAsync(value);
    }

    partial void OnSelectedTemplateChanged(ImportMappingTemplateDto? value)
    {
        if (value is null) return;
        foreach (var row in MappingRows)
        {
            row.SelectedColumn = value.Mapping.TryGetValue(row.Field.Key, out var col) && SourceColumns.Contains(col)
                ? col
                : "(none)";
        }
    }

    private Dictionary<string, string> BuildMapping() =>
        MappingRows.Where(r => !string.IsNullOrWhiteSpace(r.SelectedColumn) && r.SelectedColumn != "(none)")
            .ToDictionary(r => r.Field.Key, r => r.SelectedColumn!);

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTemplateName)) return;

        ErrorMessage = null;
        try
        {
            var saved = await _apiClient.SaveImportMappingTemplateAsync(new SaveMappingTemplateRequest
            {
                Name = NewTemplateName,
                ImportType = ImportType,
                Mapping = BuildMapping()
            });

            var existing = Templates.FirstOrDefault(t => t.Id == saved.Id);
            if (existing is not null) Templates.Remove(existing);
            Templates.Add(saved);
            NewTemplateName = string.Empty;
            StatusMessage = "Template saved.";
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (SelectedFilePath is null) return;

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var mapping = BuildMapping();
            var batch = await _apiClient.UploadXlsxAsync(SelectedFilePath, SelectedSheetName, mapping);
            ApplyBatch(batch);
            StatusMessage = $"Staged {batch.RowCount} row(s), {batch.ValidRowCount} ready to import. Review below, then Confirm.";
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Details.Count > 0 ? string.Join(" ", ex.Error.Details) : ex.Error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (CurrentBatchId is null) return;

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _apiClient.ConfirmImportAsync(CurrentBatchId.Value);
            ConfirmResultSummary =
                $"Created: {result.CreatedPurchaseCount} purchase(s), {result.CreatedItemCount} item(s), " +
                $"{result.CreatedListingCount} listing(s), {result.CreatedSaleCount} sale(s), {result.CreatedReturnCount} return(s). " +
                $"Skipped {result.SkippedRowCount} row(s).";
            PreviewRows.Clear();
            CurrentBatchId = null;
            HasBatch = false;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyBatch(ImportBatchDto batch)
    {
        CurrentBatchId = batch.Id;
        HasBatch = true;
        RowCount = batch.RowCount;
        ValidRowCount = batch.ValidRowCount;
        PreviewRows.Clear();
        foreach (var row in batch.Rows) PreviewRows.Add(row);
    }

    [RelayCommand]
    private void Back() => _navigation.ShowDashboard();
}
