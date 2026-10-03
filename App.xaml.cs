using System.Windows;

namespace GameTimer;

public partial class App : Application
{
    private Mutex? _mutex;
    private KeyboardHook? _hook;
    private OverlayWindow? _overlay;
    private SettingsWindow? _settingsWindow;

    public static App Instance => (App)Current;

    public AppSettings Settings { get; private set; } = new();

    /// <summary>설정 창에서 키를 캡처하는 동안에는 타이머를 트리거하지 않는다.</summary>
    public bool IsCapturingKey { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Local\GameTimer.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
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
        if (IsCapturingKey) return;
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
