using System.IO;
using System.Media;

namespace GameTimer;

/// <summary>메모리에서 생성한 짧은 "띵" 소리(사인파 + 배음, 지수 감쇠).</summary>
public static class Chime
{
    private static SoundPlayer? _player;
    private static int _playerVolume = -1;

    public static void Play(int volume)
    {
        if (volume <= 0) return;
        try
        {
            if (_player == null || _playerVolume != volume)
            {
                _player?.Dispose();
                _player = new SoundPlayer(new MemoryStream(CreateWav(volume)));
                _player.Load();
                _playerVolume = volume;
            }
            // 비동기 재생. 여러 타이머가 동시에 끝나도 마지막 호출이 처음부터 다시 재생될 뿐이다.
            _player.Play();
        }
        catch
        {
            // 사운드 장치 문제 등은 무시
        }
    }

    // 48kHz 스테레오 16bit, 0.5초 → 약 96KB.
    // SoundPlayer는 비동기 재생 중 내부 byte[]를 그대로 쓰므로, 85KB 이상으로 만들어
    // GC가 옮기지 않는 LOH에 할당되게 한다.
    private static byte[] CreateWav(int volume)
    {
        const int rate = 48000;
        const int channels = 2;
        const double duration = 0.5;
        const double attack = 0.004;
        const double tail = 0.03;
        int samples = (int)(rate * duration);
        int dataBytes = samples * channels * 2;
        double amp = 0.45 * Math.Clamp(volume, 0, 100) / 100.0 * short.MaxValue;

        using var ms = new MemoryStream(44 + dataBytes);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);                 // PCM
        w.Write((short)channels);
        w.Write(rate);
        w.Write(rate * channels * 2);      // byte rate
        w.Write((short)(channels * 2));    // block align
        w.Write((short)16);                // bits per sample
        w.Write("data"u8);
        w.Write(dataBytes);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / rate;
            double x = 2 * Math.PI * 880 * t;
            double tone = Math.Sin(x)
                        + 0.30 * Math.Sin(2 * x) * Math.Exp(-t / 0.06)
                        + 0.10 * Math.Sin(3 * x) * Math.Exp(-t / 0.04);
            tone /= 1.4;

            double env = Math.Exp(-t / 0.11);
            if (t < attack) env *= Math.Pow(Math.Sin(Math.PI / 2 * t / attack), 2); // 부드러운 어택
            double remain = duration - t;
            if (remain < tail) env *= remain / tail;                                 // 끝을 0으로

            short v = (short)Math.Round(tone * env * amp);
            for (int c = 0; c < channels; c++) w.Write(v);
        }
        w.Flush();
        return ms.ToArray();
    }
}
