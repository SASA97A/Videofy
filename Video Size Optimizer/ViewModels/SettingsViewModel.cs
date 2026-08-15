using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Video_Size_Optimizer.Services;
using Video_Size_Optimizer.Utils;


namespace Video_Size_Optimizer.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly FfmpegService _ffmpegService = new();

        [ObservableProperty] private bool _deleteOriginal;
        [ObservableProperty] private string _selectedFormat;
        [ObservableProperty] private bool _saveToDisk = false;
        [ObservableProperty] private bool _preventSleep;
        [ObservableProperty] private int _lowDiskBufferGb;
        [ObservableProperty] private bool _processAlreadyOptimized;
        [ObservableProperty] private ObservableCollection<EncoderOption> _encoderOptions = new();
        [ObservableProperty] private string _customExtensions;
        [ObservableProperty] private bool _preventUpsampling;
        [ObservableProperty] private bool _useSoftwareRendering;
        [ObservableProperty] private bool _autoCheckUpdates;
        [ObservableProperty] private bool _modalCompletionMessages;
        [ObservableProperty] private bool _modalErrorMessages;
        [ObservableProperty] private bool _modalInfoMessages;
        [ObservableProperty] private bool _playSoundOnCompletion;
        [ObservableProperty] private bool _sendDesktopNotification;

        public List<string> OutputFormats => AppConstants.AvailableFormats;

        public SettingsViewModel(Models.AppSettings currentSettings)
        {
            DeleteOriginal = currentSettings.DeleteOriginalAfterCompression;
            SelectedFormat = currentSettings.DefaultOutputFormat;
            PreventSleep = currentSettings.PreventSleep;
            LowDiskBufferGb = currentSettings.LowDiskBufferGb;
            ProcessAlreadyOptimized = currentSettings.ProcessAlreadyOptimized;
            CustomExtensions = currentSettings.CustomExtensions;
            PreventUpsampling = currentSettings.PreventUpsampling;
            UseSoftwareRendering = currentSettings.UseSoftwareRendering;
            AutoCheckUpdates = currentSettings.AutoCheckUpdatesOnStartup;
            ModalCompletionMessages = currentSettings.ModalCompletionMessages;
            ModalErrorMessages = currentSettings.ModalErrorMessages;
            ModalInfoMessages = currentSettings.ModalInfoMessages;
            PlaySoundOnCompletion = currentSettings.PlaySoundOnCompletion;
            SendDesktopNotification = currentSettings.SendDesktopNotification;

            foreach (var name in AppConstants.HardwareEncoderNames)
            {
                bool isSupp = currentSettings.SupportedHardwareEncoders.Contains(name);
                EncoderOptions.Add(new EncoderOption
                {
                    Name = name,
                    IsIncluded = currentSettings.EnabledEncoders.Contains(name) && isSupp,
                    IsSupported = isSupp
                });
            }
        }

        [RelayCommand]
        public async Task AutoDetectHardware()
        {
            var detected = await _ffmpegService.DetectSupportedHardwareEncodersAsync();
            foreach (var option in EncoderOptions)
            {
                bool isSupp = detected.Contains(option.Name);
                option.IsSupported = isSupp;
                if (isSupp)
                {
                    option.IsIncluded = true;
                }
                else
                {
                    option.IsIncluded = false;
                }
            }
        }

        public Models.AppSettings GetUpdatedSettings()
        {
            var enabled = new List<string> { "Standard (Slow, Best Quality)" };
            enabled.AddRange(EncoderOptions.Where(x => x.IsIncluded && x.IsSupported).Select(x => x.Name));

            var supported = EncoderOptions.Where(x => x.IsSupported).Select(x => x.Name).ToList();

            return new Models.AppSettings
            {
                DeleteOriginalAfterCompression = DeleteOriginal,
                DefaultOutputFormat = SelectedFormat,
                PreventSleep = PreventSleep,
                LowDiskBufferGb = LowDiskBufferGb,
                ProcessAlreadyOptimized = ProcessAlreadyOptimized,
                EnabledEncoders = enabled,
                SupportedHardwareEncoders = supported,
                HasDetectedHardware = true,
                CustomExtensions = CustomExtensions,
                PreventUpsampling = PreventUpsampling,
                UseSoftwareRendering = UseSoftwareRendering,
                AutoCheckUpdatesOnStartup = AutoCheckUpdates,
                ModalCompletionMessages = ModalCompletionMessages,
                ModalErrorMessages = ModalErrorMessages,
                ModalInfoMessages = ModalInfoMessages,
                PlaySoundOnCompletion = PlaySoundOnCompletion,
                SendDesktopNotification = SendDesktopNotification
            };
        }

        public partial class EncoderOption : ObservableObject
        {
            public string Name { get; set; } = "";
            [ObservableProperty] private bool _isIncluded;
            [ObservableProperty] private bool _isSupported;
        }
    }
}
