using System.Collections.Concurrent;
using Windows.Devices.Bluetooth.Advertisement;

namespace Bledob;

public static class AdvertisementScanner
{
    public static async Task<IReadOnlyList<SeenDevice>> ScanAsync(
        TimeSpan duration,
        IProgress<SeenDevice>? progress,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<ulong, SeenDevice>();
        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += (_, args) =>
        {
            var name = args.Advertisement.LocalName ?? "";
            var device = new SeenDevice(args.BluetoothAddress, name, args.RawSignalStrengthInDBm);
            found[args.BluetoothAddress] = device;
            progress?.Report(device);
        };

        try
        {
            watcher.Start();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Bluetooth is off or this PC has no LE adapter.", ex);
        }

        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Return whatever advertised before the scan was stopped.
        }
        finally
        {
            if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                watcher.Stop();
        }

        if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Aborted && found.IsEmpty)
            throw new InvalidOperationException("Bluetooth is off or this PC has no LE adapter.");

        return found.Values.ToList();
    }
}
