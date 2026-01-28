using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using Video_Size_Optimizer.ViewModels;

namespace Video_Size_Optimizer.Models
{
    public partial class VideoFile
    {

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EndTime))]
        [NotifyPropertyChangedFor(nameof(TrimDisplay))]
        private double _durationSeconds;

        [ObservableProperty]
        private bool _isDurationLoaded = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
        [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
        [NotifyPropertyChangedFor(nameof(TrimDisplay))]
        [NotifyPropertyChangedFor(nameof(IsTrimmed))]
        [NotifyPropertyChangedFor(nameof(StartHours))]
        [NotifyPropertyChangedFor(nameof(StartMinutes))]
        [NotifyPropertyChangedFor(nameof(StartSeconds))]
        [NotifyPropertyChangedFor(nameof(StartMilliseconds))]
        private double _startTime;

        partial void OnStartTimeChanged(double value)
        {
            if (value < 0) StartTime = 0;
            if (value >= EndTime && IsDurationLoaded)
            {
                // Keep EndTime at least 0.1s ahead of StartTime
                EndTime = value + 0.1;
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCustomSettings))]
        [NotifyPropertyChangedFor(nameof(CustomSettingsBadge))]
        [NotifyPropertyChangedFor(nameof(TrimDisplay))]
        [NotifyPropertyChangedFor(nameof(IsTrimmed))]
        [NotifyPropertyChangedFor(nameof(EndHours))]
        [NotifyPropertyChangedFor(nameof(EndMinutes))]
        [NotifyPropertyChangedFor(nameof(EndSeconds))]
        [NotifyPropertyChangedFor(nameof(EndMilliseconds))]
        private double _endTime;

        partial void OnEndTimeChanged(double value)
        {
            if (value > DurationSeconds && IsDurationLoaded) EndTime = DurationSeconds;
            if (value <= StartTime && IsDurationLoaded)
            {
                // Don't let EndTime go behind StartTime
                StartTime = Math.Max(0, value - 0.1);
            }
        }

        public double StartHours
        {
            get => (int)TimeSpan.FromSeconds(StartTime).TotalHours;
            set => UpdateStartTime(h: (int)value);
        }

        public double StartMinutes
        {
            get => TimeSpan.FromSeconds(StartTime).Minutes;
            set => UpdateStartTime(m: (int)value);
        }

        public double StartSeconds
        {
            get => TimeSpan.FromSeconds(StartTime).Seconds;
            set => UpdateStartTime(s: (int)value);
        }

        public double StartMilliseconds
        {
            get => TimeSpan.FromSeconds(StartTime).Milliseconds;
            set => UpdateStartTime(ms: (int)value);
        }

        public double EndHours
        {
            get => TimeSpan.FromSeconds(EndTime).TotalHours >= 1 ? (int)TimeSpan.FromSeconds(EndTime).TotalHours : 0;
            set => UpdateEndTime(h: (int)value);
        }

        public double EndMinutes
        {
            get => TimeSpan.FromSeconds(EndTime).Minutes;
            set => UpdateEndTime(m: (int)value);
        }

        public double EndSeconds
        {
            get => TimeSpan.FromSeconds(EndTime).Seconds;
            set => UpdateEndTime(s: (int)value);
        }

        public double EndMilliseconds
        {
            get => TimeSpan.FromSeconds(EndTime).Milliseconds;
            set => UpdateEndTime(ms: (int)value);
        }


        private void UpdateStartTime(int? h = null, int? m = null, int? s = null, int? ms = null)
        {
            var t = TimeSpan.FromSeconds(StartTime);
            var newTime = new TimeSpan(0,
                h ?? (int)t.TotalHours,
                m ?? t.Minutes,
                s ?? t.Seconds,
                ms ?? t.Milliseconds);

            StartTime = newTime.TotalSeconds;

            // Notify all proxies so they stay in sync if one overflows
            OnPropertyChanged(nameof(StartHours));
            OnPropertyChanged(nameof(StartMinutes));
            OnPropertyChanged(nameof(StartSeconds));
            OnPropertyChanged(nameof(StartMilliseconds));
        }

        private void UpdateEndTime(int? h = null, int? m = null, int? s = null, int? ms = null)
        {
            var t = TimeSpan.FromSeconds(EndTime);

            // Construct the new time. 
            // We use the 'days' parameter as 0 to avoid offset issues.
            var newTime = new TimeSpan(0,
                h ?? (int)t.TotalHours, // Fixed
                m ?? t.Minutes,
                s ?? t.Seconds,
                ms ?? t.Milliseconds);

            EndTime = newTime.TotalSeconds;

            // Notify all proxies to refresh the NumericUpDown values
            OnPropertyChanged(nameof(EndHours));
            OnPropertyChanged(nameof(EndMinutes));
            OnPropertyChanged(nameof(EndSeconds));
            OnPropertyChanged(nameof(EndMilliseconds));
        }

        public string TrimDisplay => IsTrimmed ? $"{FormatTime(StartTime)} - {FormatTime(EndTime)}" : "";
        public bool IsTrimmed => StartTime > 0.001 || EndTime < (DurationSeconds - 0.001);
        public void ResetCustomSettings()
        {
            CustomTargetSizeMb = null;
            CustomResolution = null;
            CustomFps = null;
            StartTime = 0;
            EndTime = (IsDurationLoaded) ? DurationSeconds : 1;
           // SplitSizeMb = null;
        }
        public string CustomSettingsBadge
        {
            get
            {
                if (!HasCustomSettings) return string.Empty;

                var parts = new List<string>();
                if (HasCustomSize) parts.Add($"{CustomTargetSizeMb}MB");
                if (!string.IsNullOrEmpty(CustomResolution)) parts.Add(CustomResolution);
                if (!string.IsNullOrEmpty(CustomFps)) parts.Add(CustomFps + " Fps");
            //    if (IsSplitEnabled) parts.Add($"Split: {SplitSizeMb}MB");
                if (IsTrimmed) parts.Add($"{FormatTime(StartTime)} - {FormatTime(EndTime)}");
                
                return $" {string.Join(" | ", parts)} ";
            }
        }


        private string FormatTime(double? seconds)
        {
            if (!seconds.HasValue)
                return "--:--:--.--";

            var t = TimeSpan.FromSeconds(seconds.Value);
            return t.TotalHours >= 1
                ? t.ToString(@"h\:mm\:ss")
                : t.ToString(@"mm\:ss");
        }



    }
}
