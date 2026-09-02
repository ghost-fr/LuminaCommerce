using Lumina.Contracts.Devices;

namespace Lumina.Application.Devices;

/// <summary>
/// EMULATED scale — random stable weight in produce/deli range.
/// Swap DI for a real serial adapter when the device model is known.
/// </summary>
public sealed class SimulatedScaleService : IScaleService
{
    private readonly Random _random = new();

    public Task<DeviceConnectionStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(DeviceConnectionStatus.Connected);

    public Task<WeightReading?> ReadWeightAsync(CancellationToken ct = default)
    {
        var kg = Math.Round((decimal)(0.100 + _random.NextDouble() * 2.0), 3);
        return Task.FromResult<WeightReading?>(new WeightReading(kg, IsStable: true));
    }
}
