namespace Bledob;

public static class BleAddress
{
    public static ulong Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Bluetooth address is empty.");

        Span<char> hex = stackalloc char[text.Length];
        var count = 0;
        foreach (var character in text)
        {
            if (char.IsAsciiHexDigit(character))
                hex[count++] = character;
        }

        if (count != 12)
            throw new FormatException("A Bluetooth address is 6 bytes, such as BE:37:33:00:0D:0B.");

        return Convert.ToUInt64(new string(hex[..count]), 16);
    }

    public static string Format(ulong address)
    {
        Span<char> chars = stackalloc char[17];
        for (var i = 0; i < 6; i++)
        {
            var octet = (byte)((address >> (8 * (5 - i))) & 0xFF);
            octet.TryFormat(chars[(i * 3)..], out _, "X2");
            if (i < 5)
                chars[i * 3 + 2] = ':';
        }

        return new string(chars);
    }
}
