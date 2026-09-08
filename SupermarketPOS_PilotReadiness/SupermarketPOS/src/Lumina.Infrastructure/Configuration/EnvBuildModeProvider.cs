using Lumina.Application.Ports;

namespace Lumina.Infrastructure.Configuration;

/// <summary>
/// Reads LUMINA_BUILD_MODE from the environment. Unset or unrecognized
/// values default to Development — deliberately fail-open toward the mode
/// with NO extra safety check, so a missing/misspelled env var never
/// accidentally blocks a legitimate pilot/production start with a confusing
/// error, and never accidentally enables the demo-data check for a normal
/// dev session either. This means the check is opt-in (you must correctly
/// set LUMINA_BUILD_MODE=Pilot or =Production) rather than opt-out — worth
/// knowing when setting up a real pilot build's environment.
/// </summary>
public sealed class EnvBuildModeProvider : IBuildModeProvider
{
    public BuildMode Mode { get; }

    public EnvBuildModeProvider()
    {
        var raw = Environment.GetEnvironmentVariable("LUMINA_BUILD_MODE");
        Mode = Enum.TryParse<BuildMode>(raw, ignoreCase: true, out var parsed) ? parsed : BuildMode.Development;
    }
}
