using System.Runtime.InteropServices;

namespace Unosquare.Hpet.WinMM;

internal static class Constants
{
    public const int EventTypeSingle = 0;
    public const uint MaximumPossiblePrecision = 0;
    public const uint OneMillisecond = 1;
    public const string Library = "winmm.dll";

    public static readonly uint SizeOfTimeCaps = (uint)Marshal.SizeOf<TimeCaps>();
}
