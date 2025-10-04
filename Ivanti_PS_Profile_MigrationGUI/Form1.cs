using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ivanti_PS_Profile_MigrationGUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            LoadAppsIntoCheckedListBox();

            this.FormClosing += Form1_FormClosing;

            // (Optional) also save if the app crashes
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            string computertype = System.Environment.GetEnvironmentVariable("APComputerType");

            if (computertype == "VDI" || computertype == "RDS")
            {
                lblDisclaimer.Text = "Sie befinden sich aktuell auf einem Citrix System.\n\nIst dies Ihr aktueller Hauptarbeitsplatz?\nDann starten Sie die Migration Ihrer persönlichen Applikationseinstellungen.";

            }
            else
            {
                lblDisclaimer.Text = "Sie befinden sich aktuell auf einem Notebook/ Desktop.\n\nIst dies Ihr aktueller Hauptarbeitsplatz?\nDann starten Sie die Migration Ihrer persönlichen Applikationseinstellungen.";
            }

            var importpath = Environment.GetEnvironmentVariable("APImportPath", EnvironmentVariableTarget.User);

            if (string.IsNullOrWhiteSpace(importpath) || importpath.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                toolStripStatusLabel1.Text = "ImportMode = local";
            }
            else
            {
                toolStripStatusLabel1.Text = "ImportMode = network";
            }
        }


        private async void btnStartMigration_Click(object sender, EventArgs e)
        {
            {
                btnStartMigration.Enabled = false;
                try
                {
                    // Make single-click check behavior obvious
                    checkedListBox1.CheckOnClick = true;

                    var selectedInternal = GetCheckedInternalNames();
                    if (selectedInternal.Count == 0)
                    {
                        MessageBox.Show(this, "Please check at least one application (checkbox on the left).",
                                        "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var tempListPath = Path.Combine(Path.GetTempPath(), "apps_selected_" + Guid.NewGuid().ToString("N") + ".txt");
                    File.WriteAllLines(tempListPath, selectedInternal.ToArray(), Encoding.UTF8);

                    richTextBoxLog.AppendText("Starting EMP import (selected apps)..." + Environment.NewLine);
                    var exit = await EmpImporter.RunEmpManagedAppDataTxtDrivenAsync(richTextBoxLog, tempListPath);
                    richTextBoxLog.AppendText("Done. Exit code: " + exit + Environment.NewLine);

                    try { File.Delete(tempListPath); } catch { }
                }
                catch (Exception ex)
                {
                    richTextBoxLog.AppendText("Exception: " + ex + Environment.NewLine);
                }
                finally
                {
                    btnStartMigration.Enabled = true;
                }
            }
        }

        private void LoadAppsIntoCheckedListBox()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var appListPath = Path.Combine(exeDir, "apps.txt");

            if (!File.Exists(appListPath))
            {
                File.WriteAllText(appListPath,
        @"# One app per line. Optional: DisplayName | InternalAppName
# Lines starting with # are comments
Google Chrome | Google Chrome
Notepad++     | Notepad++ Group
7-Zip         | 7-Zip
Testpers      | Testpers Group
Notepad
", Encoding.UTF8);
            }

            // Parse lines → AppItem[]
            var items = new System.Collections.Generic.List<AppItem>();
            foreach (var raw in File.ReadAllLines(appListPath, Encoding.UTF8))
            {
                var line = (raw ?? string.Empty).Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string display, internalName;
                int sep = line.IndexOf('|');
                if (sep >= 0)
                {
                    display = line.Substring(0, sep).Trim();
                    internalName = line.Substring(sep + 1).Trim();
                    if (display.Length == 0) display = internalName;
                    if (internalName.Length == 0) internalName = display;
                }
                else
                {
                    display = internalName = line;
                }

                // avoid dupes by internal name (case-insensitive)
                bool exists = items.Exists(i => string.Equals(i.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
                if (!exists) items.Add(new AppItem(display, internalName));
            }

            checkedListBox1.BeginUpdate();
            try
            {
                checkedListBox1.Items.Clear();

                // Open HKCU\Software\AppSense\UVConfig once
                var regPath = @"Software\AppSense\UVConfig";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath))
                {
                    foreach (var item in items)
                    {
                        int idx = checkedListBox1.Items.Add(item);

                        bool alreadyImported = key != null && key.GetValue(item.InternalName) != null;
                        // Unchecked if flag exists; checked otherwise
                        checkedListBox1.SetItemChecked(idx, !alreadyImported);
                    }
                }
            }
            finally
            {
                checkedListBox1.EndUpdate();
            }

            // Friendlier UX
            checkedListBox1.CheckOnClick = true;
        }

        private System.Collections.Generic.List<string> GetCheckedInternalNames()
        {
            var result = new System.Collections.Generic.List<string>();

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (!checkedListBox1.GetItemChecked(i)) continue;

                var obj = checkedListBox1.Items[i];

                // AppItem support
                if (obj is AppItem ai && !string.IsNullOrWhiteSpace(ai.InternalName))
                {
                    result.Add(ai.InternalName.Trim());
                }
                // Backward-compat: plain string items
                else if (obj is string s && !string.IsNullOrWhiteSpace(s))
                {
                    result.Add(s.Trim());
                }
            }

            return result;
        }

        private void btnStartMigrationWSG_Click(object sender, EventArgs e)
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var listFilePath = Path.Combine(exeDir, "copylist.txt");

            FileCopyHelper.ExecuteFromListFile(listFilePath, richTextBoxLog);

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            TrySaveLogToTemp();
        }

        private void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            TrySaveLogToTemp();
            // Re-throw default handling or show your own dialog
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            TrySaveLogToTemp();
            // Note: app likely terminates after this
        }

        private void TrySaveLogToTemp()
        {
            try
            {
                var path = SaveRichTextLogToTemp(richTextBoxLog);
                // Optional: inform user once
                // MessageBox.Show(this, "Log saved to:\r\n" + path, "Log saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                // Swallow — app is closing; avoid blocking shutdown
            }
        }

        /// <summary>
        /// Writes the content of the provided RichTextBox to a UTF-8 .txt file in %TEMP%.
        /// Returns the full file path.
        /// </summary>
        private static string SaveRichTextLogToTemp(RichTextBox rtb)
        {
            var tempDir = Path.GetTempPath();
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var file = Path.Combine(tempDir, $"EMP_ImportLog_{stamp}.txt");

            string text;
            if (rtb == null || rtb.IsDisposed)
            {
                text = string.Empty;
            }
            else if (rtb.InvokeRequired)
            {
                // marshal to UI thread to read safely
                text = (string)rtb.Invoke(new Func<string>(() => rtb.Text));
            }
            else
            {
                text = rtb.Text;
            }

            // Ensure directory exists (TEMP should exist, but just in case)
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(file, text ?? string.Empty, Encoding.UTF8);
            return file;
        }
    }
}

