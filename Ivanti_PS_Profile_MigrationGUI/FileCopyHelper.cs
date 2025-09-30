using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ivanti_PS_Profile_MigrationGUI
{
    public static class FileCopyHelper
    {
        /// <summary>
        /// Reads a .txt list with lines formatted as "source|destination" and copies them.
        /// Both files and directories are supported.
        /// Logs progress into the given RichTextBox.
        /// </summary>
        public static void CopyFromListFile(string listFilePath, RichTextBox logBox)
        {
            if (logBox == null) throw new ArgumentNullException(nameof(logBox));
            if (string.IsNullOrWhiteSpace(listFilePath) || !File.Exists(listFilePath))
            {
                AppendLog(logBox, $"List file not found: {listFilePath}");
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(listFilePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppendLog(logBox, "Error reading list file: " + ex.Message);
                return;
            }

            foreach (var raw in lines)
            {
                var line = (raw ?? string.Empty).Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length != 2)
                {
                    AppendLog(logBox, "Invalid line (missing '|'): " + line);
                    continue;
                }

                var source = parts[0].Trim();
                var dest = parts[1].Trim();

                try
                {
                    if (File.Exists(source))
                    {
                        // Copy single file
                        var destDir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Copy(source, dest, true);
                        AppendLog(logBox, $"Copied file: {source} -> {dest}");
                    }
                    else if (Directory.Exists(source))
                    {
                        // Copy entire directory
                        CopyDirectory(source, dest);
                        AppendLog(logBox, $"Copied directory: {source} -> {dest}");
                    }
                    else
                    {
                        AppendLog(logBox, $"Source not found: {source}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(logBox, $"Error copying {source} -> {dest}: {ex.Message}");
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, dest);
            }
        }

        private static void AppendLog(RichTextBox logBox, string message)
        {
            if (logBox.IsDisposed) return;

            if (logBox.InvokeRequired)
                logBox.BeginInvoke((Action)(() => logBox.AppendText(message + Environment.NewLine)));
            else
                logBox.AppendText(message + Environment.NewLine);
        }
    }
}
