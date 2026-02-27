using System.Diagnostics.CodeAnalysis;

namespace OpenTabletDriver.Native.Linux.Timers
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum TimerFlag
    {
        Default = 0,
        AbsoluteTime = 1 << 0,
        CancelOnSet = 1 << 1
    }
}
