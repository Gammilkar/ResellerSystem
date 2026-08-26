using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// "Новое поступление" / edit-existing-purchase screen — the full
/// purchase-intake workflow (Product Specification §1-24): header +
/// Reseller Permit block + financial summary + a multi-line Items grid
/// (each line can carry Quantity > 1) + expense lines + documents +
/// validation summary, one Save that creates the Purchase and explodes
/// every line into its physical Items atomically (PurchaseService.
/// CreateAsync/UpdateAsync).
///
/// Live-as-you-type preview (§13) is intentionally a "Пересчитать" button
/// rather than a debounced auto-call on every keystroke — this app has no
/// existing debounce infrastructure, and recalculating on every single
/// property change would be noisy; Save always recalculates once more
/// server-side regardless; a small deliberate scope simplification, not a
/// prototype shortcut, is called out is fine per the codebase's convention
/// of documenting such choices rather than hiding them.
///
/// Documents attach only once the Purchase has a real Id — for a new
/// purchase that means after the first Save, matching how most apps handle
/// "attach a file to a record that doesn't exist yet."
/// </summary>
public sealed partial class PurchaseEditViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigation;

    private Guid? _purchaseId;

    public PurchaseEditViewModel(IServerApiClient apiClient, IDialogService dialogService,
        IFilePickerService filePickerService, INavigationService navigation)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
        _navigation = navigation;
    }

    /// <summary>Called by NavigationService right after resolving this VM —
    /// null purchaseId means "new purchase."</summary>
    public void Initialize(Guid? purchaseId)
    {
        _purchaseId = purchaseId;
        IsNew = purchaseId is null;
        ScreenTitle = IsNew ? "Новое поступление" : "Редактирование закупки";
        _ = LoadAsync();
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isNew = true;
    [ObservableProperty] private string _screenTitle = "Новое поступление";

    // ── Purchase Details ─────────────────────────────────────────────
    [ObservableProperty] private DateOnly _purchaseDateValue = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string _sourceName = string.Empty;
    [ObservableProperty] private string? _sourceType;
    [ObservableProperty] private string _purchaseType = "TaxPaid";
    [ObservableProperty] private string? _paymentMethod;
    [ObservableProperty] private string? _comment;

    public bool ShowResellerPermitBlock => PurchaseType == "ResellerPermit";
    partial void OnPurchaseTypeChanged(string value) => OnPropertyChanged(nameof(ShowResellerPermitBlock));

    [ObservableProperty] private string? _permitNumber;
    [ObservableProperty] private DateOnly? _permitDate;
    [ObservableProperty] private string _taxExemptAmountText = string.Empty;

    // ── Financial Summary ────────────────────────────────────────────
    /// <summary>Blank defaults to Merchandise Subtotal server-side.</summary>
    [ObservableProperty] private string _taxableAmountText = string.Empty;
    [ObservableProperty] private string _salesTaxRateText = string.Empty;
    [ObservableProperty] private string _salesTaxAmountOverrideText = string.Empty;
    [ObservableProperty] private string _salesTaxAllocationMethod = "Proportional";
    [ObservableProperty] private string _expenseAllocationMethod = "Proportional";
    [ObservableProperty] private string _manualAdjustmentText = "0";

    public ObservableCollection<PurchaseExpenseLineEditViewModel> ExpenseLines { get; } = new();
    [ObservableProperty] private string _newExpenseType = "Other";
    [ObservableProperty] private string _newExpenseAmountText = string.Empty;
    [ObservableProperty] private string? _newExpenseNotes;

    // ── Items in Purchase ─────────────────────────────────────────────
    public ObservableCollection<PurchaseLineEditViewModel> ItemLines { get; } = new();
    [ObservableProperty] private PurchaseLineEditViewModel? _selectedItemLine;

    // ── Reference lists ───────────────────────────────────────────────
    public ObservableCollection<string> PurchaseSourceOptions { get; } = new();
    public ObservableCollection<string> PurchaseTypeOptions { get; } = new();
    public ObservableCollection<string> PaymentMethodOptions { get; } = new();
    public ObservableCollection<string> CategoryOptions { get; } = new();
    public ObservableCollection<string> ExpenseTypeOptions { get; } = new();
    public string[] AllocationMethodOptions { get; } = { "Proportional", "EqualPerUnit", "Manual" };

    // ── Validation Summary ────────────────────────────────────────────
    [ObservableProperty] private decimal _merchandiseSubtotal;
    [ObservableProperty] private decimal _salesTaxAmount;
    [ObservableProperty] private decimal _totalExpenses;
    [ObservableProperty] private decimal _totalPurchaseCost;
    [ObservableProperty] private decimal _allocatedTotal;
    [ObservableProperty] private decimal _difference;
    [ObservableProperty] private int _physicalItemsToCreate;
    [ObservableProperty] private bool _isReadyToSave;
    [ObservableProperty] private string _validationSummaryText = "Нажмите «Пересчитать», чтобы увидеть распределение.";

    // ── Documents ──────────────────────────────────────────────────────
    public ObservableCollection<DocumentDto> Documents { get; } = new();
    public bool CanManageDocuments => _purchaseId is not null;

    // ── Save result ────────────────────────────────────────────────────
    [ObservableProperty] private bool _showSaveResult;
    [ObservableProperty] private string _saveResultText = string.Empty;

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await LoadReferenceListsAsync();

            if (_purchaseId is { } id)
            {
                var detail = await _apiClient.GetPurchaseFullAsync(id);
                ApplyDetail(detail);
                await LoadDocumentsAsync();
            }
            else
            {
                ItemLines.Add(new PurchaseLineEditViewModel());
            }
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadReferenceListsAsync()
    {
        await LoadOneListAsync(ReferenceListKeysMirror.PurchaseSource, PurchaseSourceOptions);
        await LoadOneListAsync(ReferenceListKeysMirror.PurchaseType, PurchaseTypeOptions);
        await LoadOneListAsync(ReferenceListKeysMirror.PaymentMethod, PaymentMethodOptions);
        await LoadOneListAsync(ReferenceListKeysMirror.Category, CategoryOptions);
        await LoadOneListAsync(ReferenceListKeysMirror.ExpenseType, ExpenseTypeOptions);
    }

    private async Task LoadOneListAsync(string listKey, ObservableCollection<string> target)
    {
        try
        {
            var values = await _apiClient.ListReferenceValuesAsync(listKey);
            target.Clear();
            foreach (var v in values) target.Add(v.Value);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    private async Task LoadDocumentsAsync()
    {
        if (_purchaseId is not { } id) return;
        try
        {
            var docs = await _apiClient.ListDocumentsForEntityAsync("Purchase", id);
            Documents.Clear();
            foreach (var d in docs) Documents.Add(d);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    private void ApplyDetail(PurchaseDetailDto d)
    {
        PurchaseDateValue = d.PurchaseDate;
        SourceName = d.SourceName;
        SourceType = d.SourceType;
        PurchaseType = d.PurchaseType;
        PaymentMethod = d.PaymentMethod;
        Comment = d.Comment;
        PermitNumber = d.PermitNumber;
        PermitDate = d.PermitDate;
        TaxExemptAmountText = d.TaxExemptAmount?.ToString() ?? string.Empty;
        TaxableAmountText = d.TaxableAmount.ToString();
        SalesTaxRateText = d.SalesTaxRate?.ToString() ?? string.Empty;
        SalesTaxAmountOverrideText = d.SalesTaxIsManualOverride ? d.SalesTaxAmount.ToString() : string.Empty;
        SalesTaxAllocationMethod = d.SalesTaxAllocationMethod;
        ExpenseAllocationMethod = d.ExpenseAllocationMethod;
        ManualAdjustmentText = d.ManualAdjustment.ToString();

        ExpenseLines.Clear();
        foreach (var e in d.ExpenseLines)
        {
            ExpenseLines.Add(new PurchaseExpenseLineEditViewModel { ExpenseType = e.ExpenseType, Amount = e.Amount, Notes = e.Notes });
        }

        ItemLines.Clear();
        foreach (var l in d.ItemLines)
        {
            ItemLines.Add(new PurchaseLineEditViewModel
            {
                Id = l.Id,
                ItemName = l.ItemName,
                CategoryName = l.CategoryName,
                Quantity = l.Quantity,
                UnitPurchaseCost = l.UnitPurchaseCost,
                Notes = l.Notes,
                ManualAllocatedSalesTaxText = l.ManualAllocatedSalesTax?.ToString(),
                ManualAllocatedExpensesText = l.ManualAllocatedExpenses?.ToString(),
                LinePurchaseCost = l.LinePurchaseCost,
                AllocatedSalesTax = l.AllocatedSalesTax,
                AllocatedExpenses = l.AllocatedExpenses,
                FinalLineCostBasis = l.FinalLineCostBasis,
                ItemNumbersText = string.Join(", ", l.ItemNumbers)
            });
        }

        MerchandiseSubtotal = d.MerchandiseSubtotal;
        SalesTaxAmount = d.SalesTaxAmount;
        TotalExpenses = d.ExpenseLines.Sum(e => e.Amount);
        TotalPurchaseCost = d.TotalAmount;
        AllocatedTotal = d.ItemLines.Sum(l => l.FinalLineCostBasis);
        Difference = Math.Round(TotalPurchaseCost - AllocatedTotal, 2, MidpointRounding.AwayFromZero);
        PhysicalItemsToCreate = d.TotalItemCount;
        IsReadyToSave = Difference == 0;
        ValidationSummaryText = BuildSummaryText();
        OnPropertyChanged(nameof(CanManageDocuments));
    }

    [RelayCommand]
    private async Task OpenPurchaseDateAsync()
    {
        var vm = new DatePickerDialogViewModel("Дата покупки", PurchaseDateValue);
        var newDate = await _dialogService.ShowAsync<DatePickerDialogViewModel, DateOnly?>(vm);
        if (newDate is { } d) PurchaseDateValue = d;
    }

    [RelayCommand]
    private async Task OpenPermitDateAsync()
    {
        var vm = new DatePickerDialogViewModel("Permit Date", PermitDate);
        var newDate = await _dialogService.ShowAsync<DatePickerDialogViewModel, DateOnly?>(vm);
        if (newDate is { } d) PermitDate = d;
    }

    // ── Reference list "+ Add" ────────────────────────────────────────
    [RelayCommand]
    private Task AddPurchaseSourceAsync() => AddReferenceValueAsync(ReferenceListKeysMirror.PurchaseSource, "Новый источник закупки", PurchaseSourceOptions, v => SourceName = v);

    [RelayCommand]
    private Task AddPaymentMethodAsync() => AddReferenceValueAsync(ReferenceListKeysMirror.PaymentMethod, "Новый способ оплаты", PaymentMethodOptions, v => PaymentMethod = v);

    [RelayCommand]
    private Task AddExpenseTypeAsync() => AddReferenceValueAsync(ReferenceListKeysMirror.ExpenseType, "Новый тип расхода", ExpenseTypeOptions, v => NewExpenseType = v);

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var categoryName = SelectedItemLine?.ItemName is { } ? "Новая категория" : "Новая категория";
        await AddReferenceValueAsync(ReferenceListKeysMirror.Category, categoryName, CategoryOptions,
            v => { if (SelectedItemLine is not null) SelectedItemLine.CategoryName = v; });
    }

    private async Task AddReferenceValueAsync(string listKey, string title, ObservableCollection<string> target, Action<string> onAdded)
    {
        var dialogVm = new TextInputDialogViewModel(title, "Введите новое значение:");
        var value = await _dialogService.ShowAsync<TextInputDialogViewModel, string>(dialogVm);
        if (string.IsNullOrWhiteSpace(value)) return;

        ErrorMessage = null;
        try
        {
            var created = await _apiClient.CreateReferenceValueAsync(new CreateReferenceListValueRequest { ListKey = listKey, Value = value });
            if (!target.Contains(created.Value)) target.Add(created.Value);
            onAdded(created.Value);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    // ── Items grid ─────────────────────────────────────────────────────
    [RelayCommand]
    private void AddItemLine() => ItemLines.Add(new PurchaseLineEditViewModel { Quantity = 1 });

    [RelayCommand]
    private void DuplicateLine()
    {
        if (SelectedItemLine is null) return;
        var index = ItemLines.IndexOf(SelectedItemLine);
        ItemLines.Insert(index + 1, SelectedItemLine.Clone());
    }

    [RelayCommand]
    private void RemoveLine()
    {
        if (SelectedItemLine is not null) ItemLines.Remove(SelectedItemLine);
    }

    [RelayCommand]
    private void AddExpenseLine()
    {
        if (!decimal.TryParse(NewExpenseAmountText, out var amount))
        {
            ErrorMessage = "Сумма расхода должна быть числом.";
            return;
        }
        ExpenseLines.Add(new PurchaseExpenseLineEditViewModel { ExpenseType = NewExpenseType, Amount = amount, Notes = NewExpenseNotes });
        NewExpenseAmountText = string.Empty;
        NewExpenseNotes = null;
    }

    [RelayCommand]
    private void RemoveExpenseLine(PurchaseExpenseLineEditViewModel line) => ExpenseLines.Remove(line);

    // ── Allocation preview / save ────────────────────────────────────
    private bool TryBuildRequest(out CreatePurchaseFullRequest request, out string? error)
    {
        request = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(SourceName)) { error = "Укажите место покупки."; return false; }
        if (ItemLines.Count == 0) { error = "Добавьте хотя бы один товар."; return false; }

        decimal? ParseOrNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : (decimal.TryParse(text, out var v) ? v : (decimal?)null);

        var itemLines = ItemLines.Select(l => new PurchaseItemLineInput
        {
            Id = l.Id,
            ItemName = l.ItemName,
            CategoryName = l.CategoryName,
            Quantity = l.Quantity,
            UnitPurchaseCost = l.UnitPurchaseCost,
            ManualAllocatedSalesTax = ParseOrNull(l.ManualAllocatedSalesTaxText),
            ManualAllocatedExpenses = ParseOrNull(l.ManualAllocatedExpensesText),
            Notes = l.Notes
        }).ToList();

        var expenseLines = ExpenseLines.Select(e => new PurchaseExpenseLineInput
        {
            ExpenseType = e.ExpenseType,
            Amount = e.Amount,
            Notes = e.Notes
        }).ToList();

        request = new CreatePurchaseFullRequest
        {
            PurchaseDate = PurchaseDateValue,
            SourceName = SourceName,
            SourceType = SourceType,
            PurchaseType = PurchaseType,
            PaymentMethod = PaymentMethod,
            Comment = Comment,
            PermitNumber = PermitNumber,
            PermitDate = PermitDate,
            TaxExemptAmount = ParseOrNull(TaxExemptAmountText),
            TaxableAmount = ParseOrNull(TaxableAmountText),
            SalesTaxRate = ParseOrNull(SalesTaxRateText),
            SalesTaxAmountOverride = ParseOrNull(SalesTaxAmountOverrideText),
            SalesTaxAllocationMethod = SalesTaxAllocationMethod,
            ExpenseAllocationMethod = ExpenseAllocationMethod,
            ManualAdjustment = ParseOrNull(ManualAdjustmentText) ?? 0m,
            ItemLines = itemLines,
            ExpenseLines = expenseLines
        };
        return true;
    }

    [RelayCommand]
    private async Task RecalculateAsync()
    {
        if (!TryBuildRequest(out var request, out var error))
        {
            ErrorMessage = error;
            return;
        }

        ErrorMessage = null;
        try
        {
            var preview = await _apiClient.PreviewPurchaseAllocationAsync(new PurchaseAllocationPreviewRequest
            {
                TaxableAmount = request.TaxableAmount,
                SalesTaxRate = request.SalesTaxRate,
                SalesTaxAmountOverride = request.SalesTaxAmountOverride,
                SalesTaxAllocationMethod = request.SalesTaxAllocationMethod,
                ExpenseAllocationMethod = request.ExpenseAllocationMethod,
                ManualAdjustment = request.ManualAdjustment,
                ItemLines = request.ItemLines,
                ExpenseLines = request.ExpenseLines
            });
            ApplyAllocationPreview(preview);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    private void ApplyAllocationPreview(PurchaseAllocationResult result)
    {
        for (var i = 0; i < ItemLines.Count && i < result.Lines.Count; i++)
        {
            var calc = result.Lines[i];
            ItemLines[i].LinePurchaseCost = calc.LinePurchaseCost;
            ItemLines[i].AllocatedSalesTax = calc.AllocatedSalesTax;
            ItemLines[i].AllocatedExpenses = calc.AllocatedExpenses;
            ItemLines[i].FinalLineCostBasis = calc.FinalLineCostBasis;
        }

        MerchandiseSubtotal = result.MerchandiseSubtotal;
        SalesTaxAmount = result.SalesTaxAmount;
        TotalExpenses = result.TotalExpenses;
        TotalPurchaseCost = result.TotalPurchaseCost;
        AllocatedTotal = result.AllocatedTotal;
        Difference = result.Difference;
        PhysicalItemsToCreate = result.PhysicalItemsToCreate;
        IsReadyToSave = result.IsReadyToSave;
        ValidationSummaryText = BuildSummaryText(result.ValidationErrors);
    }

    private string BuildSummaryText(IReadOnlyList<string>? validationErrors = null)
    {
        var status = IsReadyToSave ? "Готово к сохранению" : "Расхождение — распределение не совпадает с итогом";
        var text = $"Итог закупки: {TotalPurchaseCost:F2}\nРаспределено: {AllocatedTotal:F2}\nРазница: {Difference:F2}\nФизических товаров будет создано: {PhysicalItemsToCreate}\nСтатус: {status}";
        if (validationErrors is { Count: > 0 }) text += "\n" + string.Join("\n", validationErrors);
        return text;
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveInternalAsync(allowDifference: false);

    [RelayCommand]
    private async Task SaveAnywayAsync() => await SaveInternalAsync(allowDifference: true);

    private async Task SaveInternalAsync(bool allowDifference)
    {
        if (!TryBuildRequest(out var request, out var error))
        {
            ErrorMessage = error;
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            PurchaseDetailDto result;
            if (_purchaseId is { } id)
            {
                var updateRequest = new UpdatePurchaseFullRequest
                {
                    PurchaseDate = request.PurchaseDate,
                    SourceName = request.SourceName,
                    SourceType = request.SourceType,
                    PurchaseType = request.PurchaseType,
                    PaymentMethod = request.PaymentMethod,
                    Comment = request.Comment,
                    PermitNumber = request.PermitNumber,
                    PermitDate = request.PermitDate,
                    TaxExemptAmount = request.TaxExemptAmount,
                    TaxableAmount = request.TaxableAmount,
                    SalesTaxRate = request.SalesTaxRate,
                    SalesTaxAmountOverride = request.SalesTaxAmountOverride,
                    SalesTaxAllocationMethod = request.SalesTaxAllocationMethod,
                    ExpenseAllocationMethod = request.ExpenseAllocationMethod,
                    ManualAdjustment = request.ManualAdjustment,
                    ItemLines = request.ItemLines,
                    ExpenseLines = request.ExpenseLines,
                    AllowDifference = allowDifference
                };
                result = await _apiClient.UpdatePurchaseFullAsync(id, updateRequest);
            }
            else
            {
                var createRequest = new CreatePurchaseFullRequest
                {
                    PurchaseDate = request.PurchaseDate,
                    SourceName = request.SourceName,
                    SourceType = request.SourceType,
                    PurchaseType = request.PurchaseType,
                    PaymentMethod = request.PaymentMethod,
                    Comment = request.Comment,
                    PermitNumber = request.PermitNumber,
                    PermitDate = request.PermitDate,
                    TaxExemptAmount = request.TaxExemptAmount,
                    TaxableAmount = request.TaxableAmount,
                    SalesTaxRate = request.SalesTaxRate,
                    SalesTaxAmountOverride = request.SalesTaxAmountOverride,
                    SalesTaxAllocationMethod = request.SalesTaxAllocationMethod,
                    ExpenseAllocationMethod = request.ExpenseAllocationMethod,
                    ManualAdjustment = request.ManualAdjustment,
                    ItemLines = request.ItemLines,
                    ExpenseLines = request.ExpenseLines,
                    AllowDifference = allowDifference
                };
                result = await _apiClient.CreatePurchaseFullAsync(createRequest);
            }

            _purchaseId = result.Id;
            IsNew = false;
            ScreenTitle = "Редактирование закупки";
            ApplyDetail(result);

            var itemNumbers = result.ItemLines.SelectMany(l => l.ItemNumbers).OrderBy(n => n).ToList();
            SaveResultText = $"Закупка сохранена. Товаров создано: {itemNumbers.Count}\n" +
                              string.Join("\n", itemNumbers.Select(n => $"#{n}"));
            ShowSaveResult = true;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ── Documents ──────────────────────────────────────────────────────
    [RelayCommand]
    private async Task UploadDocumentAsync()
    {
        if (_purchaseId is not { } id) return;

        var path = await _filePickerService.PickFileAsync("Выберите файл");
        if (path is null) return;

        ErrorMessage = null;
        try
        {
            var document = await _apiClient.UploadDocumentAsync(path);
            await _apiClient.LinkDocumentAsync(document.Id, "Purchase", id);
            Documents.Add(document);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task OpenDocumentAsync(DocumentDto document)
    {
        ErrorMessage = null;
        try
        {
            var (content, _, filename) = await _apiClient.DownloadDocumentAsync(document.Id);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{document.Id}_{filename}");
            await File.WriteAllBytesAsync(tempPath, content);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось открыть файл: {ex.Message}";
        }
    }

    // ── Navigation ─────────────────────────────────────────────────────
    [RelayCommand]
    private void Back() => _navigation.ShowPurchaseList();

    [RelayCommand]
    private void OpenInventory() => _navigation.ShowInventory();

    [RelayCommand]
    private void CreateAnother()
    {
        _purchaseId = null;
        IsNew = true;
        ScreenTitle = "Новое поступление";
        ShowSaveResult = false;
        PurchaseDateValue = DateOnly.FromDateTime(DateTime.Today);
        SourceName = string.Empty;
        SourceType = null;
        PurchaseType = "TaxPaid";
        PaymentMethod = null;
        Comment = null;
        PermitNumber = null;
        PermitDate = null;
        TaxExemptAmountText = string.Empty;
        TaxableAmountText = string.Empty;
        SalesTaxRateText = string.Empty;
        SalesTaxAmountOverrideText = string.Empty;
        ManualAdjustmentText = "0";
        ExpenseLines.Clear();
        ItemLines.Clear();
        ItemLines.Add(new PurchaseLineEditViewModel { Quantity = 1 });
        Documents.Clear();
        ValidationSummaryText = "Нажмите «Пересчитать», чтобы увидеть распределение.";
        OnPropertyChanged(nameof(CanManageDocuments));
    }
}

/// <summary>Client-side mirror of Modules.Inventory.Domain.ReferenceListKeys
/// — the desktop client can't reference server module projects.</summary>
internal static class ReferenceListKeysMirror
{
    public const string PurchaseSource = "PurchaseSource";
    public const string PurchaseType = "PurchaseType";
    public const string PaymentMethod = "PaymentMethod";
    public const string Category = "Category";
    public const string ExpenseType = "ExpenseType";
}
