using JetBrains.Annotations;
using OpenTabletDriver.Tablet;

namespace OpenTabletDriver.Configurations.Parsers.UCLogic
{
    [PublicAPI]
    public struct UCLogicWheelReport : IWheelReport
    {
        public UCLogicWheelReport(byte[] report)
        {
            Raw = report;
            Wheel = report[5] == 0x01;
        }

        public byte[] Raw { set; get; }
        public bool Wheel { set; get; }
    }
}
