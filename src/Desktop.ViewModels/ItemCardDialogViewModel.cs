using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// "Карточка товара" — opened by clicking a row's Наименование cell (see
/// InventoryViewModel.OpenItemCardCommand). A full read+write view across
/// Item/Purchase/Listing/Sale/Return/Expense/Document/AuditLog data — the
/// grid only ever shows the "latest" Listing/Sale per item; an item can
/// have several of each (relisted, sold-returned-resold), so this card
/// shows them as selectable lists instead.
///
/// Every section saves itself independently (its own "Сохранить"/"Добавить"
/// button) rather than the whole dialog having one Save/Cancel pair — after
/// each successful write the dialog reloads everything from the server
/// (TryLoad* helpers absorb any one section's failure so the rest of the
/// dialog still renders), so what's on screen is always the persisted
/// truth, never a locally-guessed diff.
/// </summary>
public sealed partial class ItemCardDialogViewModel : ViewModelBase
{
    private readonly Guid _itemId;
    private readonly IServerApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;

    public ItemCardDialogViewModel(Guid itemId, IServerApiClient apiClient, IDialogService dialogService, IFilePickerService filePickerService)
    {
        _itemId = itemId;
        _apiClient = apiClient;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
        _ = LoadAsync();
    }

    /// <summary>Fired when the user closes the card — always true, since
    /// every edit already saved itself as it happened; the caller
    /// (InventoryViewModel) just needs to know to refresh the grid.</summary>
    public event Action<bool>? RequestClose;

    public IReadOnlyList<StatusOptions.Option> StatusOptionsList => StatusOptions.All;
    public IReadOnlyList<PurchaseTypeOptions.Option> PurchaseTypeOptionsList => PurchaseTypeOptions.All;
    public IReadOnlyList<string> ExpenseTypeOptionsList => ExpenseTypeOptions.All;
    public IReadOnlyList<string> ReturnTypeOptionsList => ReturnTypeOptions.All;
    public IReadOnlyList<string> ListingMarketplaceOptionsList => MarketplaceOptions.Listing;
    public IReadOnlyList<string> SaleMarketplaceOptionsList => MarketplaceOptions.Sale;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string? _errorMessage;

    // ── Основное ──────────────────────────────────────────────────────
    private ItemDto? _item;
    [ObservableProperty] private long _itemNumber;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _categoryName;
    [ObservableProperty] private string _status = "InStock";
    [ObservableProperty] private decimal _costBasisCalculated;
    [ObservableProperty] private decimal _effectiveCostBasis;
    /// <summary>Blank means "no override" — saving blank does NOT clear an
    /// existing override (UpdateItemRequest's PATCH semantics treat null
    /// as "leave alone"); it just leaves whatever was there.</summary>
    [ObservableProperty] private string _costBasisOverrideText = string.Empty;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _brand;
    [ObservableProperty] private string? _model;
    [ObservableProperty] private string? _serialNumber;
    [ObservableProperty] private string? _skuCustomLabel;
    [ObservableProperty] private string? _condition;
    [ObservableProperty] private string? _storageLocation;
    [ObservableProperty] private string _createdInfo = "—";
    [ObservableProperty] private string _updatedInfo = "—";

    /// <summary>Same ReferenceListValue("Category") list the Purchase
    /// screen's Category combo reads from (PurchaseEditViewModel.
    /// CategoryOptions) — a category added on either screen shows up on
    /// both.</summary>
    public ObservableCollection<string> CategoryOptions { get; } = new();

    /// <summary>Same ReferenceListValue("Condition") list the Purchase
    /// screen's Item Draft Editor reads from.</summary>
    public ObservableCollection<string> ConditionOptions { get; } = new();

    // ── Закупка ───────────────────────────────────────────────────────
    private PurchaseDto? _purchase;
    [ObservableProperty] private string _sourceName = string.Empty;
    [ObservableProperty] private DateOnly _purchaseDateValue;
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private string _editPurchaseType = "TaxPaid";
    [ObservableProperty] private bool _editUsedResellerPermit;
    [ObservableProperty] private string _editSalesTaxAmountText = "0";
    [ObservableProperty] private string _editSalesTaxRateText = string.Empty;

    public ObservableCollection<ExpenseDto> Expenses { get; } = new();
    [ObservableProperty] private string _newExpenseType = "Other";
    [ObservableProperty] private string _newExpenseAmountText = string.Empty;
    [ObservableProperty] private string? _newExpenseComment;

