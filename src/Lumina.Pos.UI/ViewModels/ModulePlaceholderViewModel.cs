using CommunityToolkit.Mvvm.ComponentModel;
using Lumina.Pos.UI.Navigation;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Structural placeholder for a blueprint module whose backend/UI is not yet implemented.
/// Preserves hierarchy and labels from UI_STRUCTURE_BLUEPRINT.md without inventing modules.
/// </summary>
public partial class ModulePlaceholderViewModel : ObservableObject
{
    public string ModuleId { get; }
    public string Title { get; }
    public string Description { get; }
    public string BlueprintNote { get; }

    public ModulePlaceholderViewModel(NavItem item)
    {
        ModuleId = item.Id;
        Title = item.Title;
        Description = item.Description;
        BlueprintNote =
            "This screen exists in the GesVent UI structure blueprint. " +
            "Backend and Avalonia UI for this module will land in a later phase. " +
            "Navigation and naming are preserved from the blueprint spine.";
    }
}
