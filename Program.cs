using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

static class Program {
    public static bool tempUpdater = false;
    public static bool gameUpdate = false;
    public static string gamePath = AppContext.BaseDirectory;

    // funny
    static async Task<bool> CheckForUpdaterUpdate() {
        if (Utils.IsRunningFromAppBundle() || tempUpdater || gameUpdate) { return true; }
        string remoteVersion = await Utils.GetSloppyAsync("https://api.github.com/repos/coop-deluxe/coopdx-updater/releases/latest", "tag_name");
        string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        return remoteVersion == version;
    }

    static async Task Main(string[] args) {
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == "--temporary") {
                tempUpdater = true;
                continue;
            }

            if (args[i] == "--game-update") {
                gameUpdate = true;
                continue;
            }

            if (args[i] == "--game-path") {
                // get string from next argument if we can
                if (i < args.Length - 1) {
                    try {
                        // bit weird, but c# doesn't provide a simple function to validate the path, so this will have to do
                        string fullPath = Path.GetFullPath(args[i + 1]);
                        gamePath = fullPath;
                    } catch {
                        Console.WriteLine($"Invalid path: {args[i + 1]}");
                        Environment.Exit(0);
                    }
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Please enter a path after using --game-path");
                    Environment.Exit(0);
                }
                continue;
            }
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("coopdx-updater (CLI)");
        Console.ForegroundColor = ConsoleColor.Gray;

        bool upToDate = await CheckForUpdaterUpdate();
        if (!upToDate) {
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

        await Updater.DownloadLatestVersion();

        if (File.Exists("update.zip")) {
            File.Delete("update.zip");
        }
    }
}
