using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

static class Program {
#if WINDOWS
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;
    const int SW_SHOW = 5;
#endif

    static async Task Main() {
#if WINDOWS
        IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
        ShowWindow(h, SW_HIDE);
#endif
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("coopdx-updater (CLI)");
        Console.ForegroundColor = ConsoleColor.Gray;

        if (!Updater.CheckForUpdate()) {
            Utils.StartGame();
            return;
        }

#if WINDOWS
        ShowWindow(h, SW_SHOW);
#endif

        await Updater.DownloadLatestVersion();

        if (File.Exists("update.zip")) {
            File.Delete("update.zip");
        }
    }
}
