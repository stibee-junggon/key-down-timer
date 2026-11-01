using System.IO;
using System.Media;

namespace GameTimer;

/// <summary>메모리에서 합성한 짧은 알림음들. 0번은 기본 "띵"(사인파 + 배음, 지수 감쇠).</summary>
public static class Chime
{
    private const int Rate = 48000;
    private const double Attack = 0.004;
    private const double Tail = 0.03;

    private static readonly (string Name, double Duration, Func<double, double> Wave)[] Sounds =
    {
        ("띵", 0.5, Ding),
        ("딩동", 0.9, DingDong),
        ("삐삐", 0.45, Beeps),
        ("상승음", 0.7, Arpeggio),
        ("맑은 종", 1.0, Bell),
    };

    public static IReadOnlyList<string> Names { get; } = Sounds.Select(s => s.Name).ToArray();

    private static readonly SoundPlayer?[] _players = new SoundPlayer?[Sounds.Length];
    private static readonly int[] _playerVolumes = Enumerable.Repeat(-1, Sounds.Length).ToArray();

    public static void Play(int sound, int volume)
    {
        if (volume <= 0) return;
        if (sound < 0 || sound >= Sounds.Length) sound = 0;
        try
        {
            var player = _players[sound];
            if (player == null || _playerVolumes[sound] != volume)
            {
                player?.Dispose();
                player = new SoundPlayer(new MemoryStream(CreateWav(sound, volume)));
                player.Load();
                _players[sound] = player;
                _playerVolumes[sound] = volume;
            }
            // 비동기 재생. 다른 소리가 재생 중이면 끊기고 새로 재생될 뿐이다.
            player.Play();
        }
        catch
        {
            // 사운드 장치 문제 등은 무시
        }
    }

    // 48kHz 스테레오 16bit, 최소 0.5초 → 약 96KB 이상.
    // SoundPlayer는 비동기 재생 중 내부 byte[]를 그대로 쓰므로, 85KB 이상으로 만들어
    // GC가 옮기지 않는 LOH에 할당되게 한다. 짧은 소리는 뒤를 무음으로 채운다.
    private static byte[] CreateWav(int sound, int volume)
    {
        const int channels = 2;
        var (_, duration, wave) = Sounds[sound];
        int soundSamples = (int)(Rate * duration);
        int samples = Math.Max(soundSamples, Rate / 2);
        int dataBytes = samples * channels * 2;
        double amp = 0.45 * Math.Clamp(volume, 0, 100) / 100.0 * short.MaxValue;

        var buffer = new double[soundSamples];
        double peak = 0;
        for (int i = 0; i < soundSamples; i++)
        {
            double t = (double)i / Rate;
            double env = 1;
            if (t < Attack) env *= Math.Pow(Math.Sin(Math.PI / 2 * t / Attack), 2); // 부드러운 어택
            double remain = duration - t;
            if (remain < Tail) env *= remain / Tail;                                 // 끝을 0으로
            buffer[i] = wave(t) * env;
            peak = Math.Max(peak, Math.Abs(buffer[i]));
        }
        // 0번은 기존 소리와 똑같이 유지하고, 나머지는 피크를 0번(약 0.76)과 비슷하게 맞춘다.
        double scale = sound == 0 || peak == 0 ? 1 : 0.75 / peak;

        using var ms = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);                 // PCM
        w.Write((short)channels);
        w.Write(Rate);
        w.Write(Rate * channels * 2);      // byte rate
        w.Write((short)(channels * 2));    // block align
        w.Write((short)16);                // bits per sample
        w.Write("data"u8);
        w.Write(dataBytes);

        for (int i = 0; i < samples; i++)
        {
            short v = i < soundSamples ? (short)Math.Round(buffer[i] * scale * amp) : (short)0;
            for (int c = 0; c < channels; c++) w.Write(v);
        }
        w.Flush();
        return ms.ToArray();
    }

    private static double Ding(double t)
    {
        double x = 2 * Math.PI * 880 * t;
        double tone = Math.Sin(x)
                    + 0.30 * Math.Sin(2 * x) * Math.Exp(-t / 0.06)
                    + 0.10 * Math.Sin(3 * x) * Math.Exp(-t / 0.04);
        return tone / 1.4 * Math.Exp(-t / 0.11);
    }

    private static double DingDong(double t) =>
        Note(t, 0.0, 659.25, 0.18) + Note(t, 0.3, 523.25, 0.22);

    private static double Beeps(double t) =>
        Beep(t, 0.0, 0.08, 1320) + Beep(t, 0.14, 0.08, 1320) + Beep(t, 0.28, 0.08, 1320);

    private static double Arpeggio(double t) =>
        Note(t, 0.0, 523.25, 0.12) + Note(t, 0.12, 659.25, 0.12) + Note(t, 0.24, 783.99, 0.18);

    // 종소리처럼 비정수배 배음(허밍음 포함)을 섞고 길게 감쇠시킨다.
    private static double Bell(double t)
    {
        const double f = 392;
        double s = 0.6 * Math.Sin(2 * Math.PI * f * 0.5 * t) * Math.Exp(-t / 0.40)
                 + 1.0 * Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t / 0.30)
                 + 0.5 * Math.Sin(2 * Math.PI * f * 1.19 * t) * Math.Exp(-t / 0.20)
                 + 0.4 * Math.Sin(2 * Math.PI * f * 1.50 * t) * Math.Exp(-t / 0.15)
                 + 0.3 * Math.Sin(2 * Math.PI * f * 2.00 * t) * Math.Exp(-t / 0.10);
        return s;
    }

    private static double Note(double t, double start, double freq, double decay)
    {
        double u = t - start;
        if (u < 0) return 0;
        double x = 2 * Math.PI * freq * u;
        double env = Math.Exp(-u / decay);
        if (u < Attack) env *= Math.Pow(Math.Sin(Math.PI / 2 * u / Attack), 2);
        return env * (Math.Sin(x) + 0.25 * Math.Sin(2 * x) * Math.Exp(-u / 0.05));
    }

    private static double Beep(double t, double start, double length, double freq)
    {
        const double ramp = 0.005;
        double u = t - start;
        if (u < 0 || u > length) return 0;
        double env = 1;
        if (u < ramp) env = Math.Pow(Math.Sin(Math.PI / 2 * u / ramp), 2);
        else if (length - u < ramp) env = Math.Pow(Math.Sin(Math.PI / 2 * (length - u) / ramp), 2);
        double x = 2 * Math.PI * freq * u;
        return env * (Math.Sin(x) + 0.15 * Math.Sin(3 * x));
    }
}
