using System.ComponentModel.DataAnnotations;

#nullable enable

namespace OpenTabletDriver.Plugin.Tablet
{
    /// <summary>
    /// Device specifications for a wheel.
    /// </summary>
    public class WheelSpecifications : AnalogSpecifications
    {
        /// <summary>
        /// For Absolute Wheels, The physical angle on the wheel's unit circle, corresponding to a reading of zero
        /// <para/>
        /// Relative wheels should leave this unset
        /// </summary>
        [Range(0, 360)]
        public float? AngleOfZeroReading { get; set; }

        /// <summary>
        /// Amount of wheel buttons
        /// </summary>
        [Required(ErrorMessage = $"{nameof(ButtonCount)} must be defined")]
        public uint ButtonCount { set; get; }
    }
}
