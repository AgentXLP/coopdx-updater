using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

static class Utils {
    // technically mebibytes
    public static string BytesToMegabytes(long byteCount) {
        return (byteCount / 1049000.0).ToString("F1");
    }

    public static void ExtractFilesFromZip(string zipPath, string extractPath) {
        using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
            foreach (var entry in archive.Entries) {
                // skip directories themselves
                if (string.IsNullOrEmpty(entry.Name)) {
                    continue;
                }

                // don't process subdirectories
                if (entry.FullName.Contains('/') || entry.FullName.Contains('\\')) {
                    continue;
                }

                // ensure destination directory exists
                Directory.CreateDirectory(extractPath);

                string destinationPath = Path.Combine(extractPath, entry.Name);

                entry.ExtractToFile(destinationPath, true);
            }
        }
    }

    public static void ExtractFolderFromZip(string zipPath, string folderName, string extractPath) {
        // normalize slashes
        folderName = folderName.Replace('\\', '/').TrimEnd('/') + "/";

        using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
            foreach (var entry in archive.Entries) {
                // skip anything not in the target folder
                if (!entry.FullName.StartsWith(folderName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                // get relative path inside the target folder
                string relativePath = entry.FullName.Substring(folderName.Length);

                // skip root folder itself
                if (string.IsNullOrEmpty(relativePath)) {
                    continue;
                }

                string destinationPath = Path.Combine(extractPath, relativePath);

                // prevent path traversal
                string fullPath = Path.GetFullPath(destinationPath);
                if (!fullPath.StartsWith(Path.GetFullPath(extractPath), StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                // if it's a directory, create it
                if (string.IsNullOrEmpty(entry.Name)) {
                    Directory.CreateDirectory(fullPath);
                } else {
                    // ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

                    entry.ExtractToFile(fullPath, true);
                }
            }
        }
    }

    public static void RefreshFolderFromZip(string zipPath, string folderName, string extractPath) {
        folderName = folderName.Replace('\\', '/').TrimEnd('/') + "/";

        using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
            // track directories we’ve already ensured exist
            HashSet<string> createdDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries) {
                if (!entry.FullName.StartsWith(folderName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string relativePath = entry.FullName.Substring(folderName.Length);
                if (string.IsNullOrEmpty(relativePath)) {
                    continue;
                }

                string destinationPath = Path.Combine(extractPath, folderName, relativePath);
                string fullPath = Path.GetFullPath(destinationPath);
                string rootFullPath = Path.GetFullPath(extractPath);

                // safety check
                if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                // handle directory
                if (string.IsNullOrEmpty(entry.Name)) {
                    if (!Directory.Exists(fullPath)) {
                        Directory.CreateDirectory(fullPath);
                    }
                    continue;
                }

                // ensure directory exists
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !createdDirs.Contains(dir)) {
                    Directory.CreateDirectory(dir);
                    createdDirs.Add(dir);
                }

                // this is KEY: delete existing file before extracting
                if (File.Exists(fullPath)) {
                    File.Delete(fullPath);
                }

                // extract fresh file
                entry.ExtractToFile(fullPath);
            }
        }
    }

    public static bool IsSteamOS() {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return false;
        }

        const string osRelease = "/etc/os-release";

        if (!File.Exists(osRelease))
            return false;

        try {
            string text = File.ReadAllText(osRelease);

            return text.Contains("ID=steamos", StringComparison.OrdinalIgnoreCase)
                || text.Contains("NAME=\"SteamOS\"", StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
    }

    public static bool IsRunningFromAppBundle() {
        return AppContext.BaseDirectory.Contains(".app/") && RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    public static string GetGameFilename() {
        string extension = "";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            extension = ".exe";
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            extension = ".app";
        }

        return $"sm64coopdx{extension}";
    }

    public static string GetGamePath() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            string gamePath = AppContext.BaseDirectory;
            if (Program.tempUpdater) {
                // update is always installed to /Applications/ so directly override var
                gamePath = Path.Combine("/Applications/",GetGameFilename());
            } else if (IsRunningFromAppBundle()) {
                // truncate to .app if we are running from an app bundle
                int appIndex = gamePath.IndexOf(".app", StringComparison.Ordinal);
                gamePath = gamePath.Substring(0, appIndex + 4);
            }
            return gamePath;
        } else {
            return Path.Combine(Program.gamePath, GetGameFilename());
        }
    }

    public static string GetAppDataPath() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Path.Combine(AppContext.BaseDirectory, IsRunningFromAppBundle() ? "/Contents/MacOS/" : "");
        } else {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "coopdx-updater");
        }
    }

    // borrowed from coop-compiler
    public static async Task<string> GetAsync(string url) {
        using (var client = new HttpClient()) {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36");

            // HTTP GET
            HttpResponseMessage response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode) {
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
        }

        return null;
    }

    public static string SloppyExtract(string json, string field) {
        json = json.Replace(" ", "");

        string fieldJson = "\"" + field + "\":";
        if (!json.Contains(fieldJson)) { return null; }

        string after = json.Split(new string[] { fieldJson }, 2, StringSplitOptions.RemoveEmptyEntries)[1];
        string[] valueSplit = after.Split('"');
        if (valueSplit.Count() < 2) { return null; }

        return valueSplit[1];
    }

    public static async Task<string> GetSloppyAsync(string url, string parameter) {
        string jsonString = await GetAsync(url);
        if (jsonString == null) { return null; }

        return SloppyExtract(jsonString, parameter);
    }

    public static void StartGame() {
        Directory.SetCurrentDirectory(Program.gamePath);
        Process.Start(new ProcessStartInfo {
            FileName = GetGamePath(),
            Arguments = "--skip-update-check",
            UseShellExecute = true
        });
    }
}
