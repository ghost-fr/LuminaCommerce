namespace Lumina.Application.Ports;

/// <summary>
/// Which kind of build the running app is. Development is the default when
/// unset — deliberately, so nothing about existing local dev workflows
/// changes unless LUMINA_BUILD_MODE is explicitly set. Pilot/Production
/// builds get an extra safety check (see PilotSafetyGuard) that Development
/// builds don't, specifically so a real customer's build can never silently
/// run against demo/seed data.
/// </summary>
public enum BuildMode
{
    Development,
    Pilot,
    Production
}

public interface IBuildModeProvider
{
    BuildMode Mode { get; }
}
