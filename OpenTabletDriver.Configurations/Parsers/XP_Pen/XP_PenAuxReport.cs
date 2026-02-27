using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.Configurations.Parsers.XP_Pen
{
    public struct XP_PenAuxReport : IAuxReport
    {
        public XP_PenAuxReport(byte[] report, int auxIndex = 2)
        {
            Raw = report;

            AuxButtons =
            [
                report[auxIndex].IsBitSet(0),
                report[auxIndex].IsBitSet(1),
                report[auxIndex].IsBitSet(2),
                report[auxIndex].IsBitSet(3),
                report[auxIndex].IsBitSet(4),
                report[auxIndex].IsBitSet(5),
                report[auxIndex].IsBitSet(6),
                report[auxIndex].IsBitSet(7),
                report[auxIndex + 1].IsBitSet(0),
                report[auxIndex + 1].IsBitSet(1),
                report[auxIndex + 1].IsBitSet(2),
                report[auxIndex + 1].IsBitSet(3),
                report[auxIndex + 1].IsBitSet(4),
                report[auxIndex + 1].IsBitSet(5),
                report[auxIndex + 1].IsBitSet(6),
                report[auxIndex + 1].IsBitSet(7),
                report[auxIndex + 2].IsBitSet(0),
                report[auxIndex + 2].IsBitSet(1),
                report[auxIndex + 2].IsBitSet(2),
                report[auxIndex + 2].IsBitSet(3),
            };
        }

        public bool[] AuxButtons { set; get; }
        public byte[] Raw { set; get; }
        public int[] AnalogDeltas { get; set; }
    }
}
