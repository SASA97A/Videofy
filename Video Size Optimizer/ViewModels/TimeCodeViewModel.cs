

using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Video_Size_Optimizer.ViewModels
{

    // Will be userd later for Video trim feature
    public partial class TimeCodeViewModel : ObservableObject
    {
        private readonly Action _onChanged;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Hours))]
        [NotifyPropertyChangedFor(nameof(Minutes))]
        [NotifyPropertyChangedFor(nameof(Seconds))]
        [NotifyPropertyChangedFor(nameof(Milliseconds))]
        private double _totalSeconds;

        public TimeCodeViewModel(Action onChanged)
        {
            _onChanged = onChanged;
        }

        // --- Segment Properties for UI Knobs ---
        public int Hours
        {
            get => (int)TimeSpan.FromSeconds(TotalSeconds).TotalHours;
            set => UpdateTime(h: value);
        }
        public int Minutes
        {
            get => TimeSpan.FromSeconds(TotalSeconds).Minutes;
            set => UpdateTime(m: value);
        }
        public int Seconds
        {
            get => TimeSpan.FromSeconds(TotalSeconds).Seconds;
            set => UpdateTime(s: value);
        }
        public int Milliseconds
        {
            get => TimeSpan.FromSeconds(TotalSeconds).Milliseconds;
            set => UpdateTime(ms: value);
        }

        private void UpdateTime(int? h = null, int? m = null, int? s = null, int? ms = null)
        {
            var t = TimeSpan.FromSeconds(TotalSeconds);
            TotalSeconds = new TimeSpan(0,
                h ?? (int)t.TotalHours,
                m ?? t.Minutes,
                s ?? t.Seconds,
                ms ?? t.Milliseconds).TotalSeconds;

            _onChanged?.Invoke();
        }

        partial void OnTotalSecondsChanged(double value) => _onChanged?.Invoke();
    }
}
