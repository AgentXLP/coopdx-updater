using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

static class Program {
#if WINDOWS_BUILD
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;
#endif

    // funny
    static async Task<bool> CheckForUpdaterUpdate() {
        string remoteVersion = await Utils.GetSloppyAsync("https://api.github.com/repos/coop-deluxe/coopdx-updater/releases/latest", "tag_name");
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        return remoteVersion == version;
    }

    static async Task Main() {
#if WINDOWS_BUILD
        IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(h, SW_HIDE);
#endif

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("coopdx-updater (CLI)");
        Console.ForegroundColor = ConsoleColor.Gray;

        bool upToDate = await CheckForUpdaterUpdate();
        if (!upToDate) {
#if WINDOWS_BUILD
            ShowWindow(h, SW_SHOW);
#endif

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("[ ! ] There is an update available for coopdx-updater!\nDownload at https://github.com/coop-deluxe/coopdx-updater/releases/latest");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        if (!Updater.CheckForUpdate()) {
            Utils.StartGame();
            return;
        }

#if WINDOWS_BUILD
        ShowWindow(h, SW_SHOW);
#endif

        await Updater.DownloadLatestVersion();

        if (File.Exists("update.zip")) {
            File.Delete("update.zip");
        }
    }
}
