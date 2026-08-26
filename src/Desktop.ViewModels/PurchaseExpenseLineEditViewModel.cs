using CommunityToolkit.Mvvm.ComponentModel;

namespace ResellerSystem.Desktop.ViewModels;

public sealed partial class PurchaseExpenseLineEditViewModel : ObservableObject
{
    [ObservableProperty] private string _expenseType = "Other";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string? _notes;
}
