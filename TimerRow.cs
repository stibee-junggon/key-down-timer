using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameTimer;

public enum RowState { Idle, Running, Warning }

/// <summary>오버레이의 타이머 한 줄. 남은 시간은 공용 Stopwatch 기준으로 계산한다.</summary>
public class TimerRow : INotifyPropertyChanged
{
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(1);
    private const int WarningSeconds = 10;

    private TimeSpan? _startedAt;
    private TimeSpan? _finishedAt;

    public TimerRow(TimerEntry entry)
    {
        Key = entry.Key;
        KeyName = KeyNames.Get(entry.Key);
        Name = entry.Name;
        Duration = TimeSpan.FromSeconds(entry.Seconds);
        Sound = entry.Sound;
    }

    public int Key { get; }
    public string KeyName { get; }
    public string Name { get; }
    public TimeSpan Duration { get; }
    public int Sound { get; }

    private string _remainingText = "--:--";
    public string RemainingText { get => _remainingText; private set => Set(ref _remainingText, value); }

    private double _progress;
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    private RowState _state = RowState.Idle;
    public RowState State { get => _state; private set => Set(ref _state, value); }

    private bool _flashOn;
    public bool FlashOn { get => _flashOn; private set => Set(ref _flashOn, value); }

    public void Start(TimeSpan now)
    {
        _startedAt = now;
        _finishedAt = null;
        FlashOn = false;
        Update(now);
    }

    /// <summary>상태를 갱신하고, 이번 호출에서 막 끝났으면 true.</summary>
    public bool Update(TimeSpan now)
    {
        bool justFinished = false;

        if (_startedAt is TimeSpan started)
        {
            var remaining = Duration - (now - started);
            if (remaining > TimeSpan.Zero)
            {
                int secs = (int)Math.Ceiling(remaining.TotalSeconds);
                RemainingText = TimeFormat.ToText(secs);
                Progress = remaining.TotalMilliseconds / Duration.TotalMilliseconds;
                State = secs <= WarningSeconds ? RowState.Warning : RowState.Running;
                return false;
            }
            _startedAt = null;
            _finishedAt = now;
            justFinished = true;
        }

        RemainingText = "--:--";
        Progress = 0;
        State = RowState.Idle;

        if (_finishedAt is TimeSpan finished)
        {
            var since = now - finished;
            if (since < FlashDuration)
            {
                FlashOn = (int)(since.TotalMilliseconds / 200) % 2 == 0;
            }
            else
            {
                FlashOn = false;
                _finishedAt = null;
            }
        }
        return justFinished;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
