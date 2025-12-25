using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Eto.Drawing;
using Eto.Forms;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.UX.Controls.Generic;
using OpenTabletDriver.UX.Windows.Tablet.ViewModel;

namespace OpenTabletDriver.UX.Windows.Tablet
{
    using TDVM = TabletDebuggerViewModel;

    public sealed class TabletDebugger : DesktopForm
    {
        private const int _LARGE_FONT_SIZE = 14;
        private const int _FONT_SIZE = _LARGE_FONT_SIZE - 4;
        private const int _SPACING = 5;

        // spacing + padding + content padding
        private const int _GROUP_BOX_EXTRA_WIDTH = _SPACING * 6;

        private static readonly Font s_LargeMonospaceFont = Fonts.Monospace(_LARGE_FONT_SIZE);
        private static readonly Font s_MonospaceFont = Fonts.Monospace(_FONT_SIZE);

        private readonly SizeF _rawTabletLineFontSizeHex = s_MonospaceFont.Measure("FF ", 8);
        private readonly SizeF _rawTabletLineFontSizeBinary = s_MonospaceFont.Measure("10101010 ", 4);

        private readonly TabletVisualizer _tabletVisualizer = new();
        private readonly Label _deviceName = new() { Font = s_LargeMonospaceFont };
        private readonly Label _rawTabletData = new() { Font = s_MonospaceFont };
        private readonly Label _tabletData = new() { Font = s_MonospaceFont };
        private readonly Label _reportRate = new() { Font = s_LargeMonospaceFont };
        private readonly Label _reportsRecorded = new() { Font = s_MonospaceFont };
        private readonly Label _maxReportedPosition = new() { Font = s_MonospaceFont };
        private readonly CheckBox _enableDataRecording = new() { Text = "Enable Data Recording" };

        private readonly DebuggerGroup _reportsRecordedGroup = new() { Text = "Reports Recorded" };
        private readonly DebuggerGroup _rawTabletGroup;

        private readonly ButtonMenuItem _activeTablets = new() { Text = "Debugged Tablets", Visible = false };