    // ── Публикация и продажа ─────────────────────────────────────────
    public ObservableCollection<ListingDto> Listings { get; } = new();
    [ObservableProperty] private ListingDto? _selectedListing;
    [ObservableProperty] private string _editListingMarketplace = "eBay";
    [ObservableProperty] private string _editListingPriceText = string.Empty;
    [ObservableProperty] private DateOnly? _firstPublishedDate;

    public ObservableCollection<SaleDto> Sales { get; } = new();
    [ObservableProperty] private SaleDto? _selectedSale;
    [ObservableProperty] private string _editSaleMarketplace = "eBay";
    [ObservableProperty] private string _editSaleItemPriceText = "0";
    [ObservableProperty] private string _editSalePayoutText = "0";
    [ObservableProperty] private string? _editSaleDestinationState;
    [ObservableProperty] private string? _editSaleDestinationZip;

    public ObservableCollection<SaleFeeDto> Fees { get; } = new();
    [ObservableProperty] private string _newFeeType = string.Empty;
    [ObservableProperty] private string _newFeeAmountText = string.Empty;

    [ObservableProperty] private SaleFinancialsDto? _financials;

    [ObservableProperty] private int? _daysPurchaseToPublish;
    [ObservableProperty] private int? _daysListed;
    [ObservableProperty] private int? _daysPurchaseToSale;

    // ── Возврат ───────────────────────────────────────────────────────
    public ObservableCollection<ReturnDto> Returns { get; } = new();
    [ObservableProperty] private string _newReturnType = "Full";
    [ObservableProperty] private string _newReturnRefundText = string.Empty;
    [ObservableProperty] private bool _newReturnPhysicallyReturned;
    [ObservableProperty] private string? _newReturnComment;

    // ── Вложения ──────────────────────────────────────────────────────
    public ObservableCollection<DocumentDto> Photos { get; } = new();
    public ObservableCollection<DocumentDto> Receipts { get; } = new();
    public ObservableCollection<DocumentDto> Pdfs { get; } = new();
    public ObservableCollection<DocumentDto> OtherDocuments { get; } = new();

    // ── История изменений ────────────────────────────────────────────
    public ObservableCollection<AuditLogEntryDto> History { get; } = new();

    partial void OnSelectedListingChanged(ListingDto? value)
    {
        EditListingMarketplace = value?.Marketplace ?? "eBay";
        EditListingPriceText = value?.ListingPrice?.ToString() ?? string.Empty;
    }

    partial void OnSelectedSaleChanged(SaleDto? value)
    {
        EditSaleMarketplace = value?.Marketplace ?? "eBay";
        EditSaleItemPriceText = value?.ItemSalePrice.ToString() ?? "0";
        EditSalePayoutText = value?.PayoutAmount.ToString() ?? "0";
        EditSaleDestinationState = value?.DestinationState;
        EditSaleDestinationZip = value?.DestinationZip;

        Fees.Clear();
        if (value is not null) foreach (var f in value.Fees) Fees.Add(f);

        RecomputeDayCounts();
        Financials = null;
        if (value is not null) _ = LoadFinancialsAsync(value.Id);
    }

