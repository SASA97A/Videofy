using Avalonia;
using System;
using System.IO;
using System.Text.Json;
using Video_Size_Optimizer.Models;

namespace Video_Size_Optimizer;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        bool useSoftwareRendering = CheckSoftwareRenderingSetting();

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (useSoftwareRendering)
        {

            // Windows Software Rendering
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software }
            });

            // Linux Software Rendering
            builder.With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Software }
            });
          
        }

        return builder;
    }


    private static bool CheckSoftwareRenderingSetting()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                          "Videofy", "settings.json");

            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings?.UseSoftwareRendering ?? false;
        }
        catch { return false; }
    }


}
