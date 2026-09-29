namespace Bledob;

public sealed class StripLink
{
    private readonly Func<byte[], CancellationToken, Task> _write;

    public StripLink(Func<byte[], CancellationToken, Task> write) =>
        _write = write ?? throw new ArgumentNullException(nameof(write));

    public Task TurnOnAsync(CancellationToken cancellationToken = default) =>
        _write(StripCommands.TurnOn(), cancellationToken);

    public Task TurnOffAsync(CancellationToken cancellationToken = default) =>
        _write(StripCommands.TurnOff(), cancellationToken);

    public Task SetColorAsync(byte red, byte green, byte blue, CancellationToken cancellationToken = default) =>
        _write(StripCommands.Color(red, green, blue), cancellationToken);

    public Task SetBrightnessAsync(int percent, CancellationToken cancellationToken = default) =>
        _write(StripCommands.Brightness(percent), cancellationToken);

    public Task SetEffectAsync(string name, CancellationToken cancellationToken = default) =>
        _write(StripCommands.Effect(name), cancellationToken);

    public Task SetSpeedAsync(int percent, CancellationToken cancellationToken = default) =>
        _write(StripCommands.Speed(percent), cancellationToken);

    public Task SyncTimeAsync(DateTime? local = null, CancellationToken cancellationToken = default) =>
        _write(StripCommands.SyncTime(local ?? DateTime.Now), cancellationToken);
}
