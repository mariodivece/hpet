namespace Unosquare.Hpet.WinMM;

internal delegate void WinMMTimerCallback(uint id, uint msg, ref uint userCtx, uint rsv1, uint rsv2);