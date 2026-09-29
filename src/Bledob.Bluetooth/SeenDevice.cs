using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bledob;

public sealed class SeenDevice : INotifyPropertyChanged
{
    private string _name;
    private short _rssi;

    public SeenDevice(ulong address, string name, short rssi)
    {
        Address = address;
        _name = (name ?? "").Trim();
        _rssi = rssi;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ulong Address { get; }

    public string Name => _name;

    public short Rssi => _rssi;

    public string AddressText => BleAddress.Format(Address);

    public string DisplayName => string.IsNullOrWhiteSpace(_name) ? "Unnamed device" : _name;

    public string RssiText => $"{_rssi} dBm";

    public bool LooksLikeStrip =>
        _name.Contains("BLEDOB", StringComparison.OrdinalIgnoreCase)
        || _name.Contains("BLEDOM", StringComparison.OrdinalIgnoreCase)
        || _name.Contains("ELK-BLE", StringComparison.OrdinalIgnoreCase);

    public void Update(string name, short rssi)
    {
        name = (name ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(_name, name, StringComparison.Ordinal))
        {
            _name = name;
            Notify(nameof(Name));
            Notify(nameof(DisplayName));
            Notify(nameof(LooksLikeStrip));
        }

        if (_rssi != rssi)
        {
            _rssi = rssi;
            Notify(nameof(Rssi));
            Notify(nameof(RssiText));
        }
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
