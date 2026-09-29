namespace Bledob;

public readonly record struct StripEffect(byte Id, string Name)
{
    public override string ToString() => Name;
}

public static class StripEffects
{
    public static IReadOnlyList<StripEffect> All { get; } =
    [
        new(0x87, "Three color jump"),
        new(0x88, "Seven color jump"),
        new(0x89, "Three color cross fade"),
        new(0x8A, "Seven color cross fade"),
        new(0x8B, "Red fade"),
        new(0x8C, "Green fade"),
        new(0x8D, "Blue fade"),
        new(0x8E, "Yellow fade"),
        new(0x8F, "Cyan fade"),
        new(0x90, "Magenta fade"),
        new(0x91, "White fade"),
        new(0x92, "Red green cross fade"),
        new(0x93, "Red blue cross fade"),
        new(0x94, "Green blue cross fade"),
        new(0x95, "Seven color strobe flash"),
        new(0x96, "Red strobe flash"),
        new(0x97, "Green strobe flash"),
        new(0x98, "Blue strobe flash"),
        new(0x99, "Yellow strobe flash"),
        new(0x9A, "Cyan strobe flash"),
        new(0x9B, "Magenta strobe flash"),
        new(0x9C, "White strobe flash"),
    ];

    public static byte IdFor(string name)
    {
        foreach (var effect in All)
        {
            if (string.Equals(effect.Name, name, StringComparison.OrdinalIgnoreCase))
                return effect.Id;
        }

        throw new ArgumentException($"Unknown effect '{name}'.", nameof(name));
    }
}
