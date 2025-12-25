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
        const int LARGE_FONTSIZE = 14;
        const int FONTSIZE = LARGE_FONTSIZE - 4;
        const int SPACING = 5;

        private readonly SizeF _rawTabletFontSizeSmallHex = MeasureMonospaceString("00 ", 8);
        private readonly SizeF _rawTabletFontSizeBigBinary = MeasureMonospaceString("00000000 ", 4);

        private static readonly Font s_LargeMonospaceFont = Fonts.Monospace(LARGE_FONTSIZE);
        private static readonly Font s_MonospaceFont = Fonts.Monospace(FONTSIZE);

        private readonly TabletVisualizer _tabletVisualizer = new();
        private readonly Label _deviceName = new() { Font = s_LargeMonospaceFont };
        private readonly Label _rawTablet = new() { Font = s_MonospaceFont };
        private readonly Label _tablet = new() { Font = s_MonospaceFont };
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
            this.DataContext = viewmodel;

            this.Content = new StackLayout
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
                            Padding = SPACING,
                            Spacing = SPACING,
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
                                            Width = (int)s_LargeMonospaceFont.MeasureString("8888Hz").Width + SPACING * 6,
                                            Content = _reportRate,
                                        },
                                    }
                                }
                            },
                        }
                    },
                    new StackLayout
                    {
                        Padding = SPACING,
                        Spacing = SPACING,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Items =
                        {
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Options",
                                    Content = _enableDataRecording,
                                }
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
                                    MinimumSize = new Size(-1, FONTSIZE * 20),
                                    Text = "Tablet Report",
                                    Width = FONTSIZE * 22,
                                    Content = _tablet,
                                }
                            },
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Maximum Position",
                                    Content = _maxReportedPosition,
                                }
                            },
                        }
                    },
                    {
                        _rawTabletGroup = new DebuggerGroup
                        {
                            Padding = SPACING,
                            Text = "Raw Tablet Data",
                            Width = GetWidthOfRawTabletDataGroupBox(),
                            MinimumSize = new Size(FONTSIZE * 22, -1),
                            ExpandContent = false,
                            Content = _rawTablet,
                        }
                    }
                },
            };

            _rawTabletGroup.Content = _rawTablet;
            _reportsRecordedGroup.Content = _reportsRecorded;

            SetupMenu();
            SetupBindings();

            this.KeyDown += (_, args) =>
            {
                if (args.Key == Keys.Escape)
                    this.Close();
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
            _rawTablet.TextBinding.BindDataContext((TDVM vm) => vm.RawTabletData, DualBindingMode.OneWay);
            _tablet.TextBinding.BindDataContext((TDVM vm) => vm.DecodedTabletData, DualBindingMode.OneWay);
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

            AddDecodingModes(decodingSwitchMenuItem);

            this.Menu = new MenuBar
            {
                ApplicationItems = { decodingSwitchMenuItem },
                QuitItem = new ButtonMenuItem((_, x) => Application.Instance.AsyncInvoke(this.Close))
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

        private static void AddDecodingModes(ButtonMenuItem decodingSwitchMenuItem)
        {
            foreach (var decodingMode in Enum.GetValues<TabletDebuggerEnums.DecodingMode>())
            {
                string modeName = decodingMode.ToString();

                var item = new CheckMenuItem();
                item.Text = modeName;
                item.BindDataContext(x => x.Checked,
                    Binding.Property((TDVM vm) => vm.DecodingMode).ToBool(decodingMode));

                decodingSwitchMenuItem.Items.Add(item);
            }
        }

        private void RefreshActiveTabletsMenu()
        {
            if (DataContext is not TDVM viewmodel) return;

            _activeTablets.Items.Clear();
            _activeTablets.Items.AddRange(viewmodel.ActiveTabletReportMenuItems);

            // hide if only 1 tablet active
            _activeTablets.Visible = _activeTablets.Items.Count > 1;
        }

        // TODO: as extension method on Eto.Drawing.Fonts or some other way?
        private static SizeF MeasureMonospaceString(string text, int repeats = 1) =>
            s_MonospaceFont.MeasureString(
                repeats > 1
                    ? string.Concat(Enumerable.Repeat(text, repeats))
                    : text);

        private int GetWidthOfRawTabletDataGroupBox(
            TabletDebuggerEnums.DecodingMode decodingMode = TabletDebuggerEnums.DecodingMode.Hex) =>
            decodingMode switch
            {
                TabletDebuggerEnums.DecodingMode.Binary => (int)_rawTabletFontSizeBigBinary.Width + SPACING * 6,
                TabletDebuggerEnums.DecodingMode.Hex => (int)_rawTabletFontSizeSmallHex.Width + SPACING * 6,
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
                var numTablets = Math.Min(tabletReferenceArr.Length, 3);
                sb.Append(" - ");
                sb.Append(string.Join(", ", tabletReferenceArr.Take(numTablets).Select(t => t.Properties.Name)));
            }
            this.Title = sb.ToString();
        }

        private void HandleTabletsChanged(object sender, IEnumerable<TabletReference> tablets) =>
            Application.Instance.AsyncInvoke(() => SetTitle(tablets));

        private class DebuggerGroup : Group
        {
            protected override Color VerticalBackgroundColor => base.HorizontalBackgroundColor;
        }

        private class TabletVisualizer : ScheduledDrawable
        {
            private static readonly Color AccentColor = SystemColors.Highlight;

            public DebugReportData ReportData { set; get; }
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
                if (ReportData?.Tablet is TabletReference tablet)
                {
                    var graphics = e.Graphics;
                    using (graphics.SaveTransformState())
                    {
                        var digitizer = tablet.Properties.Specifications.Digitizer;
                        var yScale = (this.ClientSize.Height - SPACING) / digitizer.Height;
                        var xScale = (this.ClientSize.Width - SPACING) / digitizer.Width;
                        var finalScale = Math.Min(yScale, xScale);

                        var clientCenter = new PointF(this.ClientSize.Width, this.ClientSize.Height) / 2;
                        var tabletCenter = new PointF(digitizer.Width, digitizer.Height) / 2 * finalScale;

                        graphics.TranslateTransform(clientCenter - tabletCenter);

                        DrawBackground(graphics, finalScale, tablet);
                        DrawPosition(graphics, finalScale, tablet);
                    }
                }
            }

            protected void DrawBackground(Graphics graphics, float scale, TabletReference tablet)
            {
                var digitizer = ReportData.Tablet.Properties.Specifications.Digitizer;
                var bg = new RectangleF(0, 0, digitizer.Width, digitizer.Height) * scale;

                graphics.FillRectangle(SystemColors.WindowBackground, bg);
                graphics.DrawRectangle(AccentColor, bg);
            }

            protected void DrawPosition(Graphics graphics, float scale, TabletReference tablet)
            {
                var report = ReportData?.ToObject();
                var specifications = ReportData?.Tablet.Properties.Specifications;
                var tabletName = ReportData?.Tablet.Properties.Name;
                var touchDigitizerSpecification = specifications?.Touch;
                var absDigitizerSpecification = specifications?.Digitizer;

                if (report is IAbsolutePositionReport absReport)
                {
                    if (absDigitizerSpecification != null)
                    {
                        var tabletScale = calculateTabletScale(absDigitizerSpecification, scale);
                        var position = new PointF(absReport.Position.X, absReport.Position.Y) * tabletScale;

                        var drawRect = RectangleF.FromCenter(position, new SizeF(SPACING, SPACING));
                        graphics.FillEllipse(AccentColor, drawRect);
                    }
                    else
                    {
                        var absHashName = tabletName + "abs";
                        var absHash = absHashName.GetHashCode();
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
                        var tabletScale = calculateTabletScale(touchDigitizerSpecification, scale);

                        foreach (TouchPoint touchPoint in touchReport.Touches.Where((t) => t != null))
                        {
                            var position = new PointF(touchPoint.Position.X, touchPoint.Position.Y) * tabletScale;
                            var drawPen = new Pen(AccentColor, SPACING / 2);
                            var drawRect = RectangleF.FromCenter(position, new SizeF(SPACING * 2, SPACING * 2));
                            graphics.DrawEllipse(drawPen, drawRect);
                        }
                    }
                    else
                    {
                        var touchHashName = tabletName + "touch";
                        var touchHash = touchHashName.GetHashCode();
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

            private static SizeF calculateTabletScale(DigitizerSpecifications digitizer, float scale)
            {
                var tabletMm = new SizeF(digitizer.Width, digitizer.Height);
                var tabletPx = new SizeF(digitizer.MaxX, digitizer.MaxY);
                return tabletMm / tabletPx * scale;
            }
        }
    }
}
