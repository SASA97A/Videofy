using System.Diagnostics;
using Xunit;
using Video_Size_Optimizer.Models;
using Video_Size_Optimizer.Services;
using Video_Size_Optimizer.ViewModels;

namespace Videofy.Tests;

public class RuntimeIntegrationTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return;
            await Task.Delay(25);
        }
        throw new TimeoutException("Condition was not met within the timeout.");
    }

    private static string GetSettingsPath()
    {
        var settingsService = new SettingsService();
        return Path.Combine(settingsService.SettingsFolder, "settings.json");
    }

    private static async Task WithSavedSettingsAsync(AppSettings modified, Func<Task> action)
    {
        string settingsPath = GetSettingsPath();
        string? backup = File.Exists(settingsPath) ? await File.ReadAllTextAsync(settingsPath) : null;

        try
        {
            var settingsService = new SettingsService();
            await settingsService.SaveSettingsAsync(modified);
            await action();
        }
        finally
        {
            if (backup != null)
                await File.WriteAllTextAsync(settingsPath, backup);
            else
                File.Delete(settingsPath);
        }
    }

    [Fact]
    public async Task Startup_ProfileLoad_PreservesSavedOutputFormat()
    {
        var saved = new SettingsService().LoadSettings();
        saved.DefaultOutputFormat = ".mkv";

        await WithSavedSettingsAsync(saved, async () =>
        {
            var vm = new MainWindowViewModel();
            await WaitUntilAsync(() => vm.AvailableProfiles.Count > 0 && vm.SelectedProfile != null);

            Assert.Equal("balanced", vm.SelectedProfile!.Name);
            Assert.Equal(".mkv", vm.GlobalSettings.DefaultOutputFormat);
        });
    }

    [Fact]
    public void SettingsViewModel_SelectedTheme_IsDisplayNameFromComboBox()
    {
        var settings = new AppSettings { SelectedTheme = "monokai" };
        var vm = new SettingsViewModel(settings);

        Assert.Equal("Monokai", vm.SelectedTheme);
    }

    [Fact]
    public void SettingsViewModel_GetUpdatedSettings_RoundTripsInternalThemeName()
    {
        var settings = new AppSettings { SelectedTheme = "light" };
        var vm = new SettingsViewModel(settings);

        Assert.Equal("light", vm.GetUpdatedSettings().SelectedTheme);
    }

    [Fact]
    public void ChangingSelectedAudioCodec_UpdatesGlobalSettings()
    {
        var vm = new MainWindowViewModel();
        var opus = vm.AvailableAudioCodecs.FirstOrDefault(o => o.CodecName == "libopus");
        Assert.NotNull(opus);

        vm.SelectedAudioCodec = opus;

        Assert.Equal("libopus", vm.GlobalSettings.DefaultAudioCodec);
        Assert.Equal(96, vm.GlobalSettings.DefaultAudioBitrate);
    }

    [Fact]
    public async Task Startup_AudioCodecLoad_RespectsSavedDefault()
    {
        var saved = new SettingsService().LoadSettings();
        saved.DefaultAudioCodec = "libopus";
        saved.DefaultAudioBitrate = 96;

        await WithSavedSettingsAsync(saved, async () =>
        {
            var vm = new MainWindowViewModel();
            await WaitUntilAsync(() => vm.SelectedAudioCodec != null && vm.AvailableAudioCodecs.Count > 0);

            Assert.Equal("libopus", vm.SelectedAudioCodec!.CodecName);
        });
    }
}
