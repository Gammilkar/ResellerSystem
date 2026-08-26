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
/// Live-as-you-type preview (§13) auto-recalculates via a 400ms debounce
/// (TriggerRecalculate/DebouncedRecalculateAsync) whenever a money-affecting
/// field changes — on this ViewModel directly, or bubbled up from a line's
/// PurchaseLineEditViewModel.RecalculationNeeded event. The "Пересчитать"
/// button (RecalculateCommand) stays as a manual fallback. _suppressRecalc
/// prevents a recalculate-storm while ApplyDetail/CreateAnother populate
/// fields programmatically.
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
    private CancellationTokenSource? _recalcCts;
    private bool _suppressRecalc;

    public PurchaseEditViewModel(IServerApiClient apiClient, IDialogService dialogService,
        IFilePickerService filePickerService, INavigationService navigation)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
        _navigation = navigation;
        ExpenseLines.CollectionChanged += (_, _) => TriggerRecalculate();
    }

    /// <summary>Called by NavigationService right after resolving this VM —
    /// null purchaseId means "new purchase."</summary>
    public void Initialize(Guid? purchaseId)
    {
        _purchaseId = purchaseId;
        IsNew = purchaseId is null;
        ScreenTitle = IsNew ? "Новое поступление" : "Редактирование поступления";
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
    [ObservableProperty] private Guid? _supplierId;
    [ObservableProperty] private string _purchaseType = "TaxPaid";
    [ObservableProperty] private string? _paymentMethod;
    [ObservableProperty] private string? _comment;

    public bool ShowResellerPermitBlock => PurchaseType == "ResellerPermit";
    public bool ShowTaxPaidFields => PurchaseType == "TaxPaid";
    public bool ShowNoTaxNotice => PurchaseType == "NoTax";

    partial void OnPurchaseTypeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowResellerPermitBlock));
        OnPropertyChanged(nameof(ShowTaxPaidFields));
        OnPropertyChanged(nameof(ShowNoTaxNotice));
        if (value != "TaxPaid")
        {
            SalesTaxRateText = string.Empty;
            SalesTaxAmountOverrideText = string.Empty;
        }
    }

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

    partial void OnTaxableAmountTextChanged(string value) => TriggerRecalculate();
    partial void OnSalesTaxRateTextChanged(string value) => TriggerRecalculate();
    partial void OnSalesTaxAmountOverrideTextChanged(string value) => TriggerRecalculate();
    partial void OnManualAdjustmentTextChanged(string value) => TriggerRecalculate();
    partial void OnSalesTaxAllocationMethodChanged(string value) => TriggerRecalculate();
    partial void OnExpenseAllocationMethodChanged(string value) => TriggerRecalculate();

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
    public ObservableCollection<string> ConditionOptions { get; } = new();
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
    public ObservableCollection<string> ValidationErrors { get; } = new();

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
                AttachLine(new PurchaseLineEditViewModel());
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
        await LoadOneListAsync(ReferenceListKeysMirror.Condition, ConditionOptions);
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
        _suppressRecalc = true;
        try
        {
            ApplyDetailCore(d);
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private void ApplyDetailCore(PurchaseDetailDto d)
    {
        PurchaseDateValue = d.PurchaseDate;
        SourceName = d.SourceName;
        SupplierId = d.SupplierId;
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

        foreach (var existingLine in ItemLines) existingLine.RecalculationNeeded -= TriggerRecalculate;
        ItemLines.Clear();
        foreach (var l in d.ItemLines)
        {
            var line = new PurchaseLineEditViewModel
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
                Brand = l.Brand,
                Model = l.Model,
                SerialNumber = l.SerialNumber,
                SkuCustomLabel = l.SkuCustomLabel,
                Condition = l.Condition,
                StorageLocation = l.StorageLocation
            };
            foreach (var itemRef in l.CreatedItems) line.CreatedItems.Add(itemRef);
            AttachLine(line);
        }

        MerchandiseSubtotal = d.MerchandiseSubtotal;
        SalesTaxAmount = d.SalesTaxAmount;
        TotalExpenses = d.ExpenseLines.Sum(e => e.Amount);
        TotalPurchaseCost = d.TotalAmount;
        AllocatedTotal = d.ItemLines.Sum(l => l.FinalLineCostBasis);
        Difference = Math.Round(TotalPurchaseCost - AllocatedTotal, 2, MidpointRounding.AwayFromZero);
        PhysicalItemsToCreate = d.TotalItemCount;
        IsReadyToSave = Difference == 0;
        ValidationErrors.Clear();
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
    private async Task OpenSupplierPickerAsync()
    {
        var pickerVm = new SupplierPickerViewModel(_apiClient, _dialogService);
        var chosen = await _dialogService.ShowAsync<SupplierPickerViewModel, SupplierDto>(pickerVm);
        if (chosen is null) return;

        SupplierId = chosen.Id;
        SourceType = chosen.Name;
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
    private Task AddPurchaseSourceAsync() => AddReferenceValueAsync(ReferenceListKeysMirror.PurchaseSource, "Новый источник поступления", PurchaseSourceOptions, v => SourceName = v);

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
    private PurchaseHeaderSnapshot BuildHeaderSnapshot() => new(
        PurchaseDateValue, SourceName, SourceType, PurchaseType, ShowResellerPermitBlock);

    [RelayCommand]
    private async Task AddItemLineAsync()
    {
        var line = new PurchaseLineEditViewModel { Quantity = 1 };
        var dialogVm = new ItemDraftEditorViewModel(line, isNewLine: true, BuildHeaderSnapshot(),
            _apiClient, _dialogService, _filePickerService, CategoryOptions, ConditionOptions);
        var confirmed = await _dialogService.ShowAsync<ItemDraftEditorViewModel, bool>(dialogVm);
        if (confirmed) AttachLine(line);
    }

    [RelayCommand]
    private async Task EditItemLineAsync(PurchaseLineEditViewModel line)
    {
        var dialogVm = new ItemDraftEditorViewModel(line, isNewLine: false, BuildHeaderSnapshot(),
            _apiClient, _dialogService, _filePickerService, CategoryOptions, ConditionOptions);
        var confirmed = await _dialogService.ShowAsync<ItemDraftEditorViewModel, bool>(dialogVm);
        if (confirmed) TriggerRecalculate();
    }

    [RelayCommand]
    private void DuplicateLine()
    {
        if (SelectedItemLine is null) return;
        var index = ItemLines.IndexOf(SelectedItemLine);
        var clone = SelectedItemLine.Clone();
        clone.RecalculationNeeded += TriggerRecalculate;
        ItemLines.Insert(index + 1, clone);
    }

    [RelayCommand]
    private void RemoveLine()
    {
        if (SelectedItemLine is null) return;
        SelectedItemLine.RecalculationNeeded -= TriggerRecalculate;
        ItemLines.Remove(SelectedItemLine);
        TriggerRecalculate();
    }

    [RelayCommand]
    private async Task OpenItemCardAsync(PurchaseLineItemRefDto itemRef)
    {
        var dialogVm = new ItemCardDialogViewModel(itemRef.Id, _apiClient, _dialogService, _filePickerService);
        await _dialogService.ShowAsync<ItemCardDialogViewModel, bool>(dialogVm);
        if (_purchaseId is not { } id) return;

        ErrorMessage = null;
        try
        {
            var detail = await _apiClient.GetPurchaseFullAsync(id);
            ApplyDetail(detail);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
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

        if (string.IsNullOrWhiteSpace(SourceName)) { error = "Укажите тип места покупки."; return false; }
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
            Notes = l.Notes,
            Brand = l.Brand,
            Model = l.Model,
            SerialNumber = l.SerialNumber,
            SkuCustomLabel = l.SkuCustomLabel,
            Condition = l.Condition,
            StorageLocation = l.StorageLocation
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
            SupplierId = SupplierId,
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

    /// <summary>Debounces auto-recalculation so typing doesn't fire a
    /// server call per keystroke. Suppressed while ApplyDetail/CreateAnother
    /// populate fields programmatically (_suppressRecalc).</summary>
    private void TriggerRecalculate()
    {
        if (_suppressRecalc) return;
        _recalcCts?.Cancel();
        _recalcCts = new CancellationTokenSource();
        _ = DebouncedRecalculateAsync(_recalcCts.Token);
    }

    private async Task DebouncedRecalculateAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (!token.IsCancellationRequested) await RecalculateAsync();
    }

    /// <summary>Adds a line and subscribes to its RecalculationNeeded event
    /// — every ItemLines.Add must go through this, not a bare .Add, so
    /// editing a line's Quantity/Price live-updates the summary.</summary>
    private void AttachLine(PurchaseLineEditViewModel line)
    {
        line.RecalculationNeeded += TriggerRecalculate;
        ItemLines.Add(line);
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
        ValidationErrors.Clear();
        foreach (var error in result.ValidationErrors) ValidationErrors.Add(error);
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
                    SupplierId = request.SupplierId,
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
                    SupplierId = request.SupplierId,
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

            // Capture staged documents (picked before the physical Items
            // existed) by position, before ApplyDetail rebuilds ItemLines
            // from the server response. Line order is preserved end to end
            // (request.ItemLines was built from this same ItemLines order,
            // and the server returns lines ordered by LineNumber, which is
            // assigned in that same order), matching how ApplyAllocationPreview
            // already does positional Lines[i]/ItemLines[i] matching.
            var stagedDocumentsByLine = ItemLines.Select(l => l.StagedDocuments.ToList()).ToList();

            _purchaseId = result.Id;
            IsNew = false;
            ScreenTitle = "Редактирование поступления";
            ApplyDetail(result);

            var itemNumbers = result.ItemLines.SelectMany(l => l.CreatedItems).Select(r => r.ItemNumber).OrderBy(n => n).ToList();
            SaveResultText = $"Поступление сохранено. Товаров создано: {itemNumbers.Count}\n" +
                              string.Join("\n", itemNumbers.Select(n => $"#{n}"));
            ShowSaveResult = true;

            await UploadStagedDocumentsAsync(stagedDocumentsByLine, result.ItemLines);
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

    /// <summary>Uploads and links every file staged in the Item Draft
    /// Editor (before the real Items existed) to every physical Item its
    /// line produced — a batch photo/receipt on a Quantity>1 line applies
    /// to the whole batch, not arbitrarily to just one unit. A failure here
    /// must not read as "the Purchase save failed" — the Purchase is
    /// already committed by this point — so failures are aggregated into
    /// ErrorMessage instead of thrown, matching InventoryViewModel.
    /// DeleteSelectedAsync's existing per-item error aggregation pattern.</summary>
    private async Task UploadStagedDocumentsAsync(
        IReadOnlyList<List<StagedDocumentRef>> stagedDocumentsByLine, IReadOnlyList<PurchaseItemLineDto> resultLines)
    {
        var uploadErrors = new List<string>();
        for (var i = 0; i < stagedDocumentsByLine.Count && i < resultLines.Count; i++)
        {
            if (stagedDocumentsByLine[i].Count == 0) continue;
            foreach (var staged in stagedDocumentsByLine[i])
            {
                foreach (var createdItem in resultLines[i].CreatedItems)
                {
                    try
                    {
                        var document = await _apiClient.UploadDocumentAsync(staged.FilePath);
                        await _apiClient.LinkDocumentAsync(document.Id, "Item", createdItem.Id);
                    }
                    catch (ServerApiException ex)
                    {
                        uploadErrors.Add($"{staged.DisplayName}: {ex.Error.Message}");
                    }
                }
            }
        }
        if (uploadErrors.Count > 0) ErrorMessage = string.Join(" | ", uploadErrors);
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

    [RelayCommand]
    private async Task DeleteDocumentAsync(DocumentDto document)
    {
        if (_purchaseId is not { } id) return;

        var confirmVm = new ConfirmDialogViewModel("Удалить документ?",
            $"«{document.OriginalFilename}» будет удалён.", confirmText: "Удалить");
        var confirmed = await _dialogService.ShowAsync<ConfirmDialogViewModel, bool>(confirmVm);
        if (!confirmed) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.DeleteDocumentLinkAsync(document.Id, "Purchase", id);
            Documents.Remove(document);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
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
        _suppressRecalc = true;
        try
        {
            _purchaseId = null;
            IsNew = true;
            ScreenTitle = "Новое поступление";
            ShowSaveResult = false;
            PurchaseDateValue = DateOnly.FromDateTime(DateTime.Today);
            SourceName = string.Empty;
            SupplierId = null;
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
            foreach (var existingLine in ItemLines) existingLine.RecalculationNeeded -= TriggerRecalculate;
            ItemLines.Clear();
            AttachLine(new PurchaseLineEditViewModel { Quantity = 1 });
            Documents.Clear();
            ValidationErrors.Clear();
            OnPropertyChanged(nameof(CanManageDocuments));
        }
        finally
        {
            _suppressRecalc = false;
        }
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
    public const string Condition = "Condition";
}
