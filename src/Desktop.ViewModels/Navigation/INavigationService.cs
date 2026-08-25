namespace ResellerSystem.Desktop.ViewModels.Navigation;

/// <summary>Lets view models switch the currently displayed screen without
/// depending on Avalonia/UI types.</summary>
public interface INavigationService
{
    void ShowSignIn();
    void ShowInitialSetup();
    void ShowDatabaseList();
    void ShowCreateDatabase();
    void ShowDashboard();
    void ShowInventory();
    void ShowChangePassword();
    void ShowImport();
    void ShowSuppliers();
}
