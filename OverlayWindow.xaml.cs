using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameTimer;

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly ObservableCollection<TimerRow> _rows = new();
    private readonly StopwatchRow _stopwatch = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _ticker;
    private int _volume;
    private bool _endingNameEdit;
    private IntPtr _foregroundBeforeEdit;

    /// <summary>이름 편집 중에는 타이머를 트리거하지 않는다.</summary>
    public bool IsEditingName { get; private set; }

    public OverlayWindow()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;
        StopwatchBorder.DataContext = _stopwatch;
        _ticker = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnTick, Dispatcher);
        _ticker.Start();
        Deactivated += (_, _) => EndNameEdit(true);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 포커스를 뺏지 않는 도구 창으로 만든다 (게임 입력 방해 방지).
        SetExStyle(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW, true);
    }

    private void SetExStyle(long flags, bool on)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(on ? ex | flags : ex & ~flags));
    }

    public void ApplySettings(AppSettings settings)
    {
        _volume = settings.Volume;
        byte alpha = (byte)Math.Round(Math.Clamp(settings.Opacity, 0.2, 1.0) * 255);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x10, 0x10, 0x12));

        _stopwatch.Name = settings.StopwatchName;

        _rows.Clear();
        foreach (var t in settings.Timers.Where(t => t.Enabled)) _rows.Add(new TimerRow(t));
        EmptyHint.Text = settings.Timers.Count == 0 ? "⚙에서 타이머를 추가하세요" : "활성화된 타이머가 없습니다";
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        RestorePosition(settings);
    }

    public void SetStopwatchName(string name) => _stopwatch.Name = name;

    public void Trigger(int vk)
    {
        var now = _clock.Elapsed;
        foreach (var row in _rows)
        {
            if (row.Key == vk) row.Start(now);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        _stopwatch.Update(now);
        TimerRow? finished = null;
        foreach (var row in _rows)
        {
            if (row.Update(now)) finished ??= row;
        }
        // SoundPlayer는 동시 재생이 안 되므로 먼저 끝난 행의 소리 하나만 재생한다.
        if (finished != null) Chime.Play(finished.Sound, _volume);
    }

    private void RestorePosition(AppSettings settings)
    {
        double left = SystemParameters.WorkArea.Right - Width - 20;
        double top = SystemParameters.WorkArea.Top + 100;

        if (settings.Left is double l && settings.Top is double t)
        {
            // 최소 40px 정도는 화면 안에 보여야 복원한다.
            double vl = SystemParameters.VirtualScreenLeft;
            double vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth;
            double vb = vt + SystemParameters.VirtualScreenHeight;
            if (l + 40 <= vr && l + Width - 40 >= vl && t >= vt && t + 20 <= vb)
            {
                left = l;
                top = t;
            }
        }
        Left = left;
        Top = top;
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 더블클릭의 두 번째 클릭에서는 DragMove하지 않는다.
        if (e.ButtonState != MouseButtonState.Pressed || e.ClickCount != 1) return;
        DragMove();
        App.Instance.SavePosition(Left, Top);
    }

    private void StopwatchName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        e.Handled = true;
        if (IsEditingName) return;

        IsEditingName = true;
        StopwatchNameBox.Text = _stopwatch.Name;
        StopwatchNameText.Visibility = Visibility.Hidden;
        StopwatchNameBox.Visibility = Visibility.Visible;

        // 키 입력을 받으려면 편집하는 동안만 창 활성화를 허용한다.
        _foregroundBeforeEdit = GetForegroundWindow();
        SetExStyle(WS_EX_NOACTIVATE, false);
        Activate();
        SetForegroundWindow(new WindowInteropHelper(this).Handle);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsEditingName) return;
            Keyboard.Focus(StopwatchNameBox);
            StopwatchNameBox.SelectAll();
        });
    }

    private void StopwatchNameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) EndNameEdit(true);
        else if (e.Key == Key.Escape) EndNameEdit(false);
        else return;
        e.Handled = true;
    }

    private void StopwatchNameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => EndNameEdit(true);

    private void EndNameEdit(bool save)
    {
        // 저장 실패 MessageBox나 TextBox 숨김으로 인한 Deactivated/LostKeyboardFocus 재진입을 막는다.
        if (!IsEditingName || _endingNameEdit) return;
        _endingNameEdit = true;
        try
        {
            if (save && StopwatchNameBox.Text != _stopwatch.Name) App.Instance.RenameStopwatch(StopwatchNameBox.Text);
            StopwatchNameBox.Visibility = Visibility.Collapsed;
            StopwatchNameText.Visibility = Visibility.Visible;
            SetExStyle(WS_EX_NOACTIVATE, true);
            // Enter/Esc로 끝냈으면 편집 전 창(게임)에 포커스를 돌려준다.
            if (IsActive && _foregroundBeforeEdit != IntPtr.Zero) SetForegroundWindow(_foregroundBeforeEdit);
            _foregroundBeforeEdit = IntPtr.Zero;
            IsEditingName = false;
        }
        finally
        {
            _endingNameEdit = false;
        }
    }

    private void StopwatchToggle_Click(object sender, RoutedEventArgs e) => _stopwatch.Toggle(_clock.Elapsed);

    private void StopwatchReset_Click(object sender, RoutedEventArgs e) => _stopwatch.Reset(_clock.Elapsed);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Instance.OpenSettings();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
