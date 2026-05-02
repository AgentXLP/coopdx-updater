using System;
using System.IO;
using System.Windows.Forms;

namespace coopdx_updater {
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e) {
            Updater.DownloadLatestVersion(progressBar, infoLabel);
        }

        private void cancelButton_Click(object sender, EventArgs e) {
            Updater.CancelDownload();
            Environment.Exit(0);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e) {
            Updater.CancelDownload();
        }
    }
}
