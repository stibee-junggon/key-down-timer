using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GameTimer;

/// <summary>설정 창 표의 한 줄.</summary>
public class EditRow : INotifyPropertyChanged
{
    private int? _key;
    public int? Key { get => _key; set { if (Set(ref _key, value)) OnPropertyChanged(nameof(KeyText)); } }

    private bool _isCapturing;
    public bool IsCapturing { get => _isCapturing; set { if (Set(ref _isCapturing, value)) OnPropertyChanged(nameof(KeyText)); } }

    /// <summary>캡처 중 누적된 패드 버튼 마스크 (표시용, 0이면 없음)</summary>
    private int _padCapture;
    public int PadCapture { get => _padCapture; set { if (Set(ref _padCapture, value)) OnPropertyChanged(nameof(KeyText)); } }

    public string KeyText => IsCapturing
        ? (PadCapture != 0 ? KeyNames.Get(GamepadPoller.GamepadFlag | PadCapture) + "..." : "키/패드 버튼을 누르세요...")
        : Key is int k ? KeyNames.Get(k) : "(키 선택)";

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _timeText = "";
    public string TimeText { get => _timeText; set => Set(ref _timeText, value); }

    private int _sound;
    public int Sound { get => _sound; set => Set(ref _sound, value); }

    private bool _enabled = true;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    private bool _keyError;
    public bool KeyError { get => _keyError; set => Set(ref _keyError, value); }

    private bool _timeError;
    public bool TimeError { get => _timeError; set => Set(ref _timeError, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name!);
        return true;
    }
}

public partial class SettingsWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly ObservableCollection<EditRow> _rows = new();
    private EditRow? _capturingRow;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        foreach (var t in settings.Timers)
        {
            AddRow(new EditRow { Key = t.Key, Name = t.Name, TimeText = TimeFormat.ToText(t.Seconds), Sound = t.Sound, Enabled = t.Enabled });
        }
        RowsList.ItemsSource = _rows;
        StopwatchNameBox.Text = settings.StopwatchName;
        VolumeSlider.Value = settings.Volume;
        OpacitySlider.Value = Math.Round(settings.Opacity * 100);
        Validate();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 제목 표시줄도 다크 모드로 (Windows 10 20H1+ / 11, 미지원이면 무시됨)
        int on = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref on, sizeof(int));
    }

    public void SetStopwatchName(string name) => StopwatchNameBox.Text = name;

    protected override void OnClosed(EventArgs e)
    {
        EndCapture();
        base.OnClosed(e);
    }

    private void AddRow(EditRow row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditRow.Key) or nameof(EditRow.TimeText) or nameof(EditRow.Enabled)) Validate();
            else if (e.PropertyName == nameof(EditRow.Sound)) Chime.Play(row.Sound, (int)VolumeSlider.Value);
        };
        _rows.Add(row);
    }

    private bool Validate()
    {
        var messages = new List<string>();

        var duplicates = _rows.Where(r => r.Enabled && r.Key != null)
                              .GroupBy(r => r.Key)
                              .Where(g => g.Count() > 1)
                              .Select(g => g.Key)
                              .ToHashSet();

        foreach (var row in _rows)
        {
            row.KeyError = row.Key == null || (row.Enabled && duplicates.Contains(row.Key));
            row.TimeError = !TimeFormat.TryParse(row.TimeText, out _);
        }

        if (_rows.Any(r => r.Key == null)) messages.Add("키가 지정되지 않은 타이머가 있습니다.");
        if (duplicates.Count > 0)
        {
            var names = string.Join(", ", duplicates.Select(k => KeyNames.Get(k!.Value)));
            messages.Add($"중복된 키가 있습니다: {names}");
        }
        if (_rows.Any(r => r.TimeError)) messages.Add("시간은 mm:ss 형식으로 1초 이상 입력하세요. (예: 01:30)");

        ErrorText.Text = string.Join(Environment.NewLine, messages);
        SaveButton.IsEnabled = messages.Count == 0;
        return messages.Count == 0;
    }

    // ---- 키 캡처 ----

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not EditRow row) return;
        if (_capturingRow == row)
        {
            EndCapture();
            return;
        }
        EndCapture();
        _capturingRow = row;
        row.IsCapturing = true;
        App.Instance.IsCapturingKey = true;
    }

    private void EndCapture()
    {
        if (_capturingRow != null)
        {
            _capturingRow.IsCapturing = false;
            _capturingRow.PadCapture = 0;
        }
        _capturingRow = null;
        App.Instance.IsCapturingKey = false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingRow == null) return;
        e.Handled = true; // 캡처 중 키가 버튼 클릭 등으로 처리되지 않게

        var key = e.Key switch
        {
            System.Windows.Input.Key.System => e.SystemKey,
            System.Windows.Input.Key.ImeProcessed => e.ImeProcessedKey,
            System.Windows.Input.Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        if (key == System.Windows.Input.Key.Escape)
        {
            EndCapture();
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk <= 0) return;

        _capturingRow.Key = vk;
        EndCapture();
    }

    /// <summary>
    /// 캡처 중 눌린 패드 버튼 집합을 누적하다가 모두 떼면 누적된 조합을 키로 등록한다. (App이 UI 스레드로 넘겨 호출)
    /// </summary>
    public void CaptureGamepad(int heldMask)
    {
        if (_capturingRow == null) return;
        _capturingRow.PadCapture |= heldMask;
        if (heldMask != 0 || _capturingRow.PadCapture == 0) return;
        _capturingRow.Key = GamepadPoller.GamepadFlag | _capturingRow.PadCapture;
        EndCapture();
    }

    private void Window_Deactivated(object? sender, EventArgs e) => EndCapture();

    // ---- 버튼 ----

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var row = new EditRow { TimeText = "01:00" };
        AddRow(row);
        Validate();
        EndCapture();
        _capturingRow = row;
        row.IsCapturing = true;
        App.Instance.IsCapturingKey = true;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not EditRow row) return;
        if (_capturingRow == row) EndCapture();
        _rows.Remove(row);
        Validate();
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e) => Chime.Play(0, (int)VolumeSlider.Value);

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        EndCapture();
        if (!Validate()) return;

        var settings = new AppSettings
        {
            Timers = _rows.Select(r =>
            {
                TimeFormat.TryParse(r.TimeText, out int secs);
                return new TimerEntry { Key = r.Key!.Value, Name = r.Name.Trim(), Seconds = secs, Sound = r.Sound, Enabled = r.Enabled };
            }).ToList(),
            Volume = (int)VolumeSlider.Value,
            Opacity = OpacitySlider.Value / 100.0,
            StopwatchName = string.IsNullOrWhiteSpace(StopwatchNameBox.Text) ? "스톱워치" : StopwatchNameBox.Text.Trim(),
        };

        try
        {
            App.Instance.ApplySettings(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "설정을 저장하지 못했습니다.\n" + ex.Message, "게임 타이머",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Close();
    }
}
