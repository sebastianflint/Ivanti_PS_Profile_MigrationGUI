using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace Ivanti_PS_Profile_MigrationGUI
{
    public static class EmpImporter
    {
        /// <summary>
        /// Runs EMP import using apps.txt in the EXE directory.
        /// </summary>
        public static Task<int> RunEmpManagedAppDataTxtDrivenAsync(
            RichTextBox richTextBoxLog,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = Path.Combine(exeDir, "apps.txt");
            return RunEmpManagedAppDataTxtDrivenInternalAsync(richTextBoxLog, defaultPath, cancellationToken);
        }

        /// <summary>
        /// Runs EMP import using a custom list file (each line = app).
        /// </summary>
        public static Task<int> RunEmpManagedAppDataTxtDrivenAsync(
            RichTextBox richTextBoxLog,
            string appListPath,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunEmpManagedAppDataTxtDrivenInternalAsync(richTextBoxLog, appListPath, cancellationToken);
        }

        private static async Task<int> RunEmpManagedAppDataTxtDrivenInternalAsync(
            RichTextBox richTextBoxLog,
            string appListPath,
            CancellationToken cancellationToken)
        {
            if (richTextBoxLog == null) throw new ArgumentNullException(nameof(richTextBoxLog));

            // PowerShell script
            var psScript = @"
param(
    [string] $AppListPath
)

$ErrorActionPreference = 'Stop'

function Get-EmClientPath {
    $regPaths = @(
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\AppSense\Environment Manager',
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Ivanti\Environment Manager',
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\AppSense\Environment Manager',
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Ivanti\Environment Manager'
    )
    foreach ($p in $regPaths) {
        try {
            $val = (Get-ItemProperty -Path $p -ErrorAction Stop).ClientPath
            if ($null -ne $val -and $val -ne '') { return $val }
        } catch {}
    }
    return $null
}

function Format-AppLiteral([string]$s) {
    if ($s -match ""[\s']"") {
        return ""'"" + ($s -replace ""'"", ""''"") + ""'""
    }
    return $s
}

try {
    Write-Output ('PowerShell version: ' + ($PSVersionTable.PSVersion -as [string]))
    Write-Output ('Using app list file: ' + $AppListPath)

    if (-not (Test-Path -LiteralPath $AppListPath)) {
        Write-Error ('App list file not found: ' + $AppListPath)
        exit 4
    }

    $apps = Get-Content -LiteralPath $AppListPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
    if ($apps.Count -eq 0) {
        Write-Error 'No apps found in app list file.'
        exit 5
    }

    Write-Output ('Apps: ' + [string]::Join(', ', $apps))

    Write-Output 'Searching for Environment Manager ClientPath in registry...'
    $clientPath = Get-EmClientPath
    if (-not $clientPath) {
        Write-Error 'ClientPath registry value not found in known locations.'
        exit 2
    }

    Write-Output ('Found ClientPath: ' + $clientPath)

    $dllPath = Join-Path $clientPath 'EmCmdlet.dll'
    if (-not (Test-Path -LiteralPath $dllPath)) {
        Write-Error ('EmCmdlet.dll not found at: ' + $dllPath)
        exit 3
    }

    $useLiteral = $false
    try {
        $paramTable = (Get-Command Import-Module -ErrorAction Stop).Parameters
        if ($paramTable.ContainsKey('LiteralPath')) { $useLiteral = $true }
    } catch {}

    if ($useLiteral) {
        Write-Output 'Loading module with -LiteralPath...'
        Import-Module -LiteralPath $dllPath -ErrorAction Stop
    } else {
        Write-Output 'Loading module without -LiteralPath (positional path)...'
        Import-Module $dllPath -ErrorAction Stop
    }

    # ===== APImportPath handling =====
    $importPath = [Environment]::GetEnvironmentVariable('APImportPath', 'User')
    if ($null -eq $importPath -or $importPath.Trim() -eq '') { $importPath = 'local' }
    Write-Output ('APImportPath: ' + $importPath)

    if ($importPath -ne 'local') {
        # Validate path exists before processing any app
        if (-not (Test-Path -LiteralPath $importPath)) {
            Write-Error ('APImportPath not found: ' + $importPath)
            exit 6
        }
    }
    # =================================

    $overallFailures = 0
    foreach ($app in $apps) {
        if ([string]::IsNullOrWhiteSpace($app)) { continue }

        $literal = Format-AppLiteral $app

        if ($importPath -ne 'local') {
            Write-Output ('Running: Import-EMPManagedAppData -App ' + $literal + ' -Merge -ProfilePath ""' + $importPath + '""')
        }
        else {
            Write-Output ('Running: Import-EMPManagedAppData -App -Merge' + $literal)
        }

        try {
            if ($importPath -ne 'local') {
                Import-EMPManagedAppData -App $app -Merge -ProfilePath $importPath -ErrorAction Stop
            }
            else {
                Import-EMPManagedAppData -App $app -Merge -ErrorAction Stop
            }

            Write-Output ('SUCCESS: ' + $literal)

            # Write success flag to HKCU
            $regPath = 'HKCU:\Software\AppSense\UVConfig'
            if (-not (Test-Path -LiteralPath $regPath)) {
                New-Item -Path $regPath -Force | Out-Null
            }
            New-ItemProperty -Path $regPath -Name $app -Value 1 -PropertyType DWord -Force | Out-Null
            Write-Output ('Flag written: ' + $regPath + '\' + $app + ' = 1')
        }
        catch {
            $msg = $_.Exception.Message

            if ($msg -match 'Application or Application Group was not found') {
                Write-Output ('WARN: ' + $literal + ' -> ' + $msg)
                continue
            }

            $overallFailures++
            Write-Error ('FAILED: ' + $literal + ' -> ' + $msg)
        }
    }

    if ($overallFailures -gt 0) {
        Write-Error ('Completed with failures: ' + $overallFailures)
        exit 10
    } else {
        Write-Output 'All apps completed successfully.'
        exit 0
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}
";



            // Temp script path
            var tempPs1 = Path.Combine(Path.GetTempPath(), "emp_import_" + Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(tempPs1, psScript, Encoding.UTF8);

            // Select 64-bit PowerShell
            string psExe;
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (Environment.Is64BitOperatingSystem)
            {
                psExe = Environment.Is64BitProcess
                    ? Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe")
                    : Path.Combine(windowsDir, "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
            }
            else
            {
                psExe = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            }

            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempPs1}\" -AppListPath \"{appListPath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = psExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Action<string> AppendLine = line =>
            {
                if (string.IsNullOrEmpty(line) || richTextBoxLog.IsDisposed) return;
                if (richTextBoxLog.InvokeRequired)
                    richTextBoxLog.BeginInvoke((Action)(() => richTextBoxLog.AppendText(line + Environment.NewLine)));
                else
                    richTextBoxLog.AppendText(line + Environment.NewLine);
            };

            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var tcs = new TaskCompletionSource<int>();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLine("ERR] " + e.Data); };
                proc.Exited += (s, e) => { try { tcs.TrySetResult(proc.ExitCode); } catch { } };

                try
                {
                    AppendLine("Starting 64-bit PowerShell: " + psExe);
                    AppendLine("App list path: " + appListPath);

                    if (!proc.Start())
                    {
                        AppendLine("Failed to start PowerShell process.");
                        return -1;
                    }

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    using (cancellationToken.Register(() =>
                    {
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                    }))
                    {
                        var exit = await tcs.Task.ConfigureAwait(false);
                        return exit;
                    }
                }
                finally
                {
                    try { if (File.Exists(tempPs1)) File.Delete(tempPs1); } catch { }
                }
            }
        }
    }

}
