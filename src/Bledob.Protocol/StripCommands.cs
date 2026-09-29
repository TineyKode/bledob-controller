namespace Bledob;

public static class StripCommands
{
    public static byte[] TurnOn() => [0x7E, 0x07, 0x04, 0xFF, 0x00, 0x01, 0x02, 0x01, 0xEF];

    public static byte[] TurnOff() => [0x7E, 0x07, 0x04, 0x00, 0x00, 0x00, 0x02, 0x01, 0xEF];

    public static byte[] Color(byte red, byte green, byte blue) =>
        [0x7E, 0x07, 0x05, 0x03, red, green, blue, 0x10, 0xEF];

    public static byte[] Brightness(int percent)
    {
        var value = (byte)Math.Clamp(percent, 0, 100);
        return [0x7E, 0x04, 0x01, value, 0x01, 0xFF, 0x02, 0x01, 0xEF];
    }

    public static byte[] Effect(string name)
    {
        var id = StripEffects.IdFor(name);
        return [0x7E, 0x07, 0x03, id, 0x03, 0xFF, 0xFF, 0x00, 0xEF];
    }

    public static byte[] Speed(int percent)
    {
        var value = (byte)Math.Clamp(percent, 0, 100);
        return [0x7E, 0x07, 0x02, value, 0xFF, 0xFF, 0xFF, 0x00, 0xEF];
    }

    public static byte[] SyncTime(DateTime local)
    {
        // The strip stores Monday as 1 through Sunday as 7.
        var weekday = local.DayOfWeek == DayOfWeek.Sunday ? (byte)7 : (byte)local.DayOfWeek;
        return [0x7E, 0x06, 0x83, (byte)local.Hour, (byte)local.Minute, (byte)local.Second, weekday, 0x00, 0xEF];
    }
}
