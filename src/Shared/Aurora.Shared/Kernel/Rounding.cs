namespace Aurora.Shared.Kernel;

/// <summary>
/// The one rounding rule for calculated values shown to users. Money and percentages round to two
/// decimal places, half away from zero, so a figure reads the same in the UI, the CSV export and the tests.
/// </summary>
public static class Rounding
{
    public const int DisplayScale = 2;

    public static decimal Round(decimal value) => Math.Round(value, DisplayScale, MidpointRounding.AwayFromZero);
}
