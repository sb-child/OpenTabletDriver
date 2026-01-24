using System;
using Eto.Forms;
using OpenTabletDriver.Desktop.Profiles;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.UX.Controls.Generic;

namespace OpenTabletDriver.UX.Controls.Bindings
{
    public sealed class WheelBindingEditor : BindingEditor
    {
        public WheelBindingEditor(int wheelIndex)
        {
            wheelButtonGroup = new Group
            {
                Text = "Wheel Buttons",
                Content = wheelButtons = new BindingDisplayList
                {
                    Prefix = "Wheel Button Binding"
                }
            };

            this.Content = new Scrollable
            {
                Border = BorderType.None,
                Content = new StackLayout
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Spacing = 5,
                    Items =
                    {
                        new Group
                        {
                            Text = "Clockwise Rotation Settings",
                            Content = new StackLayout
                            {
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                Spacing = 5,
                                Items =
                                {
                                    new Group
                                    {
                                        Text = "Clockwise Rotation",
                                        Orientation = Orientation.Horizontal,
                                        ExpandContent = false,
                                        Content = clockwiseButton = new BindingDisplay()
                                    },
                                    new Group
                                    {
                                        Text = "Clockwise Rotation Threshold",
                                        ToolTip = "The minimum threshold in degrees in order for the assigned binding to activate.",
                                        Orientation = Orientation.Horizontal,
                                        Content = clockwiseThreshold = new FloatSlider()
                                        {
                                            Minimum = 1,
                                            Maximum = 360,
                                            SnapToTick = true,
                                            ClampValue = true,
                                        }
                                    }
                                }
                            }
                        },
                        new Group
                        {
                            Text = "Counter-Clockwise Rotation Settings",
                            Content = new StackLayout
                            {
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                Spacing = 5,
                                Items =
                                {
                                    new Group
                                    {
                                        Text = "Counter-Clockwise Rotation",
                                        ExpandContent = false,
                                        Orientation = Orientation.Horizontal,
                                        Content = counterClockwiseButton = new BindingDisplay()
                                    },
                                    new Group
                                    {
                                        Text = "Counter-Clockwise Rotation Threshold",
                                        ToolTip = "The minimum threshold in degrees in order for the assigned binding to activate.",
                                        Orientation = Orientation.Horizontal,
                                        Content = counterClockwiseThreshold = new FloatSlider()
                                        {
                                            Minimum = 1,
                                            Maximum = 360,
                                            SnapToTick = true,
                                            ClampValue = true,
                                        }
                                    }
                                }
                            }
                        },
                        wheelButtonGroup
                    }
                }
            };

            SettingsBinding.DataValueChanged += (sender, args) =>
            {
                if (sender is not DelegateBinding<BindingSettings> delegateBinding) return;

                this.DataContext = delegateBinding.DataValue.WheelBindings.Count > 0
                    ? delegateBinding.DataValue.WheelBindings[wheelIndex]
                    : null;
            };

            int? GetDegreesPerStep(TabletReference x)
            {
                if (x == null) return null;
                var spec = x.Properties.Specifications;

                if (spec.Wheels != null && spec.Wheels.Count >= wheelIndex && spec.Wheels![wheelIndex].StepCount != null)
                    return (int)(360d / x.Properties.Specifications.Wheels![wheelIndex].StepCount!.Value);

                throw new InvalidOperationException("Provided TabletReference does not define wheel step count for this wheel");
            }

            clockwiseThreshold.Bind(x => x.StepSize, TabletBinding.Convert(GetDegreesPerStep));
            counterClockwiseThreshold.Bind(x => x.StepSize, TabletBinding.Convert(GetDegreesPerStep));
            clockwiseThreshold.Bind(x => x.Minimum, TabletBinding.Convert(GetDegreesPerStep));
            counterClockwiseThreshold.Bind(x => x.Minimum, TabletBinding.Convert(GetDegreesPerStep));

            clockwiseButton.StoreBinding.BindDataContext((WheelBindingSettings wbs) =>
                wbs.ClockwiseRotation);
            counterClockwiseButton.StoreBinding.BindDataContext((WheelBindingSettings wbs) =>
                wbs.CounterClockwiseRotation);
            clockwiseThreshold.ValueBinding.BindDataContext((WheelBindingSettings wbs) =>
                wbs.ClockwiseActivationThreshold);
            counterClockwiseThreshold.ValueBinding.BindDataContext((WheelBindingSettings wbs) =>
                wbs.CounterClockwiseActivationThreshold);

            wheelButtons.ItemSourceBinding.BindDataContext((WheelBindingSettings wbs) => wbs.WheelButtons);
        }

        private Group wheelButtonGroup;
        private BindingDisplay clockwiseButton, counterClockwiseButton;
        private FloatSlider clockwiseThreshold, counterClockwiseThreshold;
        private BindingDisplayList wheelButtons;
    }
}
