#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Tablet.Touch;
using OpenTabletDriver.Plugin.Timing;

namespace OpenTabletDriver.Desktop.ViewModels;

public class TabletDebuggerViewModel : ViewModel, INotifyCollectionChanged, IDisposable
{
    private const TabletDebuggerEnums.DecodingMode _DEFAULT_DECODING_MODE =
        TabletDebuggerEnums.DecodingMode.Hex;

    private readonly HPETDeltaStopwatch _stopwatch = new();
    private readonly HashSet<string> _seenTablets = [];
    private readonly HashSet<string> _ignoredTablets = [];

    private FileStream? _tabletRecordingFileStream;
    private StreamWriter? _tabletRecordingStreamWriter;

    public TabletDebuggerViewModel()
    {
        _additionalStatistics.CollectionChanged += (sender, args) => this.CollectionChanged?.Invoke(sender, args);
    }

    public void HandleReport(object? sender, DebugReportData data) => ReportData = data;

    #region View Model Properties (and backing fields)
    private DebugReportData? _reportData;
    public DebugReportData? ReportData
    {
        get => _reportData;
        private set
        {
            // early exit if ignored
            if (value != null && _ignoredTablets.Contains(GetNameKeyForFilter(value.Tablet, value.Path))) return;

            RaiseAndSetIfChanged(ref _reportData, value);
            if (value == null) return;
            RaiseChanged(nameof(DeviceName));

            if (_seenTablets.Add(GetNameKeyForFilter(value.Tablet, value.Path)))
                RaiseChanged(nameof(SeenTablets));

            var timeDelta = _stopwatch.Restart();
            AdditionalStatistics["Report Rate"].SaveMinMax(timeDelta.TotalMilliseconds, "ms");
            HandleReportInterval(timeDelta);

            var dataObject = value.ToObject();

            if (dataObject is IAbsolutePositionReport absolutePositionReport)
                AdditionalStatistics["Tablet Position"].SaveMinMax(absolutePositionReport.Position);

            if (dataObject is ITouchReport touchReport)
                AdditionalStatistics["Touch Position"].SaveMinMax(touchReport.Touches);

            if (dataObject is ITabletReport tabletReport)
                AdditionalStatistics["Pressure"].SaveMinMax(tabletReport.Pressure);

            if (dataObject is IAuxReport auxReport)
                if (auxReport.AuxButtons.Length > 0)
                    AdditionalStatistics["Aux Buttons"].SaveButtons(auxReport.AuxButtons, value.Tablet);

            if (dataObject is IDeviceReport deviceReport)
            {
                SetRawTabletData(deviceReport);
                DecodedTabletData = ReportFormatter.GetStringFormat(deviceReport);
                HandleDataRecording(value, deviceReport, timeDelta);
            }
        }
    }

    public string DeviceName => ReportData?.Tablet.Properties.Name ?? string.Empty;

    private readonly Queue<double> _reportRates = new();
    private void HandleReportInterval(TimeSpan timeDelta)
    {
        _reportRates.Enqueue(timeDelta.TotalMilliseconds);
        if (_reportRates.Count > 100)
            _reportRates.Dequeue();

        RaiseChanged(nameof(ReportRateString));
    }

    private readonly Statistic _additionalStatistics = new("Additional Statistics");

    public Statistic AdditionalStatistics
    {
        get => _additionalStatistics;
        init => RaiseAndSetIfChanged(ref _additionalStatistics, value);
    }

    private double ReportRateAverage => 1000 / (_reportRates.Count > 0 ? _reportRates.Average() : 0);
    public string ReportRateString => $"{ReportRateAverage:0.00}";

    private string _rawTabletData = string.Empty;
    public string RawTabletData
    {
        get => _rawTabletData;
        private set => RaiseAndSetIfChanged(ref _rawTabletData, value);
    }

    private string _decodedTabletData = string.Empty;
    public string DecodedTabletData
    {
        get => _decodedTabletData;
        private set => RaiseAndSetIfChanged(ref _decodedTabletData, value);
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
        private set
        {
            RaiseAndSetIfChanged(ref _reportsRecorded, value);

            if (value > 0 && !HasReportsRecorded)
                HasReportsRecorded = true;
        }
    }

