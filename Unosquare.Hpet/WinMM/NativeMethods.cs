using System.Runtime.InteropServices;

namespace Unosquare.Hpet.WinMM;

internal static partial class NativeMethods
{
    [LibraryImport(Constants.Library, EntryPoint = "timeSetEvent", SetLastError = true)]
    public static partial uint TimeSetEvent(uint msDelay, uint msResolution, WinMMTimerCallback callback, ref uint userCtx, uint eventType);

    [LibraryImport(Constants.Library, EntryPoint = "timeKillEvent", SetLastError = true)]
    public static partial void TimeKillEvent(uint uTimerId);

    [LibraryImport(Constants.Library, EntryPoint = "timeGetDevCaps", SetLastError = true)]
    public static partial uint TimeGetDevCaps(ref TimeCaps timeCaps, uint sizeTimeCaps);
}