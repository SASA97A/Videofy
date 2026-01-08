using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Video_Size_Optimizer
{
    public class MessageService
    {
        public async Task ShowInfoAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Info);
            await box.ShowAsync();
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Error);
            await box.ShowAsync();
        }

        public async Task ShowSuccessAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.Ok, Icon.Success);
            await box.ShowAsync();
        }

        public async Task<ButtonResult> ShowCustomAsync(MessageBoxStandardParams parameters)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(parameters);
            return await box.ShowAsync();
        }

        public async Task<bool> ShowYesNoAsync(string title, string message)
        {
            var box = MessageBoxManager.GetMessageBoxStandard(title, message + "  ", ButtonEnum.YesNo, Icon.Question);
            var result = await box.ShowAsync();
            return result == ButtonResult.Yes;
        }
    }
}
