using System.Runtime.InteropServices;

namespace GameTimer;

/// <summary>
/// 엑스박스(XInput) 패드 버튼 감지. 백그라운드 스레드에서 폴링하며 입력은 차단하지 않는다.
/// 코드 체계: GamepadFlag | XInput 버튼 비트 (LT/RT는 미사용 비트 0x0400/0x0800). 패드 번호는 구분하지 않는다.
/// </summary>
public sealed class GamepadPoller : IDisposable
{
    public const int GamepadFlag = 0x10000;
    public const int LeftTriggerBit = 0x0400;
    public const int RightTriggerBit = 0x0800;

    private const uint ERROR_SUCCESS = 0;
    private const int MaxUsers = 4;
    private const int PollMs = 16;
    private const int ReconnectMs = 1000;
    private const byte TriggerPress = 128;
    private const byte TriggerRelease = 64;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

    private readonly int[] _prevButtons = new int[MaxUsers];
    private readonly bool[] _connected = new bool[MaxUsers];
    private readonly long[] _nextRetry = new long[MaxUsers]; // 미연결 슬롯 재확인 시각 (Environment.TickCount64)
    private Thread? _thread;
    private volatile bool _stop;

    /// <summary>패드 버튼이 새로 눌렸을 때 폴링 스레드에서 호출된다. 핸들러는 즉시 반환해야 한다.</summary>
    public event Action<int>? ButtonDown;

    public static bool IsGamepadCode(int code) => code >= GamepadFlag;

    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "GamepadPoller" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        _thread?.Join(500);
        _thread = null;
    }

    private void Run()
    {
        try
        {
            while (!_stop)
            {
                long now = Environment.TickCount64;
                for (int i = 0; i < MaxUsers; i++)
                {
                    if (!_connected[i] && now < _nextRetry[i]) continue;
                    Poll(i, now);
                }
                Thread.Sleep(PollMs);
            }
        }
        catch (DllNotFoundException) { /* XInput 없음 → 패드 기능 비활성 */ }
        catch (EntryPointNotFoundException) { }
    }

    private void Poll(int user, long now)
    {
        if (XInputGetState((uint)user, out var state) != ERROR_SUCCESS)
        {
            _connected[user] = false;
            _prevButtons[user] = 0;
            _nextRetry[user] = now + ReconnectMs;
            return;
        }
        _connected[user] = true;

        int prev = _prevButtons[user];
        int buttons = state.Gamepad.wButtons & ~(LeftTriggerBit | RightTriggerBit);
        // 트리거는 히스테리시스: 128 초과로 눌림, 눌린 뒤에는 64 미만이 되어야 해제
        if (IsTriggerDown(state.Gamepad.bLeftTrigger, (prev & LeftTriggerBit) != 0)) buttons |= LeftTriggerBit;
        if (IsTriggerDown(state.Gamepad.bRightTrigger, (prev & RightTriggerBit) != 0)) buttons |= RightTriggerBit;
        _prevButtons[user] = buttons;

        int pressed = buttons & ~prev;
        for (int bit = 1; bit <= 0x8000; bit <<= 1)
        {
            if ((pressed & bit) == 0) continue;
            try { ButtonDown?.Invoke(GamepadFlag | bit); }
            catch { /* 핸들러 예외로 폴링이 멈추지 않게 한다. */ }
        }
    }

    private static bool IsTriggerDown(byte value, bool wasDown) => wasDown ? value >= TriggerRelease : value > TriggerPress;
}
