using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer
{
    public class MessageService
    {
        private readonly SettingsService _settingsService = new();

        private Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Info);
            var mainWindow = GetMainWindow();
            if (settings.ModalInfoMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Error);
            var mainWindow = GetMainWindow();
            if (settings.ModalErrorMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            var settings = _settingsService.LoadSettings();
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Success);
            var mainWindow = GetMainWindow();
            if (settings.ModalCompletionMessages && mainWindow != null)
            {
                await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                await box.ShowAsync();
            }
        }

        public async Task<ButtonResult> ShowCustomAsync(MessageBoxStandardParams parameters)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                return await box.ShowWindowDialogAsync(mainWindow);
            }
            return await box.ShowAsync();
        }

        public async Task<bool> ShowYesNoAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.YesNo, Icon.Question);
            var mainWindow = GetMainWindow();
            ButtonResult result;
            if (mainWindow != null)
            {
                result = await box.ShowWindowDialogAsync(mainWindow);
            }
            else
            {
                result = await box.ShowAsync();
            }
            return result == ButtonResult.Yes;
        }
    }
}
