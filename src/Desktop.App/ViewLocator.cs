using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App;

/// <summary>
/// Resolves "FooViewModel" -> "Views.FooView" by naming convention, so
/// MainWindowViewModel.CurrentViewModel can be any ViewModelBase and the
/// matching view renders automatically (see App.axaml DataTemplates).
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "(none)" };

        var name = data.GetType().FullName!.Replace("ViewModels", "App.Views").Replace("ViewModel", "View");
        var type = System.Type.GetType(name);

        if (type is not null)
        {
            return (Control)System.Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "View not found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
