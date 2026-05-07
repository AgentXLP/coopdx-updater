using ShellProgressBar;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

static class Updater {
    const string VERSION_URL = "https://raw.githubusercontent.com/coop-deluxe/sm64coopdx/refs/heads/main/src/pc/network/version.h";
    const string VERSION_IDENTIFIER = "#define SM64COOPDX_VERSION \"";
    const string UPDATE_URL = "https://github.com/coop-deluxe/sm64coopdx/releases/latest/download/";

    static readonly HttpClient HttpClient = new HttpClient();

    public static bool CheckForExecutable() {
        return File.Exists(Utils.GetGameFilename());
    }

    static string GetVersionFilePath() {
        return Path.Combine(Utils.GetAppDataPath(), "version.txt");
    }

    static string GetVersionString() {
        return File.ReadAllText(GetVersionFilePath());
    }

    static string GetRemoteVersion() {
        string remoteVersionInfo;
        try {
            remoteVersionInfo = HttpClient.GetStringAsync(VERSION_URL).Result;
        } catch (HttpRequestException ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to retrieve latest remote version. Are you connected to the internet?");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(ex.Message);
            return "";
        }

        string[] remoteVersionData = remoteVersionInfo.Split('\n');
        for (int i = 0; i < remoteVersionData.Length; i++) {
            if (remoteVersionData[i].StartsWith(VERSION_IDENTIFIER)) {
                return remoteVersionData[i].Substring(VERSION_IDENTIFIER.Length).Replace("\"", "");
            }
        }

        return "";
    }

    public static bool CheckForUpdate() {
        if (!File.Exists(Utils.GetGameFilename())) {
            return true;
        }

        // fetch local version
        int major, minor, patch = 0;
        if (!File.Exists(GetVersionFilePath())) {
            return true;
        }

        string versionText = GetVersionString();
        if (string.IsNullOrEmpty(versionText)) {
            return true;
        }

        string[] versionData = versionText.Substring(1).Split('.');
        major = int.Parse(versionData[0]);
        minor = int.Parse(versionData[1]);
        if (versionData.Length > 2) {
            patch = int.Parse(versionData[2]);
        } else {
            patch = 0;
        }

        // fetch remote version
        int remoteMajor = 0;
        int remoteMinor = 0;
        int remotePatch = 0;
        string remoteVersionInfo;
        try {
            remoteVersionInfo = HttpClient.GetStringAsync(VERSION_URL).Result;
        } catch (HttpRequestException ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to retrieve latest remote version. Are you connected to the internet?");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(ex.Message);
            return false;
        }

        string[] remoteVersionData = remoteVersionInfo.Split('\n');
        for (int i = 0; i < remoteVersionData.Length; i++) {
            if (remoteVersionData[i].StartsWith(VERSION_IDENTIFIER)) {
                string[] identified = remoteVersionData[i].Substring(VERSION_IDENTIFIER.Length + 1).Replace("\"", "").Split('.');
                remoteMajor = int.Parse(identified[0]);
                remoteMinor = int.Parse(identified[1]);
                if (identified.Length > 2) {
                    remotePatch = int.Parse(identified[2]);
                } else {
                    remotePatch = 0;
                }
                break;
            }
        }

        if (remoteMajor != major) {
            return remoteMajor > major;
        }
        if (remoteMinor != minor) {
            return remoteMinor > minor;
        }

        return remotePatch > patch;
    }

    public static async Task DownloadLatestVersion() {
        string platform = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            if (int.Parse(GetRemoteVersion().Substring(1).Split('.')[1]) < 5) {
                platform = "Windows_OpenGL";
            } else {
                platform = "Windows";
            }
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            if (Utils.IsSteamOS()) {
                platform = "SteamOS";
            } else {
                platform = "Linux";
            }
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64) {
                platform = "macOS_ARM";
            } else {
                platform = "macOS_Intel";
            }
        } else {
            throw new PlatformNotSupportedException($"OS '{RuntimeInformation.OSDescription}' not supported");
        }

        using var response = await HttpClient.GetAsync(
            $"{UPDATE_URL}/sm64coopdx_{platform}.zip",
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? 0;

#if WINDOWS
        var options = new ProgressBarOptions {
            ForegroundColor = ConsoleColor.Green,
            EnableTaskBarProgress = true
        };
        using ProgressBar progress = new ProgressBar((int)totalBytes, "Downloading update", options);
#else
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\rDownloading...");
#endif

        using (var contentStream = await response.Content.ReadAsStreamAsync())
        using (var fileStream = File.Create("update.zip")) {
            byte[] buffer = new byte[8192];

            int bytesRead;
            long downloaded = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0) {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloaded += bytesRead;
                string downloadString = $"{Utils.BytesToMegabytes(downloaded)} MB / {Utils.BytesToMegabytes(totalBytes)} MB";
#if WINDOWS
                progress.Tick((int)downloaded, $"- {downloadString}");
#else
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\rDownloading... ({downloadString})");
#endif
            }
        }

#if WINDOWS
        progress.Dispose();
#else
        Console.WriteLine();
#endif

        Console.WriteLine("Installing update...");

        InstallLatestVersion();

        File.Delete("update.zip");

        Console.WriteLine("Launching game...");

        Console.ForegroundColor = ConsoleColor.Gray;

        Utils.StartGame();

        Environment.Exit(0);
    }

    static void InstallLatestVersion() {
#if MACOS
        Utils.ExtractFolderFromZip("update.zip", AppContext.BaseDirectory);
        Utils.ExtractFolderFromZip("update.zip", "lang", "lang");
        Utils.RefreshFolderFromZip("update.zip", "mods", AppContext.BaseDirectory);
        Utils.RefreshFolderFromZip("update.zip", "dynos", AppContext.BaseDirectory);
#else
        Utils.ExtractFilesFromZip("update.zip", AppContext.BaseDirectory);
        Utils.ExtractFolderFromZip("update.zip", "lang", "lang");
        Utils.RefreshFolderFromZip("update.zip", "mods", AppContext.BaseDirectory);
        Utils.RefreshFolderFromZip("update.zip", "dynos", AppContext.BaseDirectory);
#endif

        if (!Directory.Exists(Utils.GetAppDataPath())) {
            Directory.CreateDirectory(Utils.GetAppDataPath());
        }
        if (!File.Exists(GetVersionFilePath())) {
            File.Create(GetVersionFilePath()).Close();
        }
        File.WriteAllText(GetVersionFilePath(), GetRemoteVersion());
    }
}
