using System.IO;
using System.Windows;

namespace GameTimer;

public partial class App : Application
{
    private Mutex? _mutex;
    private KeyboardHook? _hook;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;
    private RenameWindow? _renameWindow;

    public static App Instance => (App)Current;

    public AppSettings Settings { get; private set; } = new();

    /// <summary>설정 창에서 키를 캡처하는 동안에는 타이머를 트리거하지 않는다.</summary>
    public bool IsCapturingKey { get; set; }

    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ReportError(args.Exception);
            args.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ReportError(args.ExceptionObject as Exception);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Local\GameTimer.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            MessageBox.Show("게임 타이머가 이미 실행 중입니다.\n화면에 안 보이면 작업 관리자에서 GameTimer를 종료한 뒤 다시 실행하세요.",
                "게임 타이머", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Settings = SettingsStore.Load();
        _overlay = new OverlayWindow();
        _overlay.Closed += (_, _) => Shutdown();
        MainWindow = _overlay;
        _overlay.ApplySettings(Settings);
        _overlay.Show();

        _hook = new KeyboardHook();
        _hook.KeyDown += OnGlobalKeyDown;
        _hook.Install();

        // 첫 실행처럼 타이머가 없으면 설정 창을 바로 띄운다.
        if (Settings.Timers.Count == 0) OpenSettings();
    }

    private static void ReportError(Exception? ex)
    {
        string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameTimer");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), text);
        }
        catch { /* 로그 실패는 무시 */ }
        MessageBox.Show(ex?.ToString() ?? "알 수 없는 오류", "게임 타이머 오류", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _hook = null;
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
        base.OnExit(e);
    }

    // 훅 콜백 안에서 호출된다. 빠르게 반환하고 UI 작업은 Dispatcher로 넘긴다.
    private void OnGlobalKeyDown(int vk)
    {
        if (IsCapturingKey || _renameWindow != null) return;
        Dispatcher.BeginInvoke(() => _overlay?.Trigger(vk));
    }

    public void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(Settings);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            IsCapturingKey = false;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void OpenRename()
    {
        if (_renameWindow != null)
        {
            _renameWindow.Activate();
            return;
        }
        var anchor = _overlay == null ? Rect.Empty : new Rect(_overlay.Left, _overlay.Top, _overlay.ActualWidth, _overlay.ActualHeight);
        _renameWindow = new RenameWindow(Settings.StopwatchName, anchor);
        _renameWindow.Closed += (_, _) =>
        {
            _renameWindow = null;
        };
        _renameWindow.Show();
        _renameWindow.Activate();
    }

    public bool RenameStopwatch(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "스톱워치" : name.Trim();
        string old = Settings.StopwatchName;
        Settings.StopwatchName = name;
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex)
        {
            Settings.StopwatchName = old;
            if (_renameWindow != null)
                MessageBox.Show(_renameWindow, "이름을 저장하지 못했습니다.\n" + ex.Message, "게임 타이머",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show("이름을 저장하지 못했습니다.\n" + ex.Message, "게임 타이머",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _overlay?.SetStopwatchName(name);
        _settingsWindow?.SetStopwatchName(name);
        return true;
    }

    public void ApplySettings(AppSettings settings)
    {
        settings.Left = Settings.Left;
        settings.Top = Settings.Top;
        SettingsStore.Save(settings);
        Settings = settings;
        _overlay?.ApplySettings(settings);
    }

    public void SavePosition(double left, double top)
    {
        Settings.Left = left;
        Settings.Top = top;
        try { SettingsStore.Save(Settings); } catch { /* 위치 저장 실패는 무시 */ }
    }
}
