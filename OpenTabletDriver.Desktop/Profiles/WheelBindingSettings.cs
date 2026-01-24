using Newtonsoft.Json;
using OpenTabletDriver.Desktop.Reflection;

#nullable enable

namespace OpenTabletDriver.Desktop.Profiles
{
    /// <summary>
    /// The settings for the wheel bindings of a single wheel
    /// </summary>
    public class WheelBindingSettings : ViewModel
    {
        private PluginSettingStoreCollection _wheelButtons = [];
        private float _ct = 15, _cct = 15; // TODO: default values for these based on entry wheel

        private PluginSettingStore?
            _clockwiseRotation,
            _counterClockwiseRotation;

        [JsonProperty(nameof(WheelButtons))]
        public PluginSettingStoreCollection WheelButtons
        {
            set => RaiseAndSetIfChanged(ref _wheelButtons, value);
            get => _wheelButtons;
        }

        [JsonProperty(nameof(ClockwiseRotation))]
        public PluginSettingStore? ClockwiseRotation
        {
            set => RaiseAndSetIfChanged(ref _clockwiseRotation, value);
            get => _clockwiseRotation;
        }

        [JsonProperty(nameof(ClockwiseActivationThreshold))]
        public float ClockwiseActivationThreshold
        {
            set => RaiseAndSetIfChanged(ref _ct, value);
            get => _ct;
        }

        [JsonProperty(nameof(CounterClockwiseRotation))]
        public PluginSettingStore? CounterClockwiseRotation
        {
            set => RaiseAndSetIfChanged(ref _counterClockwiseRotation, value);
            get => _counterClockwiseRotation;
        }

        [JsonProperty(nameof(CounterClockwiseActivationThreshold))]
        public float CounterClockwiseActivationThreshold
        {
            set => RaiseAndSetIfChanged(ref _cct, value);
            get => _cct;
        }
    }
}
