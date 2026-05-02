using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace coopdx_updater {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            if (!Updater.CheckForUpdate()) {
                Process.Start("sm64coopdx.exe", "--skip-update-check");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
