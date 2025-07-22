using JetBrains.Annotations;

namespace OpenTabletDriver.Tablet
{
    [PublicAPI]
    public interface IWheelReport : IDeviceReport
    {
        /// <summary>
        /// CW = true, CCW = false
        /// </summary>
        bool Wheel { get; set; }
    }
}
