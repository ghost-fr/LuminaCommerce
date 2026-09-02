using Lumina.Contracts.Devices;

namespace Lumina.Application.Devices;

/// <summary>
/// EMULATED scale — no real hardware involved. Returns a randomized test
/// weight in a plausible range for produce/deli items. Registered by default
/// in DI; swap for a real serial-protocol adapter (Toledo/CAS/Dibal/etc. —
/// protocol depends entirely on the specific device model, see
/// PHASE8_NOTES.md) when one exists.
/// </summary>
public sealed class SimulatedScaleService : IScaleService
{
    private readonly Random _random = new();

    public Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(DeviceConnectionStatus.Connected);

    public Task<WeightReading?> ReadWeightAsync(CancellationToken ct = default)
    {
        // 0.100 kg to 2.100 kg, 3 decimal places — plausible for a produce item.
        // Always returns IsStable: true since there's no real settling behavior
        // to simulate; a real adapter's IsStable would reflect actual hardware
        // state and could legitimately be false.
        var kg = Math.Round((decimal)(0.100 + _random.NextDouble() * 2.0), 3);
        return Task.FromResult<WeightReading?>(new WeightReading(kg, IsStable: true));
    }
}
