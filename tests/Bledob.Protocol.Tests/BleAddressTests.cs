using Bledob;

namespace Bledob.Protocol.Tests;

public class BleAddressTests
{
    [Theory]
    [InlineData("BE:37:33:00:0D:0B")]
    [InlineData("be:37:33:00:0d:0b")]
    [InlineData("BE-37-33-00-0D-0B")]
    [InlineData("be3733000d0b")]
    public void Parse_accepts_the_forms_windows_and_phones_show(string text)
    {
        Assert.Equal("BE:37:33:00:0D:0B", BleAddress.Format(BleAddress.Parse(text)));
    }

    [Fact]
    public void Parse_rejects_a_short_value()
    {
        Assert.Throws<FormatException>(() => BleAddress.Parse("BE:37"));
    }
}