    private async Task LoadFinancialsAsync(Guid saleId)
    {
        try
        {
            Financials = await _apiClient.GetSaleFinancialsAsync(saleId);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    private void RecomputeDayCounts()
    {
        if (_purchase is null)
        {
            DaysPurchaseToPublish = null;
            DaysListed = null;
            DaysPurchaseToSale = null;
            return;
        }

        var purchaseDate = _purchase.PurchaseDate;
        DaysPurchaseToPublish = FirstPublishedDate is { } fp ? fp.DayNumber - purchaseDate.DayNumber : null;

        var listedFrom = FirstPublishedDate ?? purchaseDate;
        if (SelectedSale is not null)
        {
            DaysListed = SelectedSale.SaleDate.DayNumber - listedFrom.DayNumber;
            DaysPurchaseToSale = SelectedSale.SaleDate.DayNumber - purchaseDate.DayNumber;
        }
        else if (FirstPublishedDate is not null)
        {
            DaysListed = DateOnly.FromDateTime(DateTime.Today).DayNumber - listedFrom.DayNumber;
            DaysPurchaseToSale = null;
        }
        else
        {
            DaysListed = null;
            DaysPurchaseToSale = null;
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        var errors = new List<string>();

        var previousListingId = SelectedListing?.Id;
        var previousSaleId = SelectedSale?.Id;

        var item = await TryLoadAsync(() => _apiClient.GetItemAsync(_itemId), "Товар", errors);
        _item = item;
        if (item is not null)
        {
            ItemNumber = item.ItemNumber;
            Name = item.Name;
            CategoryName = item.CategoryName;
            Status = item.Status;
            CostBasisCalculated = item.CostBasisCalculated;
            EffectiveCostBasis = item.EffectiveCostBasis;
            CostBasisOverrideText = item.CostBasisOverride?.ToString() ?? string.Empty;
            Notes = item.Notes;
            Brand = item.Brand;
            Model = item.Model;
            SerialNumber = item.SerialNumber;
            SkuCustomLabel = item.SkuCustomLabel;
            Condition = item.Condition;
            StorageLocation = item.StorageLocation;
        }

        var purchaseTask = item is not null
            ? TryLoadAsync(() => _apiClient.GetPurchaseAsync(item.PurchaseId), "Закупка", errors)
            : Task.FromResult<PurchaseDto?>(null);
        var listingsTask = TryLoadListAsync(() => _apiClient.ListListingsAsync(_itemId), "Листинги", errors);
        var salesTask = TryLoadListAsync(() => _apiClient.ListSalesAsync(_itemId), "Продажи", errors);
        var returnsTask = TryLoadListAsync(() => _apiClient.ListReturnsAsync(_itemId), "Возвраты", errors);
        var expensesTask = TryLoadListAsync(() => _apiClient.ListExpensesAsync(itemId: _itemId), "Расходы", errors);
        var photosTask = TryLoadListAsync(() => _apiClient.ListDocumentsForEntityAsync("ItemPhoto", _itemId), "Фото", errors);
        var receiptsTask = TryLoadListAsync(() => _apiClient.ListDocumentsForEntityAsync("ItemReceipt", _itemId), "Чеки", errors);
        var pdfsTask = TryLoadListAsync(() => _apiClient.ListDocumentsForEntityAsync("ItemPdf", _itemId), "PDF", errors);
        var otherDocsTask = TryLoadListAsync(() => _apiClient.ListDocumentsForEntityAsync("ItemOtherDocument", _itemId), "Другие документы", errors);
        var categoryOptionsTask = TryLoadListAsync(() => _apiClient.ListReferenceValuesAsync(ReferenceListKeysMirror.Category), "Категории", errors);
        var conditionOptionsTask = TryLoadListAsync(() => _apiClient.ListReferenceValuesAsync(ReferenceListKeysMirror.Condition), "Состояния", errors);

        await Task.WhenAll(purchaseTask, listingsTask, salesTask, returnsTask, expensesTask, photosTask, receiptsTask, pdfsTask, otherDocsTask, categoryOptionsTask, conditionOptionsTask);

        _purchase = purchaseTask.Result;
        if (_purchase is not null)
        {
            SourceName = _purchase.SourceName;
            PurchaseDateValue = _purchase.PurchaseDate;
            TotalAmount = _purchase.TotalAmount;
            EditPurchaseType = _purchase.PurchaseType;
            EditUsedResellerPermit = _purchase.UsedResellerPermit;
            EditSalesTaxAmountText = _purchase.SalesTaxAmount.ToString();
            EditSalesTaxRateText = _purchase.SalesTaxRate?.ToString() ?? string.Empty;
        }

        Listings.Clear();
        foreach (var l in listingsTask.Result) Listings.Add(l);
        var publishedDates = Listings.Where(l => l.PublishedDate is not null).Select(l => l.PublishedDate!.Value).ToList();
        FirstPublishedDate = publishedDates.Count > 0 ? publishedDates.Min() : null;

        Sales.Clear();
        foreach (var s in salesTask.Result) Sales.Add(s);

        Returns.Clear();
        foreach (var r in returnsTask.Result) Returns.Add(r);

        Expenses.Clear();
        foreach (var e in expensesTask.Result) Expenses.Add(e);

        Photos.Clear();
        foreach (var d in photosTask.Result) Photos.Add(d);
        Receipts.Clear();
        foreach (var d in receiptsTask.Result) Receipts.Add(d);
        Pdfs.Clear();
        foreach (var d in pdfsTask.Result) Pdfs.Add(d);
        OtherDocuments.Clear();
        foreach (var d in otherDocsTask.Result) OtherDocuments.Add(d);

        CategoryOptions.Clear();
        foreach (var c in categoryOptionsTask.Result) CategoryOptions.Add(c.Value);
        ConditionOptions.Clear();
        foreach (var c in conditionOptionsTask.Result) ConditionOptions.Add(c.Value);

        // Preserve the user's selection across a reload when possible;
        // otherwise fall back to the most recent listing/sale.
        SelectedListing = Listings.FirstOrDefault(l => l.Id == previousListingId)
            ?? Listings.OrderByDescending(l => l.PublishedDate ?? DateOnly.MinValue).FirstOrDefault();
        SelectedSale = Sales.FirstOrDefault(s => s.Id == previousSaleId)
            ?? Sales.OrderByDescending(s => s.SaleDate).FirstOrDefault();

        RecomputeDayCounts();
        await LoadHistoryAsync(errors);

        if (errors.Count > 0) ErrorMessage = string.Join(" | ", errors);
        IsLoading = false;
    }

    private async Task LoadHistoryAsync(List<string> errors)
    {
        var ids = new List<Guid> { _itemId };
        if (_purchase is not null) ids.Add(_purchase.Id);
        ids.AddRange(Listings.Select(l => l.Id));
        ids.AddRange(Sales.Select(s => s.Id));
        ids.AddRange(Returns.Select(r => r.Id));

        var tasks = ids.Select(id => TryLoadListAsync(() => _apiClient.GetAuditLogAsync(null, id, 500), "История", errors)).ToList();
        var results = await Task.WhenAll(tasks);

        History.Clear();
        foreach (var entry in results.SelectMany(r => r).OrderByDescending(e => e.ChangedAt))
        {
            History.Add(entry);
        }

        var created = History.Count > 0 ? History.MinBy(e => e.ChangedAt) : null;
        var lastModified = History.Count > 0 ? History[0] : null; // already sorted descending
        CreatedInfo = created is not null ? $"{created.ChangedAt:g} — {created.ChangedBy}" : "—";
        UpdatedInfo = lastModified is not null ? $"{lastModified.ChangedAt:g} — {lastModified.ChangedBy}" : "—";
    }

    private static async Task<T?> TryLoadAsync<T>(Func<Task<T>> load, string sectionName, List<string> errors) where T : class
    {
        try
        {
            return await load();
        }
        catch (ServerApiException ex)
        {
            errors.Add($"{sectionName}: {ex.Error.Message}");
            return null;
        }
    }

    private static async Task<IReadOnlyList<T>> TryLoadListAsync<T>(Func<Task<IReadOnlyList<T>>> load, string sectionName, List<string> errors)
    {
        try
        {
            return await load();
        }
        catch (ServerApiException ex)
        {
            errors.Add($"{sectionName}: {ex.Error.Message}");
            return Array.Empty<T>();
        }
    }

    private async Task<DateOnly?> ShowDatePickerAsync(string title, DateOnly? initial)
    {
        var vm = new DatePickerDialogViewModel(title, initial);
        return await _dialogService.ShowAsync<DatePickerDialogViewModel, DateOnly?>(vm);
    }

    // ── Commands: Основное ───────────────────────────────────────────
    [RelayCommand]
    private async Task SaveItemAsync()
    {
        decimal? costBasisOverride = null;
        if (!string.IsNullOrWhiteSpace(CostBasisOverrideText))
        {
            if (!decimal.TryParse(CostBasisOverrideText, out var parsed))
            {
                ErrorMessage = "Итоговая себестоимость должна быть числом.";
                return;
            }
            costBasisOverride = parsed;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateItemAsync(_itemId, new UpdateItemRequest
            {
                Name = Name,
                CategoryName = CategoryName,
                Status = Status,
                CostBasisOverride = costBasisOverride,
                Notes = Notes,
                Brand = Brand,
                Model = Model,
                SerialNumber = SerialNumber,
                SkuCustomLabel = SkuCustomLabel,
                Condition = Condition,
                StorageLocation = StorageLocation
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task AddConditionAsync()
    {
        var dialogVm = new TextInputDialogViewModel("Новое состояние", "Введите новое значение:");
        var value = await _dialogService.ShowAsync<TextInputDialogViewModel, string>(dialogVm);
        if (string.IsNullOrWhiteSpace(value)) return;

        ErrorMessage = null;
        try
        {
            var created = await _apiClient.CreateReferenceValueAsync(new CreateReferenceListValueRequest
            {
                ListKey = ReferenceListKeysMirror.Condition,
                Value = value
            });
            if (!ConditionOptions.Contains(created.Value)) ConditionOptions.Add(created.Value);
            Condition = created.Value;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var dialogVm = new TextInputDialogViewModel("Новая категория", "Введите новое значение:");
        var value = await _dialogService.ShowAsync<TextInputDialogViewModel, string>(dialogVm);
        if (string.IsNullOrWhiteSpace(value)) return;

        ErrorMessage = null;
        try
        {
            var created = await _apiClient.CreateReferenceValueAsync(new CreateReferenceListValueRequest
            {
                ListKey = ReferenceListKeysMirror.Category,
                Value = value
            });
            if (!CategoryOptions.Contains(created.Value)) CategoryOptions.Add(created.Value);
            CategoryName = created.Value;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    // ── Commands: Закупка ────────────────────────────────────────────
    [RelayCommand]
    private async Task SavePurchaseAsync()
    {
        if (_purchase is null) return;
        if (!decimal.TryParse(EditSalesTaxAmountText, out var taxAmount) || taxAmount < 0)
        {
            ErrorMessage = "Sales Tax должен быть неотрицательным числом.";
            return;
        }
        decimal? taxRate = null;
        if (!string.IsNullOrWhiteSpace(EditSalesTaxRateText))
        {
            if (!decimal.TryParse(EditSalesTaxRateText, out var rate))
            {
                ErrorMessage = "Tax Rate должен быть числом.";
                return;
            }
            taxRate = rate;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdatePurchaseAsync(_purchase.Id, new UpdatePurchaseRequest
            {
                PurchaseType = EditPurchaseType,
                UsedResellerPermit = EditUsedResellerPermit,
                SalesTaxAmount = taxAmount,
                SalesTaxRate = taxRate
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task OpenPurchaseDateAsync()
    {
        if (_purchase is null) return;
        var newDate = await ShowDatePickerAsync("Дата покупки", _purchase.PurchaseDate);
        if (newDate is null) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdatePurchaseAsync(_purchase.Id, new UpdatePurchaseRequest { PurchaseDate = newDate });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task OpenSupplierPickerAsync()
    {
        if (_purchase is null) return;
        var pickerVm = new SupplierPickerViewModel(_apiClient, _dialogService);
        var chosen = await _dialogService.ShowAsync<SupplierPickerViewModel, SupplierDto>(pickerVm);
        if (chosen is null) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdatePurchaseAsync(_purchase.Id, new UpdatePurchaseRequest { SupplierId = chosen.Id });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task AddExpenseAsync()
    {
        if (!decimal.TryParse(NewExpenseAmountText, out var amount) || amount < 0)
        {
            ErrorMessage = "Сумма расхода должна быть неотрицательным числом.";
            return;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.CreateExpenseAsync(new CreateExpenseRequest
            {
                ExpenseType = NewExpenseType,
                Amount = amount,
                ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
                ItemId = _itemId,
                Comment = NewExpenseComment
            });
            NewExpenseAmountText = string.Empty;
            NewExpenseComment = null;
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    // ── Commands: Публикация и продажа ───────────────────────────────
    [RelayCommand]
    private async Task CreateListingAsync()
    {
        ErrorMessage = null;
        try
        {
            await _apiClient.CreateListingAsync(new CreateListingRequest
            {
                ItemId = _itemId,
                Marketplace = "eBay",
                PublishedDate = DateOnly.FromDateTime(DateTime.Today)
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task SaveListingAsync()
    {
        if (SelectedListing is null) return;
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(EditListingPriceText))
        {
            if (!decimal.TryParse(EditListingPriceText, out var parsed))
            {
                ErrorMessage = "Цена публикации должна быть числом.";
                return;
            }
            price = parsed;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateListingAsync(SelectedListing.Id, new UpdateListingRequest
            {
                Marketplace = EditListingMarketplace,
                ListingPrice = price
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task OpenListingDateAsync()
    {
        if (SelectedListing is null) return;
        var newDate = await ShowDatePickerAsync("Дата публикации", SelectedListing.PublishedDate);
        if (newDate is null) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateListingAsync(SelectedListing.Id, new UpdateListingRequest { PublishedDate = newDate });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task CreateSaleAsync()
    {
        ErrorMessage = null;
        try
        {
            await _apiClient.CreateSaleAsync(new CreateSaleRequest
            {
                ItemId = _itemId,
                ListingId = SelectedListing?.Id,
                Marketplace = "eBay",
                SaleDate = DateOnly.FromDateTime(DateTime.Today),
                ItemSalePrice = 0,
                PayoutAmount = 0
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task SaveSaleAsync()
    {
        if (SelectedSale is null) return;
        if (!decimal.TryParse(EditSaleItemPriceText, out var price) || price < 0)
        {
            ErrorMessage = "Цена продажи должна быть неотрицательным числом.";
            return;
        }
        if (!decimal.TryParse(EditSalePayoutText, out var payout))
        {
            ErrorMessage = "Payout Amount должен быть числом.";
            return;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateSaleAsync(SelectedSale.Id, new UpdateSaleRequest
            {
                Marketplace = EditSaleMarketplace,
                ItemSalePrice = price,
                PayoutAmount = payout,
                DestinationState = EditSaleDestinationState,
                DestinationZip = EditSaleDestinationZip
            });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task OpenSaleDateAsync()
    {
        if (SelectedSale is null) return;
        var newDate = await ShowDatePickerAsync("Дата продажи", SelectedSale.SaleDate);
        if (newDate is null) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateSaleAsync(SelectedSale.Id, new UpdateSaleRequest { SaleDate = newDate });
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task AddFeeAsync()
    {
        if (SelectedSale is null) return;
        if (string.IsNullOrWhiteSpace(NewFeeType))
        {
            ErrorMessage = "Укажите тип комиссии.";
            return;
        }
        if (!decimal.TryParse(NewFeeAmountText, out var amount))
        {
            ErrorMessage = "Сумма комиссии должна быть числом.";
            return;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.AddSaleFeeAsync(SelectedSale.Id, new CreateSaleFeeRequest { FeeType = NewFeeType, Amount = amount });
            NewFeeType = string.Empty;
            NewFeeAmountText = string.Empty;
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    // ── Commands: Возврат ─────────────────────────────────────────────
    [RelayCommand]
    private async Task CreateReturnAsync()
    {
        if (SelectedSale is null)
        {
            ErrorMessage = "Сначала выберите продажу.";
            return;
        }
        if (!decimal.TryParse(NewReturnRefundText, out var refund) || refund < 0)
        {
            ErrorMessage = "Сумма возврата должна быть неотрицательным числом.";
            return;
        }

        ErrorMessage = null;
        try
        {
            await _apiClient.CreateReturnAsync(new CreateReturnRequest
            {
                SaleId = SelectedSale.Id,
                ItemId = _itemId,
                ReturnDate = DateOnly.FromDateTime(DateTime.Today),
                ReturnType = NewReturnType,
                RefundToBuyer = refund,
                PhysicallyReturned = NewReturnPhysicallyReturned,
                Comment = NewReturnComment
            });
            NewReturnRefundText = string.Empty;
            NewReturnComment = null;
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    // ── Commands: Вложения ────────────────────────────────────────────
    [RelayCommand]
    private Task UploadPhotoAsync() => UploadDocumentAsync("ItemPhoto", Photos);

    [RelayCommand]
    private Task UploadReceiptAsync() => UploadDocumentAsync("ItemReceipt", Receipts);

    [RelayCommand]
    private Task UploadPdfAsync() => UploadDocumentAsync("ItemPdf", Pdfs);

    [RelayCommand]
    private Task UploadOtherDocumentAsync() => UploadDocumentAsync("ItemOtherDocument", OtherDocuments);

    private async Task UploadDocumentAsync(string category, ObservableCollection<DocumentDto> targetCollection)
    {
        var path = await _filePickerService.PickFileAsync("Выберите файл");
        if (path is null) return;

        ErrorMessage = null;
        try
        {
            var document = await _apiClient.UploadDocumentAsync(path);
            await _apiClient.LinkDocumentAsync(document.Id, category, _itemId);
            targetCollection.Add(document);
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
            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
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
    private async Task DeleteItemAsync()
    {
        var confirmVm = new ConfirmDialogViewModel(
            "Удалить товар?",
            $"«{Name}» будет удалён из инвентаря. Это действие нельзя отменить из интерфейса.",
            confirmText: "Удалить");
        var confirmed = await _dialogService.ShowAsync<ConfirmDialogViewModel, bool>(confirmVm);
        if (!confirmed) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.DeleteItemAsync(_itemId);
            RequestClose?.Invoke(true);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(true);
}
