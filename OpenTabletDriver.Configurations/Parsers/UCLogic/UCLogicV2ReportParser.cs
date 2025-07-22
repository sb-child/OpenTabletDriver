using OpenTabletDriver.Tablet;
using OpenTabletDriver.Configurations.Parsers.UCLogic;

namespace OpenTabletDriver.Configurations.Parsers.UCLogic
{
    public class UCLogicV2ReportParser : IReportParser<IDeviceReport>
    {
        public IDeviceReport Parse(byte[] data)
        {
            return data[1] switch
            {
                0xe0 => new UCLogicAuxReport(data),
                // 0xf0 is for wheel data, reported in data[5]
                0xf0 => new UCLogicWheelReport(data),
                _ => new TiltTabletReport(data)
            };
        }
    }
}
