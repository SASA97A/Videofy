using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;


namespace Video_Size_Optimizer.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] private bool _deleteOriginal;
        [ObservableProperty] private string _selectedFormat;
        [ObservableProperty] private bool _saveToDisk = false;

        public List<string> OutputFormats => AppConstants.AvailableFormats;

        public SettingsViewModel(Models.AppSettings currentSettings)
        {
            DeleteOriginal = currentSettings.DeleteOriginalAfterCompression;
            SelectedFormat = currentSettings.DefaultOutputFormat;
        }

        public Models.AppSettings GetUpdatedSettings()
        {
            return new Models.AppSettings
            {
                DeleteOriginalAfterCompression = DeleteOriginal,
                DefaultOutputFormat = SelectedFormat
            };
        }









    }
}
