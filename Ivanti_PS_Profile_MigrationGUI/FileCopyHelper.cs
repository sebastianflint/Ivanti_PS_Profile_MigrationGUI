using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

public static class FileCopyHelper
{
    /// <summary>
    /// Reads a .txt list and executes actions:
    /// - COPY:  'source|destination'  (files or directories)
    /// - REG:   'REGIMPORT|path-to.reg' (or 'REG|path-to.reg')
    /// Logs every step to the provided RichTextBox.
    /// </summary>
    public static void ExecuteFromListFile(string listFilePath, RichTextBox logBox)
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
            if (line.Length == 0) continue;
            if (line.StartsWith("#") || line.StartsWith(";")) continue;

            // REGIMPORT / REG | <path>
            var pipeIdx = line.IndexOf('|');
            if (pipeIdx > 0)
            {
                var firstToken = line.Substring(0, pipeIdx).Trim();
                var rest = line.Substring(pipeIdx + 1).Trim();

                if (firstToken.Equals("REGIMPORT", StringComparison.OrdinalIgnoreCase) ||
                    firstToken.Equals("REG", StringComparison.OrdinalIgnoreCase))
                {
                    ImportRegFile(rest, logBox);
                    continue;
                }

                // Otherwise: treat as COPY (source|dest)
                var source = ExpandVars(firstToken);
                var dest = ExpandVars(rest);
                CopySourceToDest(source, dest, logBox);
                continue;
            }

            // Single token line with no pipe: warn
            AppendLog(logBox, "Invalid line (expected 'source|dest' or 'REGIMPORT|path.reg'): " + line);
        }
    }

    private static void CopySourceToDest(string source, string dest, RichTextBox logBox)
    {
        try
        {
            if (File.Exists(source))
            {
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                File.Copy(source, dest, true);
                AppendLog(logBox, $"Copied file: {source} -> {dest}");
            }
            else if (Directory.Exists(source))
            {
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

    private static void ImportRegFile(string regPathRaw, RichTextBox logBox)
    {
        var regPath = ExpandVars(regPathRaw);

        if (!File.Exists(regPath))
        {
            AppendLog(logBox, $"REGIMPORT: file not found: {regPath}");
            return;
        }

        AppendLog(logBox, $"REGIMPORT: {regPath}");

        // Use reg.exe (silent). Note: importing HKLM may require elevation.
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"import \"{regPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    AppendLog(logBox, stdout.Trim());
                if (!string.IsNullOrEmpty(stderr))
                    AppendLog(logBox, "ERR] " + stderr.Trim());

                if (p.ExitCode == 0)
                {
                    AppendLog(logBox, "REGIMPORT: success.");
                }
                else
                {
                    AppendLog(logBox, $"REGIMPORT: failed (exit {p.ExitCode}). " +
                                      "If importing HKLM keys, run the app elevated.");
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog(logBox, "REGIMPORT: error -> " + ex.Message);
        }
    }

    private static string ExpandVars(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

        // Manual case-insensitive replace of %exe%
        string token = "%exe%";
        int idx = input.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            input = input.Substring(0, idx) + exeDir + input.Substring(idx + token.Length);
        }

        // Expand environment variables like %TEMP%, %USERPROFILE%, etc.
        input = Environment.ExpandEnvironmentVariables(input);

        return input;
    }


    private static void AppendLog(RichTextBox logBox, string message)
    {
        if (logBox == null || logBox.IsDisposed) return;

        if (logBox.InvokeRequired)
            logBox.BeginInvoke((Action)(() => logBox.AppendText(message + Environment.NewLine)));
        else
            logBox.AppendText(message + Environment.NewLine);
    }
}
