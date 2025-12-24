using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Eto.Forms;
using JetBrains.Annotations;
using OpenTabletDriver.Desktop;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timing;
using OpenTabletDriver.UX.Tools;

#nullable enable

namespace OpenTabletDriver.UX.Windows.Tablet.ViewModel;

public class TabletDebuggerViewModel : Desktop.ViewModel, IDisposable
{
    private const TabletDebuggerEnums.DecodingMode _DEFAULT_DECODING_MODE =
        TabletDebuggerEnums.DecodingMode.Hex;

    private readonly HPETDeltaStopwatch _stopwatch = new();

    public void HandleReport(object sender, DebugReportData data) => ReportData = data;

    private string _deviceName = string.Empty;
    public string DeviceName
    {
        get => _deviceName;
        set => RaiseAndSetIfChanged(ref _deviceName, value);
    }

    private DebugReportData? _reportData;
    public DebugReportData? ReportData
    {
        get => _reportData;
        set
        {
            // early exit if ignored
            if (value != null && _ignoredTablets.Contains(value.Tablet.Properties.Name)) return;

            RaiseAndSetIfChanged(ref _reportData, value);
            if (value == null) return;

            if (_seenTablets.Add(value.Tablet.Properties.Name))
                RaiseChanged(nameof(ActiveTabletsMenuItems));

            var timeDelta = _stopwatch.Restart();
            ReportRate += (timeDelta.TotalMilliseconds - ReportRate) * 0.01f;

            DeviceName = value.Tablet.Properties.Name;

            var dataObject = value.ToObject();

            if (dataObject is ITabletReport tabletReport)
                HandleMaxPosition(tabletReport);

            if (dataObject is IDeviceReport deviceReport)
            {
                SetRawTabletData(deviceReport);
                DecodedTabletData = ReportFormatter.GetStringFormat(deviceReport);
                HandleDataRecording(value, deviceReport, timeDelta);
            }
        }
    }

    private void HandleDataRecording(DebugReportData reportData, IDeviceReport report, TimeSpan timeDelta)
    {
        if (!DataRecordingEnabled || _tabletRecordingStreamWriter == null)
            return;

        string? output = ReportFormatter.GetStringFormatOneLine(reportData.Tablet.Properties,
            report,
            timeDelta,
            reportData.Path);

        _tabletRecordingStreamWriter.WriteLine(output);
        ReportsRecorded++;
    }

    // BUG: this is mapped across all reporting tablets
    private double _reportRate;
    public double ReportRate
    {
        get => _reportRate;
        set
        {
            RaiseAndSetIfChanged(ref _reportRate, value);
            RaiseChanged(nameof(ReportRateString));
        }
    }

    public string ReportRateString => $"{Math.Round(1000 / ReportRate)}hz";

    private void SetRawTabletData(IDeviceReport report)
    {
        RawTabletData = DecodingMode switch
        {
            TabletDebuggerEnums.DecodingMode.Hex => ReportFormatter.GetStringRaw(report),
            TabletDebuggerEnums.DecodingMode.Binary => ReportFormatter.GetStringRawAsBinary(report),
            _ => throw new ArgumentOutOfRangeException(nameof(DecodingMode))
        };
    }

    private string _rawTabletData = string.Empty;
    public string RawTabletData
    {
        get => _rawTabletData;
        set => RaiseAndSetIfChanged(ref _rawTabletData, value);
    }

    private string _decodedTabletData = string.Empty;
    public string DecodedTabletData
    {
        get => _decodedTabletData;
        set => RaiseAndSetIfChanged(ref _decodedTabletData, value);
    }

    private TabletDebuggerEnums.DecodingMode _decodingMode = _DEFAULT_DECODING_MODE;
    public TabletDebuggerEnums.DecodingMode DecodingMode
    {
        get => _decodingMode;
        set => RaiseAndSetIfChanged(ref _decodingMode, value);
    }

    private int _reportsRecorded;
    public int ReportsRecorded
    {
        get => _reportsRecorded;
        set
        {
            RaiseAndSetIfChanged(ref _reportsRecorded, value);
            RaiseChanged(nameof(ReportsRecordedString));

            if (value > 0 && !HasReportsRecorded)
                HasReportsRecorded = true;
        }
    }

    private bool _hasReportsRecorded;
    public bool HasReportsRecorded
    {
        get => _hasReportsRecorded;
        set => RaiseAndSetIfChanged(ref _hasReportsRecorded, value);
    }

    public string ReportsRecordedString => $"{_reportsRecorded}";

    private FileStream? _tabletRecordingFileStream;
    private StreamWriter? _tabletRecordingStreamWriter;

    private bool _dataRecordingEnabled;
    public bool DataRecordingEnabled
    {
        get => _dataRecordingEnabled;
        [UsedImplicitly]
        set
        {
            RaiseAndSetIfChanged(ref _dataRecordingEnabled, value);

            if (value)
            {
                ReportsRecorded = 0;

                string fileName = "tablet-data_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".txt";
                _tabletRecordingFileStream = File.OpenWrite(Path.Join(AppInfo.Current.AppDataDirectory, fileName));
                _tabletRecordingStreamWriter = new StreamWriter(_tabletRecordingFileStream);
            }
            else
                CleanupLocks();
        }
    }

    private void HandleMaxPosition(ITabletReport report)
    {
        _maxPosition.X = Math.Max(report.Position.X, _maxPosition.X);
        _maxPosition.Y = Math.Max(report.Position.Y, _maxPosition.Y);
        RaiseChanged(nameof(MaxPosition));
    }

    private Vector2 _maxPosition = Vector2.Zero;
    public string MaxPosition => $"Max Position: {_maxPosition}";

    private readonly HashSet<string> _seenTablets = new();
    private readonly HashSet<string> _ignoredTablets = new();

    public IEnumerable<CheckMenuItem> ActiveTabletsMenuItems => GenerateMenuItem(_seenTablets, _ignoredTablets);

    private static IEnumerable<CheckMenuItem> GenerateMenuItem(HashSet<string> seenIDs, HashSet<string> ignoredIDs)
    {
        foreach (string id in seenIDs)
        {
            bool isIgnored = ignoredIDs.Contains(id);

            var command = new CheckCommand(HandleCheckCommand)
            {
                Checked = !isIgnored,
                Tag = (id, seenIDs, ignoredIDs),
            };

            var menuItem = new CheckMenuItem(command)
            {
                Text = id,
            };

            yield return menuItem;
        }
    }

    private static void HandleCheckCommand(object? sender, EventArgs e)
    {
        if (sender is not CheckCommand checkCommand) return;

        (string name, var seenIDs, var ignoredIDs) = (ValueTuple<string, HashSet<string>, HashSet<string>>)checkCommand.Tag;

        if (checkCommand.Checked)
            ignoredIDs.Remove(name);
        else if (seenIDs.Count > ignoredIDs.Count + 1) // don't allow removing last entry
            ignoredIDs.Add(name);
        else
            checkCommand.Checked = true;
    }

    private void CleanupLocks()
    {
        _tabletRecordingStreamWriter?.Dispose();
        _tabletRecordingStreamWriter = null;
        _tabletRecordingFileStream?.Dispose();
        _tabletRecordingFileStream = null;
    }

    public void Dispose()
    {
        CleanupLocks();
        GC.SuppressFinalize(this);
    }
}

public static class TabletDebuggerEnums
{
    public enum DecodingMode
    {
        Hex,
        Binary
    }
}
