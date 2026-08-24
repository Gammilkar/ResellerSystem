namespace ResellerSystem.Desktop.ViewModels.Navigation;

/// <summary>Lets view models switch the currently displayed screen without
/// depending on Avalonia/UI types.</summary>
public interface INavigationService
{
    void ShowServerConnection();
    void ShowInitialSetup();
    void ShowLogin();
    void ShowDatabaseList();
    void ShowCreateDatabase();
    void ShowSelectedDatabase();
    void ShowInventory();
}
