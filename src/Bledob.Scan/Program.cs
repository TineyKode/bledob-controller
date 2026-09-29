using Bledob;

if (args.Any(arg => arg.Equals("connect", StringComparison.OrdinalIgnoreCase)))
{
    var addressText = args.FirstOrDefault(arg => arg.Contains(':')) ?? "BE:37:33:00:0D:0B";
    var address = BleAddress.Parse(addressText);
    Console.WriteLine($"Connecting to {BleAddress.Format(address)}...");
    try
    {
        await using var session = await GattStripSession.ConnectAsync(address);
        Console.WriteLine($"Connected to {session.Name}.");
        await session.Link.SyncTimeAsync();
        await session.Link.TurnOnAsync();
        await session.Link.SetColorAsync(255, 196, 120);
        await session.Link.SetBrightnessAsync(80);
        Console.WriteLine("On, warm white, brightness 80. Disconnecting so the phone can reconnect.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }

    return;
}

var seconds = 8;
if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
    seconds = parsed;

Console.WriteLine($"Scanning for Bluetooth LE devices for {seconds} seconds...");
try
{
    var devices = await AdvertisementScanner.ScanAsync(TimeSpan.FromSeconds(seconds), progress: null, CancellationToken.None);
    if (devices.Count == 0)
    {
        Console.WriteLine("No advertisements seen. Bluetooth may be off, or nothing nearby is advertising.");
        return;
    }

    foreach (var device in devices.OrderByDescending(d => d.LooksLikeStrip).ThenByDescending(d => d.Rssi))
    {
        var mark = device.LooksLikeStrip ? "  strip" : "";
        var name = string.IsNullOrWhiteSpace(device.Name) ? "(no name)" : device.Name;
        Console.WriteLine($"{name,-24} {device.AddressText}  {device.Rssi,4} dBm{mark}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
