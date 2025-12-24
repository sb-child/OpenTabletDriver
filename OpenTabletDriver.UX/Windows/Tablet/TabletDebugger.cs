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

        public TabletDebugger()
            : base(Application.Instance.MainForm)
        {
            Label deviceName, rawTablet, tablet, reportRate, reportsRecorded, maxReportedPosition;
            TabletVisualizer tabletVisualizer;
            DebuggerGroup reportsRecordedGroup;
            CheckBox enableDataRecording;

            SetTitle(App.Driver.Instance.GetTablets().Result);

            var viewmodel = new TDVM();
            this.DataContext = viewmodel;

            var visualizerGroup = new StackLayout
            {
                Padding = SPACING,
                Spacing = SPACING,
                Items =
                {
                    new StackLayoutItem
                    {
                        Expand = true,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Control = new DebuggerGroup
                        {
                            Text = "Visualizer",
                            Content = tabletVisualizer = new TabletVisualizer(),
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
                                Content = deviceName = new Label
                                {
                                    Font = Fonts.Monospace(LARGE_FONTSIZE)
                                }
                            },
                            new DebuggerGroup
                            {
                                Text = "Report Rate",
                                Width = LARGE_FONTSIZE * 6,
                                Content = reportRate = new Label
                                {
                                    Font = Fonts.Monospace(LARGE_FONTSIZE)
                                }
                            },
                        }
                    }
                },
            };

            var debugger = new StackLayout
            {
                Padding = SPACING,
                Spacing = SPACING,
                Items =
                {
                    new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Items =
                        {
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Options",
                                    Content = enableDataRecording = new CheckBox
                                    {
                                        Text = "Enable Data Recording"
                                    }
                                }
                            },
                            new StackLayoutItem
                            {
                                Control = reportsRecordedGroup = new DebuggerGroup
                                {
                                    Text = "Reports Recorded",
                                    Content = reportsRecorded = new Label
                                    {
                                        Font = Fonts.Monospace(FONTSIZE)
                                    }
                                }
                            },
                        }
                    },
                    new StackLayoutItem
                    {
                        Control = new DebuggerGroup
                        {
                            Text = "Raw Tablet Data",
                            Width = FONTSIZE * 33,
                            Content = rawTablet = new Label
                            {
                                Font = Fonts.Monospace(FONTSIZE)
                            }
                        }
                    },
                    new PaddingSpacerItem(),
                    new StackLayout
                    {
                        Items =
                        {
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Tablet Report",
                                    Width = FONTSIZE * 33,
                                    Height = FONTSIZE * 25,
                                    Content = tablet = new Label
                                    {
                                        Font = Fonts.Monospace(FONTSIZE)
                                    }
                                }
                            },
                            new StackLayoutItem
                            {
                                Control = new DebuggerGroup
                                {
                                    Text = "Maximum Position",
                                    Width = FONTSIZE * 33,
                                    Content = maxReportedPosition = new Label
                                    {
                                        Font = Fonts.Monospace(FONTSIZE)
                                    }
                                }
                            },
                        }
                    }
                },
            };

            var splitter = new Splitter
            {
                Orientation = Orientation.Horizontal,
                Width = 610 + 340 + SPACING,
                Panel1MinimumSize = 610,
                Panel2MinimumSize = 340,
                Height = 550,
                FixedPanel = SplitterFixedPanel.Panel2,
                Panel1 = visualizerGroup,
                Panel2 = debugger,
            };

            this.Content = new Scrollable
            {
                Content = splitter,
            };

            this.KeyDown += (_, args) =>
            {
                if (args.Key == Keys.Escape)
                    this.Close();
            };


            this.Menu = new MenuBar
            {
                QuitItem = new ButtonMenuItem((_, x) => Application.Instance.AsyncInvoke(this.Close))
                {
                    Text = "Close Window",
                },
                Items =
                {
                    _activeTablets,
                },
            };

            deviceName.TextBinding.BindDataContext((TDVM vm) => vm.DeviceName, DualBindingMode.OneWay);
            rawTablet.TextBinding.BindDataContext((TDVM vm) => vm.RawTabletData, DualBindingMode.OneWay);
            tablet.TextBinding.BindDataContext((TDVM vm) => vm.DecodedTabletData, DualBindingMode.OneWay);
            maxReportedPosition.TextBinding.BindDataContext((TDVM vm) => vm.MaxPosition, DualBindingMode.OneWay);
            reportRate.TextBinding.BindDataContext((TDVM vm) => vm.ReportRateString, DualBindingMode.OneWay);
            reportsRecorded.TextBinding.BindDataContext((TDVM vm) => vm.ReportsRecordedString, DualBindingMode.OneWay);
            tabletVisualizer.ReportDataBinding.BindDataContext((TDVM vm) => vm.ReportData, DualBindingMode.OneWay);
            enableDataRecording.CheckedBinding.BindDataContext((TDVM vm) => vm.DataRecordingEnabled);
            reportsRecordedGroup.BindDataContext(x => x.Visible, (TDVM vm) => vm.HasReportsRecorded);

            // handle ActiveTabletsMenuItems updates directly
            viewmodel.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(TDVM.ActiveTabletReportMenuItems):
                        RefreshActiveTabletsMenu();
                        break;
                    default:
                        return;
                }
            };

            App.Driver.DeviceReport += viewmodel.HandleReport;
            App.Driver.TabletsChanged += HandleTabletsChanged;
            App.Driver.Instance.SetTabletDebug(true);
        }

        private void RefreshActiveTabletsMenu()
        {
            if (DataContext is not TDVM viewmodel) return;

            _activeTablets.Items.Clear();
            _activeTablets.Items.AddRange(viewmodel.ActiveTabletReportMenuItems);

            // hide if only 1 tablet active
            _activeTablets.Visible = _activeTablets.Items.Count > 1;
        }

        private readonly ButtonMenuItem _activeTablets = new()
        {
            Text = "Debugged Tablets",
            Visible = false,
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
