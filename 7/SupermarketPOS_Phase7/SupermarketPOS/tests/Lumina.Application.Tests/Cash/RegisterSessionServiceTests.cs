using Lumina.Application.Cash;
using Lumina.Application.Ports;
using Lumina.Contracts.Auth;
using Lumina.Contracts.Cash;
using Lumina.Domain.Cash;
using Lumina.Domain.Sales;
using Xunit;

namespace Lumina.Application.Tests.Cash;

public class RegisterSessionServiceTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid RegisterId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FakeRegisterRepository : IRegisterRepository
    {
        public Register? Register;
        public Task<Register?> FindByIdAsync(Guid registerId, CancellationToken ct = default) =>
            Task.FromResult(Register?.Id == registerId ? Register : null);
        public Task UpdateAsync(Register register, CancellationToken ct = default) { Register = register; return Task.CompletedTask; }
    }

    private sealed class FakeRegisterSessionRepository : IRegisterSessionRepository
    {
        public RegisterSession? Session;
        public Task<RegisterSession?> FindByIdAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Session?.Id == registerSessionId ? Session : null);
        public Task<RegisterSession?> FindOpenByRegisterIdAsync(Guid registerId, CancellationToken ct = default) =>
            Task.FromResult(Session?.RegisterId == registerId && Session.Status == RegisterSessionStatus.Open ? Session : null);
        public Task AddAsync(RegisterSession session, CancellationToken ct = default) { Session = session; return Task.CompletedTask; }
        public Task UpdateAsync(RegisterSession session, CancellationToken ct = default) { Session = session; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCashMovementRepository : ICashMovementRepository
    {
        public readonly List<CashMovement> Movements = new();
        public Task AddAsync(CashMovement movement, CancellationToken ct = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task<decimal> GetTotalForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult(Movements.Where(m => m.RegisterSessionId == registerSessionId).Sum(m => m.Amount));
        public Task<IReadOnlyList<CashMovement>> GetForSessionAsync(Guid registerSessionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CashMovement>>(Movements.Where(m => m.RegisterSessionId == registerSessionId).ToList());
    }

    private sealed class FakeUserSession : IUserSession
    {
        public Guid UserId { get; init; }
        public Guid TenantId { get; init; }
        public Guid StoreId { get; init; }
        public string DisplayName { get; init; } = "Test User";
        public string Username { get; init; } = "test";
        public IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>();
        public bool HasCapability(string capability) => Capabilities.Contains(capability);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public IUserSession? CurrentSession { get; set; }
        public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Fixture
    {
        public FakeRegisterRepository Registers = new();
        public FakeRegisterSessionRepository Sessions = new();
        public FakeCashMovementRepository Movements = new();
        public FakeAuthService Auth = new();

        public Fixture()
        {
            Registers.Register = new Register(RegisterId, StoreId, "Register 1");
            Auth.CurrentSession = new FakeUserSession { UserId = UserId, StoreId = StoreId };
        }

        public RegisterSessionService BuildService() => new(Registers, Sessions, Movements, Auth);
    }

    [Fact]
    public async Task OpenAsync_RegisterCurrentlyClosed_Succeeds_AndOpensRegister()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var result = await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        Assert.Equal(OpenRegisterStatus.Success, result.Status);
        Assert.NotNull(result.Session);
        Assert.Equal(100m, result.Session!.OpeningFloat);
        Assert.True(f.Registers.Register!.IsOpen);
    }

    [Fact]
    public async Task OpenAsync_AlreadyOpen_ReturnsRejected()
    {
        var f = new Fixture();
        f.Registers.Register!.Open();
        var svc = f.BuildService();

        var result = await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        Assert.Equal(OpenRegisterStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task OpenAsync_NegativeOpeningFloat_ReturnsRejected()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var result = await svc.OpenAsync(new OpenRegisterRequest(RegisterId, -1m));

        Assert.Equal(OpenRegisterStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task OpenAsync_NoLoggedInUser_Throws()
    {
        var f = new Fixture();
        f.Auth.CurrentSession = null;
        var svc = f.BuildService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m)));
    }

    [Fact]
    public async Task CloseAsync_NoDiscrepancy_ReturnsZeroDiscrepancy()
    {
        var f = new Fixture();
        var svc = f.BuildService();
        await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        var result = await svc.CloseAsync(new CloseRegisterRequest(RegisterId, 100m));

        Assert.Equal(CloseRegisterStatus.Success, result.Status);
        Assert.Equal(100m, result.ExpectedClosingCash);
        Assert.Equal(0m, result.Discrepancy);
        Assert.False(f.Registers.Register!.IsOpen);
    }

    [Fact]
    public async Task CloseAsync_WithCashMovements_ComputesExpectedCashCorrectly()
    {
        var f = new Fixture();
        var svc = f.BuildService();
        var openResult = await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        // simulate two cash sales and one manual cash-out
        f.Movements.Movements.Add(new CashMovement(Guid.NewGuid(), openResult.Session!.RegisterSessionId, CashMovementType.CashSaleTender, 20m, null, Guid.NewGuid()));
        f.Movements.Movements.Add(new CashMovement(Guid.NewGuid(), openResult.Session!.RegisterSessionId, CashMovementType.CashSaleTender, 15m, null, Guid.NewGuid()));
        f.Movements.Movements.Add(new CashMovement(Guid.NewGuid(), openResult.Session!.RegisterSessionId, CashMovementType.CashOut, -10m, "Safe drop", null));

        // 100 (float) + 20 + 15 - 10 = 125 expected
        var result = await svc.CloseAsync(new CloseRegisterRequest(RegisterId, 125m));

        Assert.Equal(125m, result.ExpectedClosingCash);
        Assert.Equal(0m, result.Discrepancy);
    }

    [Fact]
    public async Task CloseAsync_NotOpen_ReturnsRejected()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var result = await svc.CloseAsync(new CloseRegisterRequest(RegisterId, 100m));

        Assert.Equal(CloseRegisterStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RecordCashMovementAsync_CashOut_Succeeds()
    {
        var f = new Fixture();
        var svc = f.BuildService();
        await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        var result = await svc.RecordCashMovementAsync(
            new RecordCashMovementRequest(RegisterId, CashMovementDirection.Out, 30m, "Safe drop"));

        Assert.Equal(RecordCashMovementStatus.Success, result.Status);
        Assert.Single(f.Movements.Movements);
        Assert.Equal(-30m, f.Movements.Movements[0].Amount);
    }

    [Fact]
    public async Task RecordCashMovementAsync_EmptyReason_ReturnsRejected()
    {
        var f = new Fixture();
        var svc = f.BuildService();
        await svc.OpenAsync(new OpenRegisterRequest(RegisterId, 100m));

        var result = await svc.RecordCashMovementAsync(
            new RecordCashMovementRequest(RegisterId, CashMovementDirection.In, 30m, ""));

        Assert.Equal(RecordCashMovementStatus.Rejected, result.Status);
        Assert.Empty(f.Movements.Movements);
    }

    [Fact]
    public async Task RecordCashMovementAsync_RegisterNotOpen_ReturnsRejected()
    {
        var f = new Fixture();
        var svc = f.BuildService();

        var result = await svc.RecordCashMovementAsync(
            new RecordCashMovementRequest(RegisterId, CashMovementDirection.In, 30m, "Top up"));

        Assert.Equal(RecordCashMovementStatus.Rejected, result.Status);
    }
}
