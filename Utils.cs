using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

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
        bool runningFromAppBundle = AppContext.BaseDirectory.Contains(".app/Contents/");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return runningFromAppBundle ? AppContext.BaseDirectory : "sm64coopdx.app";
        } else {
            return GetGameFilename();
        }
    }

    public static string GetAppDataPath() {
        bool runningFromAppBundle = AppContext.BaseDirectory.Contains(".app/Contents/");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Path.Combine(AppContext.BaseDirectory, runningFromAppBundle ? "/Contents/MacOS/" : "");
        } else {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "coopdx-updater");
        }
    }

    public static void StartGame() {
        Process.Start(new ProcessStartInfo {
            FileName = GetGamePath(),
            Arguments = "--skip-update-check",
            UseShellExecute = true
        });
    }
}
