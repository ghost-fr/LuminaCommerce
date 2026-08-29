using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Pos;
using Lumina.Domain.Identity;
using Lumina.Pos.UI.Navigation;

namespace Lumina.Pos.UI.ViewModels;

/// <summary>
/// Application shell — modern visual layer over the GesVent MainForm spine
/// (UI_STRUCTURE_BLUEPRINT.md §3): header/branding, left quick-access nav,
/// center content, status bar. Session is the single source of truth for login.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IPosSaleService _pos;

    [ObservableProperty] private object? _currentContent;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _userDisplay;
    [ObservableProperty] private string? _companyDisplay;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private NavItem? _selectedNavItem;

    public LoginViewModel Login { get; }

    public ObservableCollection<NavGroupViewModel> NavGroups { get; } = new();

    public ShellViewModel(IAuthService auth, IPosSaleService pos)
    {
        _auth = auth;
        _pos = pos;
        Login = new LoginViewModel(auth, OnLoginSucceeded);
        ShowLogin();
    }

    private void OnLoginSucceeded()
    {
        var session = _auth.CurrentSession;
        if (session is null)
        {
            ShowLogin();
            return;
        }

        IsLoggedIn = true;
        UserDisplay = session.DisplayName;
        CompanyDisplay = $"Store {session.StoreId.ToString()[..8]}…";
        StatusMessage = "Ready";

        RebuildNavigation(session);

        // Default landing: POS terminal if allowed, else first available item.
        var posItem = FindNavItem(AppModules.Pos);
        if (posItem is not null && session.HasCapability(Capabilities.PosOperateRegister))
            NavigateTo(posItem);
        else if (NavGroups.SelectMany(g => g.Items).FirstOrDefault() is { } first)
            NavigateTo(first.Item);
        else
            CurrentContent = new MainPlaceholderViewModel(session);
    }

    private void RebuildNavigation(IUserSession session)
    {
        NavGroups.Clear();
        foreach (var group in AppModules.BuildTree())
        {
            var visible = group.Items
                .Where(i => i.RequiredCapability is null || session.HasCapability(i.RequiredCapability))
                .Select(i => new NavItemViewModel(i, () => NavigateTo(i)))
                .ToList();

            // Always show group structure from blueprint; items without capability are hidden.
            // If no items visible, still show group with a locked note via empty list skip.
            if (visible.Count > 0)
                NavGroups.Add(new NavGroupViewModel(group.Title, visible));
        }
    }

    private NavItem? FindNavItem(string id) =>
        AppModules.BuildTree().SelectMany(g => g.Items).FirstOrDefault(i => i.Id == id);

    private void NavigateTo(NavItem item)
    {
        SelectedNavItem = item;
        StatusMessage = item.Title;

        var session = _auth.CurrentSession;
        if (session is null)
        {
            ShowLogin();
            return;
        }

        CurrentContent = item.Id switch
        {
            AppModules.Pos when session.HasCapability(Capabilities.PosOperateRegister)
                => new PosCartViewModel(_pos, session, registerId: null),

            AppModules.Pos
                => new MainPlaceholderViewModel(session),

            _ => new ModulePlaceholderViewModel(item)
        };
    }

    private void ShowLogin()
    {
        IsLoggedIn = false;
        UserDisplay = null;
        CompanyDisplay = null;
        StatusMessage = null;
        SelectedNavItem = null;
        NavGroups.Clear();
        CurrentContent = Login;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        ShowLogin();
    }
}

public partial class NavGroupViewModel : ObservableObject
{
    public string Title { get; }
    public ObservableCollection<NavItemViewModel> Items { get; }

    public NavGroupViewModel(string title, IEnumerable<NavItemViewModel> items)
    {
        Title = title;
        Items = new ObservableCollection<NavItemViewModel>(items);
    }
}

public partial class NavItemViewModel : ObservableObject
{
    public NavItem Item { get; }
    public string Title => Item.Title;
    public string Description => Item.Description;

    private readonly Action _navigate;

    public NavItemViewModel(NavItem item, Action navigate)
    {
        Item = item;
        _navigate = navigate;
    }

    [RelayCommand]
    private void Open() => _navigate();
}

public partial class MainPlaceholderViewModel : ObservableObject
{
    public string Greeting { get; }
    public string CapabilitiesSummary { get; }

    public MainPlaceholderViewModel(IUserSession session)
    {
        Greeting = $"Welcome, {session.DisplayName}";
        CapabilitiesSummary = session.Capabilities.Count == 0
            ? "(no capabilities)"
            : string.Join(", ", session.Capabilities.OrderBy(c => c));
    }
}
