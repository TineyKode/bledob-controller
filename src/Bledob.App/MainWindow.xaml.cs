using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System.Windows.Media;
using Bledob;

namespace Bledob.App;

public partial class MainWindow : Window
{
    private static readonly (string Name, byte R, byte G, byte B)[] ColorPresets =
    [
        ("White", 255, 255, 255),
        ("Warm", 255, 196, 120),
        ("Red", 255, 32, 32),
        ("Orange", 255, 120, 20),
        ("Yellow", 255, 214, 40),
        ("Green", 40, 200, 80),
        ("Cyan", 40, 210, 220),
        ("Blue", 40, 90, 255),
        ("Purple", 140, 60, 255),
        ("Pink", 255, 60, 140),
    ];

    private readonly ObservableCollection<SeenDevice> _devices = new();
    private readonly ICollectionView _deviceView;
    private GattStripSession? _session;
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _colorSend;
    private CancellationTokenSource? _brightnessSend;
    private CancellationTokenSource? _speedSend;
    private bool _uiReady;
    private bool _suppressSliders;
    private bool _allowClose;
    private bool _hiding;
    private bool _toldTray;
    private Forms.NotifyIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();
        AddressBox.Text = AppSettings.LoadAddress();
        _deviceView = System.Windows.Data.CollectionViewSource.GetDefaultView(_devices);
        _deviceView.Filter = FilterDevice;
        _deviceView.SortDescriptions.Add(new SortDescription(nameof(SeenDevice.LooksLikeStrip), ListSortDirection.Descending));
        _deviceView.SortDescriptions.Add(new SortDescription(nameof(SeenDevice.Rssi), ListSortDirection.Descending));
        DeviceList.ItemsSource = _deviceView;
        EffectBox.ItemsSource = StripEffects.All;
        BuildPresets();
        UpdateSwatch();
        _uiReady = true;
    }

    public void EnableTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => Dispatcher.BeginInvoke(BringToFront));
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.BeginInvoke(Quit));
        _tray = new Forms.NotifyIcon
        {
            Icon = TrayIcon.Create(),
            Visible = true,
            Text = "BLEDOB",
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(BringToFront);
    }

    public void BringToFront()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Quit()
    {
        _allowClose = true;
        Close();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_scanCts is not null)
        {
            await _scanCts.CancelAsync();
            return;
        }

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        ScanButton.Content = "Stop";
        _devices.Clear();
        try
        {
            SetStatus("Scanning...");
            var progress = new Progress<SeenDevice>(UpsertDevice);
            await AdvertisementScanner.ScanAsync(TimeSpan.FromSeconds(8), progress, token);
            _deviceView.Refresh();
            if (token.IsCancellationRequested)
            {
                SetStatus("Scan stopped.");
                return;
            }

            var strips = _devices.Where(device => device.LooksLikeStrip).ToList();
            if (strips.Count > 0)
                SetStatus($"Found {strips[0].DisplayName} at {strips[0].AddressText}.");
            else if (_devices.Count == 0)
                SetStatus("No Bluetooth LE devices were advertising.");
            else
                SetStatus($"No ELK-BLEDOB in this scan. {_devices.Count} other device(s) were nearby. Show every device if the name looks unfamiliar.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            Log(ex.Message);
        }
        finally
        {
            _scanCts.Dispose();
            _scanCts = null;
            ScanButton.Content = "Scan";
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not null)
        {
            ConnectButton.IsEnabled = false;
            try
            {
                await DisconnectAsync();
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
            return;
        }

        if (TryParseAddress() is not ulong address)
        {
            SetStatus("Enter an address like BE:37:33:00:0D:0B.");
            return;
        }

        ConnectButton.IsEnabled = false;
        try
        {
            if (!_devices.Any(device => device.Address == address || device.LooksLikeStrip))
            {
                SetStatus("Looking for the strip...");
                var seen = await AdvertisementScanner.ScanAsync(TimeSpan.FromSeconds(6), new Progress<SeenDevice>(UpsertDevice), CancellationToken.None);
                _deviceView.Refresh();
                var match = seen.FirstOrDefault(device => device.Address == address)
                    ?? seen.FirstOrDefault(device => device.LooksLikeStrip);
                if (match is not null && match.Address != address)
                {
                    address = match.Address;
                    AddressBox.Text = match.AddressText;
                    SetStatus($"Found {match.DisplayName} at {match.AddressText}. Connecting...");
                }
                else if (match is null)
                {
                    SetStatus("That address is not advertising. Trying it anyway...");
                }
            }

            SetStatus("Connecting...");
            var session = await GattStripSession.ConnectAsync(address);
            _session = session;
            session.ConnectionLost += Session_ConnectionLost;
            try
            {
                await session.Link.SyncTimeAsync();
            }
            catch (Exception ex)
            {
                Log($"Time sync failed: {ex.Message}");
            }

            AppSettings.SaveAddress(BleAddress.Format(address));
            ControlsCard.IsEnabled = true;
            ConnectButton.Content = "Disconnect";
            SetStatus($"Connected to {session.Name}.");
            Log($"Connected {session.Name} {BleAddress.Format(address)}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            Log(ex.Message);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private async void TurnOn_Click(object sender, RoutedEventArgs e) =>
        await Send(() => _session!.Link.TurnOnAsync(), "Turned on");

    private async void TurnOff_Click(object sender, RoutedEventArgs e) =>
        await Send(() => _session!.Link.TurnOffAsync(), "Turned off");

    private void Color_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;
        UpdateSwatch();
        if (!_uiReady || _suppressSliders || _session is null)
            return;
        Schedule(_colorSend, cts => _colorSend = cts, SendColorAsync);
    }

    private void Brightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady || _session is null)
            return;
        Schedule(_brightnessSend, cts => _brightnessSend = cts, SendBrightnessAsync);
    }

    private void Speed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_uiReady || _session is null)
            return;
        Schedule(_speedSend, cts => _speedSend = cts, SendSpeedAsync);
    }

    private async void EffectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || EffectBox.SelectedItem is not StripEffect effect)
            return;
        var link = _session?.Link;
        if (link is null)
            return;
        var speed = (int)SpeedSlider.Value;
        await Send(async () =>
        {
            await link.SetEffectAsync(effect.Name);
            await link.SetSpeedAsync(speed);
        }, effect.Name);
    }

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceList.SelectedItem is SeenDevice device)
            AddressBox.Text = device.AddressText;
    }

    private void DeviceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DeviceList.SelectedItem is not SeenDevice device || _session is not null)
            return;
        AddressBox.Text = device.AddressText;
        Connect_Click(sender, e);
    }

    private void ShowAllCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_uiReady)
            _deviceView.Refresh();
    }

    private void AddressBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_uiReady)
            _deviceView.Refresh();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        base.OnClosed(e);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _scanCts?.Cancel();
        if (_session is null)
            return;

        e.Cancel = true;
        Dispatcher.InvokeAsync(async () =>
        {
            await DisconnectAsync();
            Close();
        });
    }

    private void HideToTray()
    {
        if (_hiding)
            return;

        _hiding = true;
        _scanCts?.Cancel();
        Hide();
        WindowState = WindowState.Normal;
        _hiding = false;
        _ = DisconnectAsync();
        if (_toldTray || _tray is null)
            return;

        _toldTray = true;
        _tray.ShowBalloonTip(2500, "BLEDOB", "Still running. Click the tray icon when you want it.", Forms.ToolTipIcon.Info);
    }

    private void UpsertDevice(SeenDevice incoming)
    {
        var existing = _devices.FirstOrDefault(device => device.Address == incoming.Address);
        if (existing is null)
        {
            _devices.Add(incoming);
            _deviceView.Refresh();
            return;
        }

        existing.Update(incoming.Name, incoming.Rssi);
    }

    private bool FilterDevice(object item)
    {
        if (item is not SeenDevice device)
            return false;
        if (ShowAllCheck.IsChecked == true || device.LooksLikeStrip)
            return true;
        var typed = TryParseAddress();
        return typed is not null && device.Address == typed.Value;
    }

    private void BuildPresets()
    {
        foreach (var (name, red, green, blue) in ColorPresets)
        {
            var color = Color.FromRgb(red, green, blue);
            var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            var button = new Button
            {
                Content = name,
                Style = (Style)FindResource("PresetButton"),
                Background = new SolidColorBrush(color),
                Foreground = new SolidColorBrush(luminance > 150 ? Colors.Black : Colors.White),
                Tag = (red, green, blue)
            };
            button.Click += Preset_Click;
            Presets.Children.Add(button);
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        var (red, green, blue) = ((byte R, byte G, byte B))((Button)sender).Tag;
        _suppressSliders = true;
        RedSlider.Value = red;
        GreenSlider.Value = green;
        BlueSlider.Value = blue;
        _suppressSliders = false;
        UpdateSwatch();
        if (_session is not null)
            Schedule(_colorSend, cts => _colorSend = cts, SendColorAsync);
    }

    private void UpdateSwatch()
    {
        Swatch.Background = new SolidColorBrush(Color.FromRgb(
            (byte)RedSlider.Value,
            (byte)GreenSlider.Value,
            (byte)BlueSlider.Value));
    }

    private void Schedule(CancellationTokenSource? current, Action<CancellationTokenSource> store, Func<CancellationToken, Task> send)
    {
        current?.Cancel();
        var cts = new CancellationTokenSource();
        store(cts);
        _ = SendSoon(cts, send);
    }

    private async Task SendSoon(CancellationTokenSource cts, Func<CancellationToken, Task> send)
    {
        try
        {
            await Task.Delay(120, cts.Token);
            await send(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer slider position replaced this one.
        }
    }

    private async Task SendColorAsync(CancellationToken cancellationToken)
    {
        var link = _session?.Link;
        if (link is null)
            return;
        var red = (byte)RedSlider.Value;
        var green = (byte)GreenSlider.Value;
        var blue = (byte)BlueSlider.Value;
        await Send(() => link.SetColorAsync(red, green, blue, cancellationToken), $"Color {red}, {green}, {blue}");
    }

    private async Task SendBrightnessAsync(CancellationToken cancellationToken)
    {
        var link = _session?.Link;
        if (link is null)
            return;
        var percent = (int)BrightnessSlider.Value;
        await Send(() => link.SetBrightnessAsync(percent, cancellationToken), $"Brightness {percent}");
    }

    private async Task SendSpeedAsync(CancellationToken cancellationToken)
    {
        var link = _session?.Link;
        if (link is null || EffectBox.SelectedItem is not StripEffect)
            return;
        var percent = (int)SpeedSlider.Value;
        await Send(() => link.SetSpeedAsync(percent, cancellationToken), $"Speed {percent}");
    }

    private async Task Send(Func<Task> action, string label)
    {
        try
        {
            await action();
            SetStatus(label);
            Log(label);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetStatus(ex.Message);
            Log(ex.Message);
        }
    }

    private void Session_ConnectionLost(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (!ReferenceEquals(sender, _session))
                return;
            await DisconnectAsync();
            SetStatus("The strip disconnected.");
        });
    }

    private async Task DisconnectAsync()
    {
        var session = _session;
        _session = null;
        ControlsCard.IsEnabled = false;
        ConnectButton.Content = "Connect";
        if (session is null)
            return;
        session.ConnectionLost -= Session_ConnectionLost;
        await session.DisposeAsync();
        SetStatus("Disconnected.");
        Log("Disconnected");
    }

    private ulong? TryParseAddress()
    {
        try
        {
            return BleAddress.Parse(AddressBox.Text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void Log(string message)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}

internal static class AppSettings
{
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bledob", "settings.json");

    public static string LoadAddress()
    {
        try
        {
            if (!File.Exists(FilePath))
                return MainWindowAddress.Default;
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (document.RootElement.TryGetProperty("address", out var address))
                return address.GetString() ?? MainWindowAddress.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A missing or unreadable settings file falls back to the known strip.
        }

        return MainWindowAddress.Default;
    }

    public static void SaveAddress(string address)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new { address }));
    }
}

internal static class MainWindowAddress
{
    public const string Default = "BE:37:33:00:0D:0B";
}
