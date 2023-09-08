using System.Runtime.InteropServices;

namespace Unosquare.Hpet.WinMM;

[StructLayout(LayoutKind.Sequential)]
internal struct TimeCaps
{
    public uint ResolutionMinPeriod;
    public uint ResolutionMaxPeriod;
};