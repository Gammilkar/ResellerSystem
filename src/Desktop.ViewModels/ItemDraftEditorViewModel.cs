using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>A snapshot of the parent Purchase's header at the moment the
/// Item Draft Editor opened — shown read-only inside the editor ("Данные
/// закупки"), not live-bound, since the header can keep changing after
/// this dialog opens.</summary>
public sealed record PurchaseHeaderSnapshot(
    DateOnly PurchaseDate, string SourceName, string? SourceType, string PurchaseType, bool UsedResellerPermit);

/// <summary>
/// "Новый товар" / "Редактировать товар" — opened from the Purchase
/// screen's "Добавить товар"/"Редактировать товар" buttons and the Items
/// grid's double-click. Edits a PurchaseLineEditViewModel directly (no
/// separate copy/apply step — Cancel just discards the dialog without ever
/// touching the parent's data), since the underlying Item doesn't exist on
/// the server yet (or, when editing an already-saved line, still goes
/// through the normal Purchase Save → PurchaseService.UpdateAsync path,
/// same as editing any other line field). This is deliberately a separate,
/// smaller component from ItemCardDialogViewModel rather than a "null id"
/// mode of it — see the architecture note in the approved plan for why.
/// </summary>
public sealed partial class ItemDraftEditorViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly IFilePickerService _filePickerService;

    public PurchaseLineEditViewModel Line { get; }
    public bool IsNewLine { get; }
    public PurchaseHeaderSnapshot Header { get; }
    public ObservableCollection<string> CategoryOptions { get; }
    public ObservableCollection<string> ConditionOptions { get; }

    public string ConfirmButtonLabel => IsNewLine ? "Добавить в поступление" : "Сохранить изменения";
    public decimal LineTotal => Line.UnitPurchaseCost * Line.Quantity;
    public bool HasCostPreview => Line.FinalLineCostBasis > 0;

    /// <summary>True if the user confirmed (Line should be attached/kept);
    /// false if cancelled (caller discards a new Line, or leaves an
    /// existing one untouched).</summary>
    public event Action<bool>? RequestClose;

    public ItemDraftEditorViewModel(
        PurchaseLineEditViewModel line, bool isNewLine, PurchaseHeaderSnapshot header,
        IServerApiClient apiClient, IDialogService dialogService, IFilePickerService filePickerService,
        ObservableCollection<string> categoryOptions, ObservableCollection<string> conditionOptions)
    {
        Line = line;
        IsNewLine = isNewLine;
        Header = header;
        _apiClient = apiClient;
        _dialogService = dialogService;
        _filePickerService = filePickerService;
        CategoryOptions = categoryOptions;
        ConditionOptions = conditionOptions;

        // LineTotal is a plain computed property (not [ObservableProperty],
        // since it derives from the injected Line rather than owning its
        // own backing field) — without this, typing a new Price/Quantity
        // would never refresh the "Сумма" display.
        Line.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PurchaseLineEditViewModel.UnitPurchaseCost) or nameof(PurchaseLineEditViewModel.Quantity))
                OnPropertyChanged(nameof(LineTotal));
        };
    }

    [ObservableProperty] private string? _errorMessage;

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
            Line.CategoryName = created.Value;
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
            Line.Condition = created.Value;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task AddDocumentAsync()
    {
        var path = await _filePickerService.PickFileAsync("Выберите файл");
        if (path is null) return;
        Line.StagedDocuments.Add(new StagedDocumentRef(path, System.IO.Path.GetFileName(path)));
    }

    [RelayCommand]
    private void RemoveDocument(StagedDocumentRef document) => Line.StagedDocuments.Remove(document);

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Line.ItemName))
        {
            ErrorMessage = "Укажите наименование товара.";
            return;
        }
        if (Line.Quantity <= 0)
        {
            ErrorMessage = "Количество должно быть больше 0.";
            return;
        }
        if (Line.UnitPurchaseCost < 0)
        {
            ErrorMessage = "Цена не может быть отрицательной.";
            return;
        }
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
