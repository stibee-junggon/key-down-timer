namespace GameTimer;

public static class KeyNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt",
        [0x13] = "Pause", [0x14] = "CapsLock", [0x15] = "한/영", [0x19] = "한자", [0x1B] = "Esc", [0x20] = "Space",
        [0x21] = "PgUp", [0x22] = "PgDn", [0x23] = "End", [0x24] = "Home",
        [0x25] = "←", [0x26] = "↑", [0x27] = "→", [0x28] = "↓",
        [0x2C] = "PrtSc", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x5B] = "Win", [0x5C] = "Win", [0x5D] = "Menu",
        [0x6A] = "Num*", [0x6B] = "Num+", [0x6D] = "Num-", [0x6E] = "Num.", [0x6F] = "Num/",
        [0x90] = "NumLock", [0x91] = "ScrollLock",
        [0xA0] = "LShift", [0xA1] = "RShift", [0xA2] = "LCtrl", [0xA3] = "RCtrl", [0xA4] = "LAlt", [0xA5] = "RAlt",
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/", [0xC0] = "`",
        [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'",
    };

    // XInput 버튼 비트 → 이름 (LT/RT는 GamepadPoller의 가상 비트)
    private static readonly Dictionary<int, string> PadNames = new()
    {
        [0x0001] = "↑", [0x0002] = "↓", [0x0004] = "←", [0x0008] = "→",
        [0x0010] = "Start", [0x0020] = "Back", [0x0040] = "LS", [0x0080] = "RS",
        [0x0100] = "LB", [0x0200] = "RB", [0x0400] = "LT", [0x0800] = "RT",
        [0x1000] = "A", [0x2000] = "B", [0x4000] = "X", [0x8000] = "Y",
    };

    // 조합키 표시 순서
    private static readonly int[] PadOrder =
    {
        0x0100, 0x0200, 0x0400, 0x0800, 0x0020, 0x0010, 0x0040, 0x0080,
        0x0001, 0x0002, 0x0004, 0x0008, 0x1000, 0x2000, 0x4000, 0x8000,
    };

    public static string Get(int vk)
    {
        if (GamepadPoller.IsGamepadCode(vk)) return "패드 " + GetPad(vk & 0xFFFF);
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();           // 0~9
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();           // A~Z
        if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);             // 숫자패드
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);           // F1~F24
        return Names.TryGetValue(vk, out var name) ? name : $"0x{vk:X2}";
    }

    // 버튼 마스크의 각 비트 이름을 "+"로 연결 (예: "LT+X")
    private static string GetPad(int mask)
    {
        var parts = PadOrder.Where(bit => (mask & bit) != 0).Select(bit => PadNames[bit]).ToList();
        return parts.Count > 0 ? string.Join("+", parts) : $"0x{mask:X4}";
    }
}

public static class TimeFormat
{
    public static string ToText(int totalSeconds) => $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

    /// <summary>"mm:ss" 또는 초 단위 숫자를 받는다. 1초 이상만 유효.</summary>
    public static bool TryParse(string? text, out int seconds)
    {
        seconds = 0;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        var parts = text.Split(':');
        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0], out seconds)) return false;
        }
        else if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0], out int m) || !int.TryParse(parts[1], out int s)) return false;
            if (m < 0 || s < 0 || s >= 60 || m > 999) return false;
            seconds = m * 60 + s;
        }
        else
        {
            return false;
        }
        return seconds >= 1 && seconds <= 999 * 60 + 59;
    }
}
