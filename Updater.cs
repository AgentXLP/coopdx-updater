using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace coopdx_updater {
    static class Updater {
        const string VERSION_URL = "https://raw.githubusercontent.com/coop-deluxe/sm64coopdx/refs/heads/main/src/pc/network/version.h";
        const string VERSION_IDENTIFIER = "#define SM64COOPDX_VERSION \"";
        const string UPDATE_URL = "https://github.com/coop-deluxe/sm64coopdx/releases/latest/download/sm64coopdx_Windows_OpenGL.zip";

        static WebClient webClient = null;

        public static bool CheckForExecutable() {
            return File.Exists("sm64coopdx.exe");
        }

        public static bool CheckForUpdate() {
            if (!File.Exists("sm64coopdx.exe")) {
                return true;
            }

            // fetch local version
            int major = 1;
            int minor = 4;
            int patch = 1;
            if (File.Exists("sm64coopdx.exe")) {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo("sm64coopdx.exe");
                if (versionInfo.FileVersion != null) {
                    string[] versionData = versionInfo.FileVersion.Substring(1).Split('.');
                    if (!string.IsNullOrEmpty(versionInfo.FileVersion)) {
                        major = int.Parse(versionData[0]);
                        minor = int.Parse(versionData[1]);
                        if (versionData.Length > 2) {
                            patch = int.Parse(versionData[2]);
                        } else {
                            patch = 0;
                        }
                    }
                }
            }

            // fetch remote version
            int remoteMajor = 0;
            int remoteMinor = 0;
            int remotePatch = 0;
            string remoteVersionInfo = "";
            using (WebClient client = new WebClient()) {
                remoteVersionInfo = client.DownloadString(VERSION_URL);
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

        public static void CancelDownload() {
            if (webClient != null) {
                webClient.CancelAsync();
                webClient.Dispose();
                webClient = null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1000) {
                try {
                    if (File.Exists("update.zip")) {
                        File.Delete("update.zip");
                    }
                    return;
                } catch (IOException) { // file is in use still
                    Thread.Sleep(500);
                }
            }
        }

        public static void DownloadLatestVersion(ProgressBar progressBar, Label info) {
            progressBar.Style = ProgressBarStyle.Blocks;

            webClient = new WebClient();

            // update progress bar as download proceeds
            webClient.DownloadProgressChanged += (_, ev) => {
                progressBar.Value = ev.ProgressPercentage;
                info.Text = $"Downloaded {Utils.BytesToMegabytes(ev.BytesReceived)} MB / {Utils.BytesToMegabytes(ev.TotalBytesToReceive)} MB";
            };

            // show message when finished
            webClient.DownloadFileCompleted += (_, ev) => {
                info.Text = "Installing update...";
                InstallLatestVersion();
                File.Delete("update.zip");
                info.Text = "Launching game...";
                Process.Start("sm64coopdx.exe", "--skip-update-check");

                webClient.Dispose();
                webClient = null;
                Environment.Exit(0);
            };

            // start the asynchronous download
            webClient.DownloadFileAsync(new Uri(UPDATE_URL), "update.zip");
        }

        static void InstallLatestVersion() {
            Utils.ExtractFilesFromZip("update.zip", Application.StartupPath);
            Utils.ExtractFolderFromZip("update.zip", "lang", "lang");
            Utils.RefreshFolderFromZip("update.zip", "mods", Application.StartupPath);
            Utils.RefreshFolderFromZip("update.zip", "dynos", Application.StartupPath);
        }
    }
}