    private bool _hasReportsRecorded;
    public bool HasReportsRecorded
    {
        get => _hasReportsRecorded;
        private set => RaiseAndSetIfChanged(ref _hasReportsRecorded, value);
    }

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

    private bool _isVisualizerEnabled = true;
    public bool IsVisualizerEnabled
    {
        get => _isVisualizerEnabled;
        set => RaiseAndSetIfChanged(ref _isVisualizerEnabled, value);
    }

    private bool _showAdditionalStatistics;
    public bool ShowAdditionalStatistics
    {
        get => _showAdditionalStatistics;
        set => RaiseAndSetIfChanged(ref _showAdditionalStatistics, value);
    }

    public ReadOnlyCollection<string> SeenTablets => _seenTablets.ToArray().AsReadOnly();
    public HashSet<string> IgnoredTablets => _ignoredTablets;

    #endregion

    #region Class Functions

    private void SetRawTabletData(IDeviceReport report)
    {
        RawTabletData = DecodingMode switch
        {
            TabletDebuggerEnums.DecodingMode.Hex => ReportFormatter.GetStringRaw(report),
            TabletDebuggerEnums.DecodingMode.Binary => ReportFormatter.GetStringRawAsBinary(report),
            _ => throw new NotImplementedException(),
        };
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

    #endregion

    #region Static Functions

    private static string GetNameKeyForFilter(TabletReference tabletReference, string path) =>
        $"{tabletReference.Properties.Name}: {path}";

    #endregion

    #region Cleanup/Management

    private void CleanupLocks()
    {
        // TODO: print statistics before closing
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

    #endregion

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
}

public static class TabletDebuggerEnums
{
    public enum DecodingMode
    {
        Hex,
        Binary
    }
}

public class Statistic : ViewModel, INotifyCollectionChanged
{
    private readonly string _name = null!;
    private object? _value;
    private string? _unit;
    private string _valueStringFormat;
    private bool _hidden;
    private ObservableCollection<Statistic> _children = [];

    internal Statistic(string name, string? value = null, string? unit = null, string? valueStringFormat = null)
    {
        Name = name;
        Value = value;
        _unit = unit;
        _valueStringFormat = valueStringFormat ?? "{0}";
        Children.CollectionChanged += (sender, args) => CollectionChanged?.Invoke(sender, args);
    }

    /// <summary>
    /// If <c>Children</c> has any elements, this instance effectively becomes a group
    /// </summary>
    public ObservableCollection<Statistic> Children
    {
        get => _children;
        set => RaiseAndSetIfChanged(ref _children, value);
    }

    /// <summary>
    /// The key name of the instance
    /// </summary>
    public string Name
    {
        get => _name;
        private init => RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>
    /// The optional value of the instance
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            RaiseAndSetIfChanged(ref _value, value);
            RaiseChanged(nameof(ValueString));
        }
    }

    /// <summary>
    /// The unit intended to be appended to the string. Consumed by clients.
    /// </summary>
    public string? Unit
    {
        get => _unit;
        set => RaiseAndSetIfChanged(ref _unit, value);
    }

    /// <summary>
    /// The string format to use. See <see cref="string.Format(string, object?[])"/>.
    /// Used when retrieving <see cref="ValueString"/>
    /// </summary>
    public string ValueStringFormat
    {
        get => _valueStringFormat;
        set
        {
            RaiseAndSetIfChanged(ref _valueStringFormat, value);
            RaiseChanged(nameof(ValueString));
        }
    }

    /// <summary>
    /// Should the value normally be displayed/designed for in a UI
    /// </summary>
    public bool Hidden
    {
        get => _hidden;
        set => RaiseAndSetIfChanged(ref _hidden, value);
    }

    /// <summary>
    /// Formatted string of the value using the specified <see cref="ValueStringFormat"/>
    /// </summary>
    public string ValueString => Value != null ? string.Format(ValueStringFormat, Value) : "<null>";

    /// <summary>
    /// Retrieve the child group <see cref="Statistic"/> from the current instance
    /// </summary>
    /// <param name="childName">The <see cref="Name"/> of the child</param>
    public Statistic this[string childName]
    {
        get
        {
            var rv = Children.FirstOrDefault(x => x.Name == childName);
            if (rv == null) Children.Add(rv = new Statistic(childName));
            return rv;
        }
    }

    public Statistic SaveMinMax(double source, string? unit = null, int precision = 2) => SaveMinMax(source, Math.Min, Math.Max, unit, precision);
    public Statistic SaveMinMax(uint source, string? unit = null) => SaveMinMax(source, Math.Min, Math.Max, unit, null);
    public Statistic SaveMinMax(Vector2 source, string? unit = null, int precision = 0) => SaveMinMax(source, Vector2.Min, Vector2.Max, unit, precision);
    public Statistic SaveMinMax(TouchPoint?[] touchPoints)
    {
        var validTouchPoints = touchPoints.Where(x => x != null).Select(x => x!.Position).ToArray();
        if (validTouchPoints.Length == 0) return this;

        // Vector2 doesn't implement IComparable, do naive method:
        foreach (var touchPoint in validTouchPoints)
            SaveMinMax(touchPoint);

        return this;
    }

    private Statistic SaveMinMax<T>(T source, Func<T, T, T> minFunc, Func<T, T, T> maxFunc, string? unit, int? precision)
    {
        var min = this["Min"];
        min.Value = minFunc(source, (T)(min.Value ?? source)!);
        min.Unit = unit;
        var max = this["Max"];
        max.Value = maxFunc(source, (T)(max.Value ?? source)!);
        max.Unit = unit;

        if (precision != null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(precision.Value, nameof(precision));
            string format;

            if (precision.Value == 0)
                format = "{0:0}";
            else
                format = "{0:0." + string.Concat(Enumerable.Repeat("0", precision.Value)) + "}";

            min.ValueStringFormat = max.ValueStringFormat = format;
        }

        return this;
    }

    // null: valid (full 'false -> true -> false' transition happened)
    // false: only seen false
    // true: have seen false and then true but haven't seen false after true
    private readonly Dictionary<int, bool?> _seenButtons = new();

    public void SaveButtons(bool[] auxReportAuxButtons, TabletReference tabletReference)
    {
        uint expectedButtons = tabletReference.Properties.Specifications.AuxiliaryButtons?.ButtonCount ?? 0;

        // no buttons expected, don't log anything
        if (expectedButtons == 0) return;

        // skip if more than 1 button pressed
        if (auxReportAuxButtons.Take((int)expectedButtons).Count(x => x) > 1) return;

        for (int i = 0; i < expectedButtons; i++)
        {
            var buttonStatistic = this[$"{i}"];
            buttonStatistic.Hidden = true;

            bool buttonState = auxReportAuxButtons[i];

            if (!_seenButtons.TryGetValue(i, out bool? seenButton))
            {
                // don't add true-first buttons (click another button first, unless there's no other button to click)
                if (buttonState && expectedButtons > 1) continue;

                _seenButtons.Add(i, buttonState);
                buttonStatistic.Value = "Press Down";
                continue;
            }

            // null means this button has already been successfully processed
            if (seenButton == null) continue;

            if (buttonState && !seenButton.Value) // if button pressed and only seen false
            {
                _seenButtons[i] = true;
                buttonStatistic.Value = "Release Button";
                continue;
            }

            if (!buttonState && seenButton.Value) // if button released and last known state was true
            {
                _seenButtons[i] = null;
                buttonStatistic.Value = "PASS";
            }
        }

        var status = this["Status"];
        status.Value = string.Join(" ", _seenButtons.Select(SelectEmojisFromButtonBool));

        return;

        string SelectEmojisFromButtonBool(KeyValuePair<int, bool?> button) =>
            button.Value switch
            { // add 1 to key number to display human-friendly number
                null => $"{button.Key + 1}✔️",
                false => $"{button.Key + 1}↓️️",
                true => $"{button.Key + 1}↑",
            };
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
}
