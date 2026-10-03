using System.Runtime.InteropServices;

namespace GameTimer;

/// <summary>
/// 전역 키 감지 (WH_KEYBOARD_LL). 입력은 절대 차단하지 않고 항상 CallNextHookEx로 넘긴다.
/// 오토리피트로 인한 반복 KeyDown은 해당 키의 KeyUp 전까지 무시한다.
/// 나중에 Raw Input(RIDEV_INPUTSINK)으로 바꾸더라도 같은 KeyDown 이벤트만 제공하면 된다.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private readonly LowLevelKeyboardProc _proc; // GC 방지용으로 필드에 보관
    private const uint StaleMs = 1500;

    private readonly Dictionary<int, uint> _pressed = new(); // 눌린 키 → 마지막 KeyDown 시각
    private IntPtr _hookHandle = IntPtr.Zero;

    /// <summary>키가 새로 눌렸을 때(오토리피트 제외) 훅 스레드에서 동기 호출된다. 핸들러는 즉시 반환해야 한다.</summary>
    public event Action<int>? KeyDown;

    public KeyboardHook()
    {
        _proc = HookProc;
    }

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int msg = (int)wParam;
                int vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode
                uint time = (uint)Marshal.ReadInt32(lParam, 12); // KBDLLHOOKSTRUCT.time (ms)
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    // 오토리피트는 최대 1초 간격으로 이어지므로, 그보다 오래 입력이 없었다면 KeyUp을 놓친 것으로 보고 새 입력으로 처리
                    bool isRepeat = _pressed.TryGetValue(vk, out uint last) && time - last < StaleMs;
                    _pressed[vk] = time;
                    if (!isRepeat) KeyDown?.Invoke(vk);
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    _pressed.Remove(vk);
                }
            }
            catch
            {
                // 훅 콜백에서 예외가 새어 나가지 않게 한다.
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
