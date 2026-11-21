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
    public static void Main(string[] args)
    {
        // ffmpeg and ffprobe end together with Videofy, also after a crash or kill.
        Video_Size_Optimizer.Services.ChildProcessGuard.Initialize();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        bool useSoftwareRendering = CheckSoftwareRenderingSetting();

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Linux: Avalonia draws its own border and resize grips around our custom title bar.
#pragma warning disable AVALONIA_X11_CSD // Experimental in Avalonia 12
        // WmClass must match StartupWMClass in the .desktop file, or the panel shows no icon.
        var x11Options = new X11PlatformOptions
        {
            EnableDrawnDecorations = true,
            WmClass = Video_Size_Optimizer.Services.LinuxDesktopIntegration.WmClass
        };
#pragma warning restore AVALONIA_X11_CSD

        if (useSoftwareRendering)
        {

            // Windows Software Rendering
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.Software }
            });

            // Linux Software Rendering
            x11Options.RenderingMode = new[] { X11RenderingMode.Software };

        }

        builder.With(x11Options);

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
