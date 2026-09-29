using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace Bledob;

public sealed class GattStripSession : IAsyncDisposable
{
    private static readonly Guid WriteCharacteristicUuid = new("0000fff3-0000-1000-8000-00805f9b34fb");

    private readonly BluetoothLEDevice _device;
    private readonly List<GattDeviceService> _services;
    private readonly GattCharacteristic _write;
    private readonly GattWriteOption _option;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;
    private bool _sawConnected;

    private GattStripSession(
        BluetoothLEDevice device,
        List<GattDeviceService> services,
        GattCharacteristic write,
        GattWriteOption option)
    {
        _device = device;
        _services = services;
        _write = write;
        _option = option;
        Link = new StripLink(WriteAsync);
        _device.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    public event EventHandler? ConnectionLost;

    public StripLink Link { get; }

    public string Name
    {
        get
        {
            var name = _device.Name?.Trim();
            return string.IsNullOrWhiteSpace(name) ? "ELK-BLEDOB" : name;
        }
    }

    public static async Task<GattStripSession> ConnectAsync(ulong address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        if (device is null)
        {
            throw new InvalidOperationException(
                "Windows has not seen that address. Power the strip, close the phone app, and scan again.");
        }

        GattDeviceServicesResult services;
        try
        {
            services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        }
        catch
        {
            device.Dispose();
            throw;
        }

        if (services.Status != GattCommunicationStatus.Success)
        {
            device.Dispose();
            throw new InvalidOperationException(Describe(services.Status));
        }

        var kept = new List<GattDeviceService>();
        GattCharacteristic? write = null;
        var denied = false;
        try
        {
            foreach (var service in services.Services)
            {
                cancellationToken.ThrowIfCancellationRequested();
                kept.Add(service);
                var characteristics = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (characteristics.Status == GattCommunicationStatus.AccessDenied)
                {
                    denied = true;
                    continue;
                }

                if (characteristics.Status != GattCommunicationStatus.Success)
                    continue;

                foreach (var characteristic in characteristics.Characteristics)
                {
                    if (characteristic.Uuid == WriteCharacteristicUuid)
                        write = characteristic;
                }
            }
        }
        catch
        {
            foreach (var service in kept)
                service.Dispose();
            device.Dispose();
            throw;
        }

        if (write is null)
        {
            foreach (var service in kept)
                service.Dispose();
            device.Dispose();
            throw new InvalidOperationException(denied
                ? "Windows denied access to the strip's Bluetooth services."
                : "Connected, but this device has no ELK-BLEDOB control characteristic. It may not be the strip.");
        }

        var properties = write.CharacteristicProperties;
        GattWriteOption option;
        if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
            option = GattWriteOption.WriteWithoutResponse;
        else if (properties.HasFlag(GattCharacteristicProperties.Write))
            option = GattWriteOption.WriteWithResponse;
        else
        {
            foreach (var service in kept)
                service.Dispose();
            device.Dispose();
            throw new InvalidOperationException("The strip's control characteristic is not writable.");
        }

        return new GattStripSession(device, kept, write, option);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        foreach (var service in _services)
            service.Dispose();
        _device.Dispose();
        _gate.Dispose();
        await Task.CompletedTask;
    }

    private async Task WriteAsync(byte[] packet, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            var writer = new DataWriter();
            try
            {
                writer.WriteBytes(packet);
                var buffer = writer.DetachBuffer();
                var result = await _write.WriteValueWithResultAsync(buffer, _option);
                if (result.Status != GattCommunicationStatus.Success)
                    throw new InvalidOperationException(Describe(result.Status));
            }
            finally
            {
                writer.Dispose();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
        {
            _sawConnected = true;
            return;
        }

        if (_sawConnected && Volatile.Read(ref _disposed) == 0)
            ConnectionLost?.Invoke(this, EventArgs.Empty);
    }

    private static string Describe(GattCommunicationStatus status) => status switch
    {
        GattCommunicationStatus.Unreachable =>
            "Could not reach the strip. Close the phone app so it releases Bluetooth, then try again.",
        GattCommunicationStatus.AccessDenied =>
            "Windows denied Bluetooth access to the strip.",
        GattCommunicationStatus.ProtocolError =>
            "The strip rejected the command.",
        _ => $"Bluetooth error: {status}."
    };
}
