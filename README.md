# Ivanti_PS_Profile_MigrationGUI

# Ivanti / AppSense Profile Import GUI

A small WinForms utility written in C# for automating **Ivanti / AppSense Environment Manager** profile imports.

## 📷 Preview

<img width="802" height="481" alt="image" src="https://github.com/user-attachments/assets/03e7288d-f655-4c1b-9f50-c4a5fc2a6533" />


It provides:
- A simple UI with a `CheckedListBox` showing available applications.
- Execution of PowerShell scripts that call `Import-EMPManagedAppData`.
- Logging to a RichTextBox (and auto-export to a log file in `%TEMP%` on exit).
- Optional file/folder copy actions **and registry imports** from a plain text definition file.

---

## ✨ Features

- **App list from file:**  
  Define available applications in `apps.txt` (one per line).  
- **Registry flagging:**  
  After a successful import, a flag is written to: HKCU\Software\AppSense\UVConfig<AppName> = 1 (DWORD)
- **Smart UI behavior:**  
- Already imported apps (detected from registry) are unchecked at startup.
- Checked items = to be imported.
- Run only selected apps, or all.

- **Logging:**  
- RichTextBox shows PowerShell stdout/stderr and copy/registry import actions.
- On application exit, the log is written to `%TEMP%\EMP_ImportLog_yyyyMMdd_HHmmss.txt`.

- **File & Registry operations (copylist.txt):**  
- Copy files or folders (`source|destination`)  
- Import `.reg` files (`REGIMPORT|path-to.reg`)  

  

  
## apps.txt Example

```
# DisplayName | InternalAppName
Google Chrome | Google Chrome
Notepad++     | Notepad++ Group
7-Zip         | 7-Zip
Testpers      | Testpers Group
# single-column lines:
Notepad
```
## copylist.txt Example

```
C:\Temp\mydoc.txt|C:\Temp2\mydoc.txt
c:\temp\Test|C:\Temp2\MyFolder
```

---

## 🔧 Requirements

- Windows (32/64 bit)
- .NET Framework 4.7.2+ (tested with 4.x and C# 7.3)
- Ivanti / AppSense **Environment Manager** client installed  
(PowerShell cmdlets available via `EmCmdlet.dll`)

---
## ▶️ Usage

1. Place `apps.txt` and optionally `copylist.txt` in the same directory as the `.exe`.
2. Run the application.
3. Select applications from the list and click **Run Selected**.
4. Monitor progress in the log panel.
5. On exit, check `%TEMP%` for the saved log file.

---

## 📝 Notes

- If an app is not found (`Application or Application Group was not found`), it is logged as a **WARN** and skipped.
- PowerShell is launched in 64-bit mode automatically.
- Registry flags are **not cleared** between runs.

---
---

## 📂 Project Structure

- `EmpImporter.cs` — handles PowerShell execution for EMP import.  
- `FileCopyHelper.cs` — reads `copylist.txt` and copies files/folders.
- `AppItem.cs` — HelperClass for Reading `apps.txt`
- `Form1.cs` — main WinForms UI with `CheckedListBox`, `RichTextBoxLog`, buttons.  
- `apps.txt` — defines available applications.  
- `copylist.txt` — (optional) defines file/folder copy tasks.  