        public TabletDebugger()
            : base(Application.Instance.MainForm)
        {
            SetTitle(App.Driver.Instance.GetTablets().Result);

            var viewmodel = new TDVM();
            DataContext = viewmodel;

            Content = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Spacing = 5,
                Padding = 5,
                MinimumSize = new Size(800, 420),
                Items =
                {
                    new StackLayoutItem
                    {
                        Expand = true,
                        Control = new StackLayout
                        {
                            Padding = _SPACING,
                            Spacing = _SPACING,
                            MinimumSize = new Size(200, -1),
                            Items =
                            {
                                new StackLayoutItem
                                {
                                    Expand = true,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    Control = new DebuggerGroup
                                    {
                                        Text = "Visualizer",
                                        Content = _tabletVisualizer,
                                    },
                                },
                                new StackLayout
                                {
                                    Orientation = Orientation.Horizontal,
                                    Items =
                                    {
                                        new DebuggerGroup
                                        {
                                            Text = "Device",
                                            Content = _deviceName,
                                        },
                                        new DebuggerGroup
                                        {
                                            Text = "Report Rate",
                                            Width = (int)s_LargeMonospaceFont.Measure("8888Hz").Width + _GROUP_BOX_EXTRA_WIDTH,
                                            Content = _reportRate,
                                        },
                                    },
                                },
                            },
                        },
                    },
                    new StackLayout
                    {
                        Padding = _SPACING,
                        Spacing = _SPACING,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Items =
                        {
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Options",
                                    Content = _enableDataRecording,
                                },
                            },
                            new StackLayoutItem
                            {
                                Control = _reportsRecordedGroup,
                            },
                            new StackLayoutItem
                            {
                                Expand = true,
                                Control = new DebuggerGroup
                                {
                                    ExpandContent = false,
                                    MinimumSize = new Size(-1, _FONT_SIZE * 20),
                                    Text = "Tablet Report",
                                    Width = _FONT_SIZE * 22,
                                    Content = _tabletData,
                                },
                            },
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Maximum Position",
                                    MinimumSize = (Size)s_MonospaceFont
                                        .Measure("Max Position: <123456, 78901>") + _GROUP_BOX_EXTRA_WIDTH,
                                    Content = _maxReportedPosition,
                                },
                            },
                        },
                    },
                    {
                        _rawTabletGroup = new DebuggerGroup
                        {
                            Padding = _SPACING,
                            Text = "Raw Tablet Data",
                            Width = GetWidthOfRawTabletDataGroupBox(),
                            MinimumSize = new Size(_FONT_SIZE * 22, -1),
                            ExpandContent = false,
                            Content = _rawTabletData,
                        }
                    },
                },
            };

            _rawTabletGroup.Content = _rawTabletData;
            _reportsRecordedGroup.Content = _reportsRecorded;

            SetupMenu();
            SetupBindings();

            KeyDown += (_, args) =>
            {
                if (args.Key == Keys.Escape)
                    Close();
            };

            // handle ActiveTabletsMenuItems updates directly
            viewmodel.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(TDVM.ActiveTabletReportMenuItems):
                        RefreshActiveTabletsMenu();
                        break;
                    case nameof(TDVM.DecodingMode):
                        ResizeRawBasedOnDecodingMode();
                        break;
                    default:
                        return;
                }
            };

            App.Driver.DeviceReport += viewmodel.HandleReport;
            App.Driver.TabletsChanged += HandleTabletsChanged;
            App.Driver.Instance.SetTabletDebug(true);
        }

        private void SetupBindings()
        {
            _deviceName.TextBinding.BindDataContext((TDVM vm) => vm.DeviceName, DualBindingMode.OneWay);
            _rawTabletData.TextBinding.BindDataContext((TDVM vm) => vm.RawTabletData, DualBindingMode.OneWay);
            _tabletData.TextBinding.BindDataContext((TDVM vm) => vm.DecodedTabletData, DualBindingMode.OneWay);
            _maxReportedPosition.TextBinding.BindDataContext((TDVM vm) => vm.MaxPosition, DualBindingMode.OneWay);
            _reportRate.TextBinding.BindDataContext((TDVM vm) => vm.ReportRateString, DualBindingMode.OneWay);
            _reportsRecorded.TextBinding.Convert(null, (int value) => $"{value}").BindDataContext((TDVM vm) => vm.ReportsRecorded, DualBindingMode.OneWay);
            _tabletVisualizer.ReportDataBinding.BindDataContext((TDVM vm) => vm.ReportData, DualBindingMode.OneWay);
            _enableDataRecording.CheckedBinding.BindDataContext((TDVM vm) => vm.DataRecordingEnabled);
            _reportsRecordedGroup.BindDataContext(x => x.Visible, (TDVM vm) => vm.HasReportsRecorded);
        }

        private void SetupMenu()
        {
            var decodingSwitchMenuItem = new ButtonMenuItem
            {
                Text = "Raw Data Mode",
            };

            foreach (var decodingMode in Enum.GetValues<TabletDebuggerEnums.DecodingMode>())
            {
                string modeName = decodingMode.ToString();

                var item = new CheckMenuItem();
                item.Text = modeName;
                item.BindDataContext(x => x.Checked,
                    Binding.Property((TDVM vm) => vm.DecodingMode).ToBool(decodingMode));

                decodingSwitchMenuItem.Items.Add(item);
            }

            Menu = new MenuBar
            {
                ApplicationItems = { decodingSwitchMenuItem },
                QuitItem = new ButtonMenuItem((_, _) => Application.Instance.AsyncInvoke(Close))
                {
                    Text = "Close Window",
                },
                Items =
                {
                    _activeTablets,
                },
            };
        }

        private void ResizeRawBasedOnDecodingMode()
        {
            if (DataContext is not TDVM viewmodel) return;

            _rawTabletGroup.Width = GetWidthOfRawTabletDataGroupBox(viewmodel.DecodingMode);
        }

        private void RefreshActiveTabletsMenu()
        {
            if (DataContext is not TDVM viewmodel) return;

            _activeTablets.Items.Clear();
            _activeTablets.Items.AddRange(viewmodel.ActiveTabletReportMenuItems);

            // hide if only 1 tablet active
            _activeTablets.Visible = _activeTablets.Items.Count > 1;
        }

        private int GetWidthOfRawTabletDataGroupBox(
            TabletDebuggerEnums.DecodingMode decodingMode = TabletDebuggerEnums.DecodingMode.Hex) =>
            decodingMode switch
            {
                TabletDebuggerEnums.DecodingMode.Binary => (int)_rawTabletLineFontSizeBinary.Width + _GROUP_BOX_EXTRA_WIDTH,
                TabletDebuggerEnums.DecodingMode.Hex => (int)_rawTabletLineFontSizeHex.Width + _GROUP_BOX_EXTRA_WIDTH,
                _ => throw new ArgumentOutOfRangeException(nameof(decodingMode)),
            };

        protected override async void OnClosing(CancelEventArgs e)
        {
            var viewmodel = DataContext as TDVM ?? throw new InvalidOperationException("Invalid data context");

            await App.Driver.Instance.SetTabletDebug(false);

            App.Driver.DeviceReport -= viewmodel.HandleReport;
            App.Driver.TabletsChanged -= HandleTabletsChanged;
            if (DataContext is IDisposable disposable)
                disposable.Dispose();

            base.OnClosing(e);
        }

        private void SetTitle(IEnumerable<TabletReference> tablets)
        {
            StringBuilder sb = new StringBuilder("Tablet Debugger");
            var tabletReferenceArr = tablets as TabletReference[] ?? tablets.ToArray();

            if (tabletReferenceArr.Length != 0)
            {
                int numTablets = Math.Min(tabletReferenceArr.Length, 3);
                sb.Append(" - ");
                sb.Append(string.Join(", ", tabletReferenceArr.Take(numTablets).Select(t => t.Properties.Name)));
            }
            Title = sb.ToString();
        }

        private void HandleTabletsChanged(object sender, IEnumerable<TabletReference> tablets) =>
            Application.Instance.AsyncInvoke(() => SetTitle(tablets));

        private class DebuggerGroup : Group
        {
            protected override Color VerticalBackgroundColor => base.HorizontalBackgroundColor;
        }

        private class TabletVisualizer : ScheduledDrawable
        {
            private static readonly Color s_AccentColor = SystemColors.Highlight;

            private DebugReportData ReportData { set; get; }
            private readonly List<int> _warnedDigitizers = [];

            public BindableBinding<TabletVisualizer, DebugReportData> ReportDataBinding
            {
                get
                {
                    return new BindableBinding<TabletVisualizer, DebugReportData>(
                        this,
                        c => c.ReportData,
                        (c, v) => c.ReportData = v
                    );
                }
            }

            protected override void OnNextFrame(PaintEventArgs e)
            {
                if (ReportData?.Tablet is not { } tablet) return;

                var graphics = e.Graphics;
                using (graphics.SaveTransformState())
                {
                    var digitizer = tablet.Properties.Specifications.Digitizer;
                    float yScale = (ClientSize.Height - _SPACING) / digitizer.Height;
                    float xScale = (ClientSize.Width - _SPACING) / digitizer.Width;
                    float finalScale = Math.Min(yScale, xScale);

                    var clientCenter = new PointF(ClientSize.Width, ClientSize.Height) / 2;
                    var tabletCenter = new PointF(digitizer.Width, digitizer.Height) / 2 * finalScale;

                    graphics.TranslateTransform(clientCenter - tabletCenter);

                    DrawBackground(graphics, finalScale, tablet);
                    DrawPosition(graphics, finalScale);
                }
            }

            private static void DrawBackground(Graphics graphics, float scale, TabletReference tablet)
            {
                var digitizer = tablet.Properties.Specifications.Digitizer;
                var bg = new RectangleF(0, 0, digitizer.Width, digitizer.Height) * scale;

                graphics.FillRectangle(SystemColors.WindowBackground, bg);
                graphics.DrawRectangle(s_AccentColor, bg);
            }

            private void DrawPosition(Graphics graphics, float scale)
            {
                object report = ReportData?.ToObject();
                var specifications = ReportData?.Tablet.Properties.Specifications;
                string tabletName = ReportData?.Tablet.Properties.Name;
                var touchDigitizerSpecification = specifications?.Touch;
                var absDigitizerSpecification = specifications?.Digitizer;

                if (report is IAbsolutePositionReport absReport)
                {
                    if (absDigitizerSpecification != null)
                    {
                        var tabletScale = CalculateTabletScale(absDigitizerSpecification, scale);
                        var position = new PointF(absReport.Position.X, absReport.Position.Y) * tabletScale;

                        var drawRect = RectangleF.FromCenter(position, new SizeF(_SPACING, _SPACING));
                        graphics.FillEllipse(s_AccentColor, drawRect);
                    }
                    else
                    {
                        string absHashName = tabletName + "abs";
                        int absHash = absHashName.GetHashCode();
                        if (!_warnedDigitizers.Contains(absHash))
                        {
                            _warnedDigitizers.Add(absHash);
                            Log.Write("TabletDebugger",
                                "Digitizer undefined in tablet configuration - unable to draw points",
                                LogLevel.Warning);
                        }
                    }
                }

                // touch reports
                if (report is ITouchReport touchReport)
                {
                    if (touchDigitizerSpecification != null)
                    {
                        var tabletScale = CalculateTabletScale(touchDigitizerSpecification, scale);

                        foreach (TouchPoint touchPoint in touchReport.Touches.Where((t) => t != null))
                        {
                            var position = new PointF(touchPoint.Position.X, touchPoint.Position.Y) * tabletScale;
                            var drawPen = new Pen(s_AccentColor, _SPACING / 2);
                            var drawRect = RectangleF.FromCenter(position, new SizeF(_SPACING * 2, _SPACING * 2));
                            graphics.DrawEllipse(drawPen, drawRect);
                        }
                    }
                    else
                    {
                        string touchHashName = tabletName + "touch";
                        int touchHash = touchHashName.GetHashCode();
                        if (!_warnedDigitizers.Contains(touchHash))
                        {
                            _warnedDigitizers.Add(touchHash);
                            Log.Write("TabletDebugger",
                                "Touch undefined in tablet configuration - unable to draw touch points",
                                LogLevel.Warning);
                        }
                    }
                }
            }

            private static SizeF CalculateTabletScale(DigitizerSpecifications digitizer, float scale)
            {
                var tabletMm = new SizeF(digitizer.Width, digitizer.Height);
                var tabletPx = new SizeF(digitizer.MaxX, digitizer.MaxY);
                return tabletMm / tabletPx * scale;
            }
        }
    }
}
