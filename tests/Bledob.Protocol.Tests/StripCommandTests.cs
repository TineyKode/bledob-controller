using Bledob;

namespace Bledob.Protocol.Tests;

public class StripCommandTests
{
    [Fact]
    public void Turn_on_matches_the_bledob_frame()
    {
        Assert.Equal(
            new byte[] { 0x7E, 0x07, 0x04, 0xFF, 0x00, 0x01, 0x02, 0x01, 0xEF },
            StripCommands.TurnOn());
    }

    [Fact]
    public void Turn_off_matches_the_bledob_frame()
    {
        Assert.Equal(
            new byte[] { 0x7E, 0x07, 0x04, 0x00, 0x00, 0x00, 0x02, 0x01, 0xEF },
            StripCommands.TurnOff());
    }

    [Theory]
    [InlineData(255, 0, 0, new byte[] { 0x7E, 0x07, 0x05, 0x03, 0xFF, 0x00, 0x00, 0x10, 0xEF })]
    [InlineData(0, 255, 0, new byte[] { 0x7E, 0x07, 0x05, 0x03, 0x00, 0xFF, 0x00, 0x10, 0xEF })]
    [InlineData(0, 0, 255, new byte[] { 0x7E, 0x07, 0x05, 0x03, 0x00, 0x00, 0xFF, 0x10, 0xEF })]
    [InlineData(10, 20, 30, new byte[] { 0x7E, 0x07, 0x05, 0x03, 0x0A, 0x14, 0x1E, 0x10, 0xEF })]
    public void Color_packet_places_rgb_after_the_header(byte r, byte g, byte b, byte[] expected)
    {
        Assert.Equal(expected, StripCommands.Color(r, g, b));
    }

    [Theory]
    [InlineData(1, 0x01)]
    [InlineData(50, 0x32)]
    [InlineData(100, 0x64)]
    public void Brightness_packet_puts_percent_in_byte_3(int percent, byte expected)
    {
        var packet = StripCommands.Brightness(percent);
        Assert.Equal(new byte[] { 0x7E, 0x04, 0x01, expected, 0x01, 0xFF, 0x02, 0x01, 0xEF }, packet);
    }

    [Fact]
    public void Brightness_clamps_to_the_device_range()
    {
        Assert.Equal(StripCommands.Brightness(0), StripCommands.Brightness(-20));
        Assert.Equal(StripCommands.Brightness(100), StripCommands.Brightness(240));
    }

    [Theory]
    [InlineData("Three color jump", 0x87)]
    [InlineData("Seven color jump", 0x88)]
    [InlineData("Three color cross fade", 0x89)]
    [InlineData("Seven color cross fade", 0x8A)]
    [InlineData("Red fade", 0x8B)]
    [InlineData("Green fade", 0x8C)]
    [InlineData("Blue fade", 0x8D)]
    [InlineData("Yellow fade", 0x8E)]
    [InlineData("Cyan fade", 0x8F)]
    [InlineData("Magenta fade", 0x90)]
    [InlineData("White fade", 0x91)]
    [InlineData("Red green cross fade", 0x92)]
    [InlineData("Red blue cross fade", 0x93)]
    [InlineData("Green blue cross fade", 0x94)]
    [InlineData("Seven color strobe flash", 0x95)]
    [InlineData("Red strobe flash", 0x96)]
    [InlineData("Green strobe flash", 0x97)]
    [InlineData("Blue strobe flash", 0x98)]
    [InlineData("Yellow strobe flash", 0x99)]
    [InlineData("Cyan strobe flash", 0x9A)]
    [InlineData("Magenta strobe flash", 0x9B)]
    [InlineData("White strobe flash", 0x9C)]
    public void Effect_packet_uses_the_device_mode_id(string name, byte id)
    {
        Assert.Equal(
            new byte[] { 0x7E, 0x07, 0x03, id, 0x03, 0xFF, 0xFF, 0x00, 0xEF },
            StripCommands.Effect(name));
    }

    [Fact]
    public void Unknown_effect_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => StripCommands.Effect("disco inferno"));
    }

    [Fact]
    public void Every_device_mode_from_0x87_through_0x9C_has_a_name()
    {
        Assert.Equal(22, StripEffects.All.Count);
        Assert.Equal(0x87, StripEffects.All[0].Id);
        Assert.Equal(0x9C, StripEffects.All[^1].Id);
    }

    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(100, 0x64)]
    public void Speed_packet_puts_percent_in_byte_3(int percent, byte expected)
    {
        Assert.Equal(
            new byte[] { 0x7E, 0x07, 0x02, expected, 0xFF, 0xFF, 0xFF, 0x00, 0xEF },
            StripCommands.Speed(percent));
    }

    [Fact]
    public void Speed_clamps_to_the_device_range()
    {
        Assert.Equal(StripCommands.Speed(0), StripCommands.Speed(-5));
        Assert.Equal(StripCommands.Speed(100), StripCommands.Speed(180));
    }

    [Fact]
    public void Time_sync_writes_local_clock_and_iso_weekday()
    {
        // 2026-09-29 is a Tuesday, so the device weekday byte is 2.
        var packet = StripCommands.SyncTime(new DateTime(2026, 9, 29, 15, 32, 12));
        Assert.Equal(new byte[] { 0x7E, 0x06, 0x83, 15, 32, 12, 2, 0x00, 0xEF }, packet);
    }

    [Fact]
    public void Time_sync_encodes_sunday_as_7()
    {
        var packet = StripCommands.SyncTime(new DateTime(2024, 1, 7, 0, 0, 0));
        Assert.Equal(7, packet[6]);
    }

    [Fact]
    public async Task Link_forwards_the_on_frame_to_the_transport()
    {
        var writes = new List<byte[]>();
        var link = new StripLink((packet, _) =>
        {
            writes.Add(packet);
            return Task.CompletedTask;
        });

        await link.TurnOnAsync();

        Assert.Equal(StripCommands.TurnOn(), writes.Single());
    }
}
