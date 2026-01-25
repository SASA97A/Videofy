using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Video_Size_Optimizer.Services;
using Video_Size_Optimizer.Utils;


namespace Video_Size_Optimizer.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] private bool _deleteOriginal;
        [ObservableProperty] private string _selectedFormat;
        [ObservableProperty] private bool _saveToDisk = false;
        [ObservableProperty] private bool _preventSleep;
        [ObservableProperty] private int _lowDiskBufferGb;
        [ObservableProperty] private bool _processAlreadyOptimized;
        [ObservableProperty] private ObservableCollection<EncoderOption> _encoderOptions = new();
        [ObservableProperty] private string _customExtensions;

        public List<string> OutputFormats => AppConstants.AvailableFormats;

        public SettingsViewModel(Models.AppSettings currentSettings)
        {
            DeleteOriginal = currentSettings.DeleteOriginalAfterCompression;
            SelectedFormat = currentSettings.DefaultOutputFormat;
            PreventSleep = currentSettings.PreventSleep;
            LowDiskBufferGb = currentSettings.LowDiskBufferGb;
            ProcessAlreadyOptimized = currentSettings.ProcessAlreadyOptimized;
            CustomExtensions = currentSettings.CustomExtensions;

            foreach (var name in AppConstants.HardwareEncoderNames)
            {
                EncoderOptions.Add(new EncoderOption
                {
                    Name = name,
                    IsIncluded = currentSettings.EnabledEncoders.Contains(name)
                });
            }
        }

        public Models.AppSettings GetUpdatedSettings()
        {
            var enabled = new List<string> { "Standard (Slow, Best Quality)" };
            enabled.AddRange(EncoderOptions.Where(x => x.IsIncluded).Select(x => x.Name));

            return new Models.AppSettings
            {
                DeleteOriginalAfterCompression = DeleteOriginal,
                DefaultOutputFormat = SelectedFormat,
                PreventSleep = PreventSleep,
                LowDiskBufferGb = LowDiskBufferGb,
                ProcessAlreadyOptimized = ProcessAlreadyOptimized,
                EnabledEncoders = enabled,
                CustomExtensions = CustomExtensions
            };
        }

        public partial class EncoderOption : ObservableObject
        {
            public string Name { get; set; } = "";
            [ObservableProperty] private bool _isIncluded;
        }
    }
}
