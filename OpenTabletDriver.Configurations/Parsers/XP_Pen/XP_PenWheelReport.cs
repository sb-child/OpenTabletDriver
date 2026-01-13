using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Wheel;

namespace OpenTabletDriver.Configurations.Parsers.XP_Pen
{
    public struct XP_PenWheelReport : IRelativeWheelReport
    {
        public XP_PenWheelReport(byte[] report, ref byte previousWheelByte, int wheelIndex = 7)
        {
            Raw = report;
            // 0x01 for 1st wheel clockwise, 0x02 for counterclockwise, verified on XP Pen Artist 13.3 Pro V2
            // 0x10 for 2nd wheel clockwise, 0x20 for counterclockwise, verified on XP Pen Artist 22R Pro
            // TODO: Update for multi wheel support when implemented
            if (report[wheelIndex].IsBitSet(0) || report[wheelIndex].IsBitSet(4))
            {
                Delta = 1;
            }
            else if (report[wheelIndex].IsBitSet(1) || report[wheelIndex].IsBitSet(5))
            {
                Delta = -1;
            }
            else {
                Delta = null;
            }
            // The XP-Pen Deco 03 wheel sequence is goofy. To track clockwise vs counterclockwise, the previous
            // report is needed. For example, if report[wheelIndex] is 0xC0, and the previous report has report[wheelIndex]
            // set to 0x40, then the wheel is going clockwise. If however the previous report has report[wheelIndex]
            // set to 0x80, then the wheel is going counterclockwise.
            //
            // Because of this, we essentially need to handle each possible byte seperately. I apologize in advance for
            // anyone looking at this.
            //
            // Perhaps only God knows why on earth the firmware devs for the Deco 03 did it this way.
            switch ((report[wheelIndex], previousWheelByte)) {
                case (0x00, 0x80):
                case (0x40, 0x00):
                case (0xC0, 0x40):
                case (0x80, 0x40):
                    Delta = 1;
                    break;

                case (0x00, 0x40):
                case (0x40, 0xC0):
                case (0xC0, 0x80):
                case (0x80, 0x00):
                    Delta = -1;
                    break;

                default:
                    Delta = null;
                    break;
            }

            // Set for the next report
            previousWheelByte = report[wheelIndex];
        }

        public byte[] Raw { get; set; }
        public int? Delta { set; get; }
    }
}
