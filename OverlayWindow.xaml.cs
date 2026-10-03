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

    private readonly ObservableCollection<TimerRow> _rows = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _ticker;
    private int _volume;

    public OverlayWindow()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;
        _ticker = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, OnTick, Dispatcher);
        _ticker.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 포커스를 뺏지 않는 도구 창으로 만든다 (게임 입력 방해 방지).
        var hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
    }

    public void ApplySettings(AppSettings settings)
    {
        _volume = settings.Volume;
        byte alpha = (byte)Math.Round(Math.Clamp(settings.Opacity, 0.2, 1.0) * 255);
        RootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0x10, 0x10, 0x12));

        _rows.Clear();
        foreach (var t in settings.Timers) _rows.Add(new TimerRow(t));
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        RestorePosition(settings);
    }

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
        bool anyFinished = false;
        foreach (var row in _rows)
        {
            if (row.Update(now)) anyFinished = true;
        }
        if (anyFinished) Chime.Play(_volume);
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
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        App.Instance.SavePosition(Left, Top);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => App.Instance.OpenSettings();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
