using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// GesVent-shaped module landing page for domains not yet wired to application services.
/// </summary>
public partial class ModulePlaceholderViewModel : ObservableObject
{
    public string Title { get; }
    public string GesVentKey { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Functions { get; }
    public bool IsLive { get; }

    public ModulePlaceholderViewModel(
        string title,
        string gesVentKey,
        string summary,
        IReadOnlyList<string> functions,
        bool isLive = false)
    {
        Title = title;
        GesVentKey = gesVentKey;
        Summary = summary;
        Functions = functions;
        IsLive = isLive;
    }
}
