using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Video_Size_Optimizer.Models
{
    public partial class VideoFile
    {

        [ObservableProperty] private double progress = 0;
        [ObservableProperty] private bool isProcessing;
        [ObservableProperty] private bool isCompleted;
        [ObservableProperty] private string _eta = "";

        public bool ShowIndeterminate => IsProcessing && Progress <= 0;
        public bool IsInvalid => FileName.Contains("-CRF", StringComparison.OrdinalIgnoreCase) ||
                                 FileName.Contains("-Target", StringComparison.OrdinalIgnoreCase);
        public bool IsReady => !IsProcessing && !IsCompleted && !IsInvalid;
        partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(ShowIndeterminate));
        partial void OnIsProcessingChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowIndeterminate));
            OnPropertyChanged(nameof(IsReady));
        }
        partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(IsReady));


        public void UpdateProgress(double percentage, string speed, string fps)
        {
            Progress = percentage;

            if (percentage >= 100) { Eta = "Done"; return; }

            if (double.TryParse(speed.Replace("x", ""), out double speedVal) && speedVal > 0)
            {
                double remainingVideoSeconds = DurationSeconds * (1 - (percentage / 100));
                double realTimeSecondsLeft = remainingVideoSeconds / speedVal;
                var t = TimeSpan.FromSeconds(realTimeSecondsLeft);

                Eta = t.TotalHours >= 1
                    ? $@"{(int)t.TotalHours}h {t.Minutes}m remaining"
                    : $@"{t.Minutes}m {t.Seconds}s remaining";
            }
            else
            {
                Eta = "Calculating...";
            }
        }
    }
}
