using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
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

        public List<string> OutputFormats => AppConstants.AvailableFormats;

        public SettingsViewModel(Models.AppSettings currentSettings)
        {
            DeleteOriginal = currentSettings.DeleteOriginalAfterCompression;
            SelectedFormat = currentSettings.DefaultOutputFormat;
            PreventSleep = currentSettings.PreventSleep;
            LowDiskBufferGb = currentSettings.LowDiskBufferGb;
        }

        public Models.AppSettings GetUpdatedSettings()
        {
            return new Models.AppSettings
            {
                DeleteOriginalAfterCompression = DeleteOriginal,
                DefaultOutputFormat = SelectedFormat,
                PreventSleep = PreventSleep,
                LowDiskBufferGb = LowDiskBufferGb
            };
        }









    }
}
