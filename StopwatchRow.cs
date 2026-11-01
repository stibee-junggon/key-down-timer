using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameTimer;

/// <summary>오버레이 최상단의 스톱워치. 경과 시간은 공용 Stopwatch 기준으로 누적한다.</summary>
public class StopwatchRow : INotifyPropertyChanged
{
    private TimeSpan _accumulated;
    private TimeSpan? _startedAt;

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _elapsedText = "00:00";
    public string ElapsedText { get => _elapsedText; private set => Set(ref _elapsedText, value); }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }

    public void Toggle(TimeSpan now)
    {
        if (_startedAt is TimeSpan started)
        {
            _accumulated += now - started;
            _startedAt = null;
        }
        else
        {
            _startedAt = now;
        }
        IsRunning = _startedAt != null;
        Update(now);
    }

    public void Reset(TimeSpan now)
    {
        _accumulated = TimeSpan.Zero;
        _startedAt = null;
        IsRunning = false;
        Update(now);
    }

    public void Update(TimeSpan now)
    {
        var elapsed = _accumulated + (_startedAt is TimeSpan started ? now - started : TimeSpan.Zero);
        int secs = (int)elapsed.TotalSeconds;
        ElapsedText = secs < 3600
            ? $"{secs / 60:00}:{secs % 60:00}"
            : $"{secs / 3600}:{secs / 60 % 60:00}:{secs % 60:00}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
