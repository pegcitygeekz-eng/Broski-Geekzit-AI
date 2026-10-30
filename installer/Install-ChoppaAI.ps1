param(
    [string] $DestinationPath,
    [switch] $Silent,
    [switch] $FromBootstrapper
)

$ErrorActionPreference = 'Stop'
if ($FromBootstrapper) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ChoppaSetupConsole {
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
    public static void Hide() {
        var window = GetConsoleWindow();
        if (window != IntPtr.Zero) {
            ShowWindow(window, 0);
        }
    }
}
'@
    [ChoppaSetupConsole]::Hide()
}

$payloadPath = Join-Path $PSScriptRoot 'ChoppaAI-Payload.zip'
$uninstallKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ChoppaAI'
$programsPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Broski AI'
$startupPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$startShortcutPath = Join-Path $programsPath 'Broski.lnk'
$uninstallShortcutPath = Join-Path $programsPath 'Uninstall Broski.lnk'
$startupShortcutPath = Join-Path $startupPath 'Broski.lnk'
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Broski.lnk'

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Windows.Forms

function Get-SafeArchivePath {
    param([string] $Root, [string] $EntryName)

    $normalizedName = $EntryName.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::IsPathRooted($normalizedName)) {
        throw "The installer archive contains a rooted path: $EntryName"
    }

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    $entryPath = [IO.Path]::GetFullPath((Join-Path $rootPath $normalizedName))
    if (-not $entryPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The installer archive contains a path outside the installation folder: $EntryName"
    }

    return $entryPath
}

function Get-SafeInstallPath {
    param([string] $Root, [string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) {
        throw "The existing installation manifest contains an invalid relative path: $RelativePath"
    }

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    $entryPath = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    if (-not $entryPath.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The existing installation manifest points outside its install folder: $RelativePath"
    }

    return $entryPath
}

function New-AppShortcut {
    param(
        [string] $ShortcutPath,
        [string] $TargetPath,
        [string] $WorkingDirectory,
        [string] $Description,
        [string] $IconPath
    )

    $parentPath = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Path $parentPath -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = $Description
    if ($IconPath) {
        $shortcut.IconLocation = $IconPath
    }
    $shortcut.Save()
}

function Install-ChoppaAI {
    param([string] $InstallDirectory)

    if (-not (Test-Path -LiteralPath $payloadPath -PathType Leaf)) {
        throw "The application payload is missing: $payloadPath"
    }
    $InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
    $rootPath = [IO.Path]::GetPathRoot($InstallDirectory)
    if ($InstallDirectory.TrimEnd('\') -eq $rootPath.TrimEnd('\')) {
        throw 'Choose a dedicated installation folder, not a drive root.'
    }

    $oldManifest = $null
    $oldManifestPath = Join-Path $InstallDirectory 'InstallManifest.json'
    if (Test-Path -LiteralPath $oldManifestPath -PathType Leaf) {
        try {
            $oldManifest = Get-Content -LiteralPath $oldManifestPath -Raw | ConvertFrom-Json
        }
        catch {
            throw "The existing installation manifest cannot be read. Existing files were left untouched. $($_.Exception.Message)"
        }

        if ($oldManifest.ProductId -ne 'ChoppaAI.DesktopPet' -or
            [IO.Path]::GetFullPath($oldManifest.InstallDirectory).TrimEnd('\') -ne $InstallDirectory.TrimEnd('\')) {
            throw 'The destination contains an unrecognized installation manifest. Existing files were left untouched.'
        }
        foreach ($relativePath in @($oldManifest.Files) + @($oldManifest.Directories)) {
            [void](Get-SafeInstallPath -Root $InstallDirectory -RelativePath $relativePath)
        }
    }

    $replacementDirectory = $InstallDirectory
    $replacementManifest = $oldManifest
    if (Test-Path -LiteralPath $uninstallKeyPath) {
        $registeredPath = (Get-ItemProperty -LiteralPath $uninstallKeyPath `
            -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
        if ([string]::IsNullOrWhiteSpace($registeredPath)) {
            throw 'Broski AI is registered, but its installation folder is missing. Existing files were left untouched.'
        }

        $replacementDirectory = [IO.Path]::GetFullPath($registeredPath)
        $registeredManifestPath = Join-Path $replacementDirectory 'InstallManifest.json'
        if (-not (Test-Path -LiteralPath $registeredManifestPath -PathType Leaf)) {
            throw "Broski AI is registered at '$replacementDirectory', but its ownership manifest is missing. Existing files were left untouched."
        }
        try {
            $replacementManifest = Get-Content -LiteralPath $registeredManifestPath -Raw | ConvertFrom-Json
        }
        catch {
            throw "The existing installation manifest cannot be read. Existing files were left untouched. $($_.Exception.Message)"
        }
        if ($replacementManifest.ProductId -ne 'ChoppaAI.DesktopPet' -or
            [IO.Path]::GetFullPath($replacementManifest.InstallDirectory).TrimEnd('\') -ne $replacementDirectory.TrimEnd('\')) {
            throw 'The registered installation manifest is invalid. Existing files were left untouched.'
        }
        foreach ($relativePath in @($replacementManifest.Files) + @($replacementManifest.Directories)) {
            [void](Get-SafeInstallPath -Root $replacementDirectory -RelativePath $relativePath)
        }
        if ($replacementDirectory.TrimEnd('\') -ne $InstallDirectory.TrimEnd('\') -and $null -ne $oldManifest) {
            throw 'A second Broski AI installation exists at the selected destination. Remove it before installing there.'
        }
    }

    if ($null -eq $replacementManifest) {
        $replacementDirectory = $InstallDirectory
    }

    foreach ($shortcutPath in @(
        $startShortcutPath,
        $uninstallShortcutPath,
        $startupShortcutPath,
        $desktopShortcutPath
    )) {
        if ((Test-Path -LiteralPath $shortcutPath) -and
            ($null -eq $replacementManifest -or $shortcutPath -notin @($replacementManifest.Shortcuts))) {
            throw "Setup will not overwrite an unrelated shortcut: $shortcutPath"
        }
    }

    $replacementAppPaths = @(
        (Join-Path $replacementDirectory 'Broski.exe'),
        (Join-Path $replacementDirectory 'BroskiLauncher.exe'),
        (Join-Path $replacementDirectory 'ChoppaAI.exe')
    )
    foreach ($process in Get-Process -Name Broski, BroskiLauncher, ChoppaAI -ErrorAction SilentlyContinue) {
        if ($process.Path -and $replacementAppPaths -contains [IO.Path]::GetFullPath($process.Path)) {
            throw 'Close Broski before running setup so the old installation can be replaced.'
        }
    }

    $protectedFolders = @(
        [Environment]::GetFolderPath('Windows'),
        [Environment]::GetFolderPath('ProgramFiles'),
        [Environment]::GetFolderPath('ProgramFilesX86')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    foreach ($protectedFolder in $protectedFolders) {
        $protectedPath = [IO.Path]::GetFullPath($protectedFolder).TrimEnd('\') + '\'
        if ($InstallDirectory.StartsWith($protectedPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Choose a user-writable installation folder instead of $protectedFolder."
        }
    }

    if (Test-Path -LiteralPath $InstallDirectory -PathType Container) {
        if ($null -eq $oldManifest -and @(Get-ChildItem -LiteralPath $InstallDirectory -Force).Count -gt 0) {
            throw "The destination folder is not empty. Choose another folder: $InstallDirectory"
        }
    }
    elseif (Test-Path -LiteralPath $InstallDirectory) {
        throw "The destination path is not a folder: $InstallDirectory"
    }

    $stagingDirectory = Join-Path ([IO.Path]::GetTempPath()) "ChoppaAI-install-$([guid]::NewGuid().ToString('N'))"
    $createdInstallDirectory = -not (Test-Path -LiteralPath $InstallDirectory -PathType Container)
    $createdShortcuts = [Collections.Generic.List[string]]::new()
    $registryWasWritten = $false
    try {
        $archive = [IO.Compression.ZipFile]::OpenRead($payloadPath)
        try {
            foreach ($entry in $archive.Entries) {
                [void](Get-SafeArchivePath -Root $stagingDirectory -EntryName $entry.FullName)
            }
        }
        finally {
            $archive.Dispose()
        }

        New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($payloadPath, $stagingDirectory)
    foreach ($requiredExecutable in @('Broski.exe', 'BroskiLauncher.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $requiredExecutable) -PathType Leaf)) {
            throw "The installer payload does not contain $requiredExecutable."
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory 'animations') -PathType Container)) {
        throw 'The installer payload does not contain the animation assets.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory 'blueprints') -PathType Container)) {
        throw 'The installer payload does not contain the blueprint assets.'
    }

        $newFiles = @(
            Get-ChildItem -LiteralPath $stagingDirectory -File -Recurse |
                ForEach-Object { $_.FullName.Substring($stagingDirectory.TrimEnd('\').Length + 1) }
        )
        $newDirectories = @(
            Get-ChildItem -LiteralPath $stagingDirectory -Directory -Recurse |
                ForEach-Object { $_.FullName.Substring($stagingDirectory.TrimEnd('\').Length + 1) }
        )

        if ($null -ne $replacementManifest) {
            foreach ($relativePath in @($replacementManifest.Files)) {
                $oldFilePath = Get-SafeInstallPath -Root $replacementDirectory -RelativePath $relativePath
                if (Test-Path -LiteralPath $oldFilePath -PathType Leaf) {
                    Remove-Item -LiteralPath $oldFilePath -Force
                }
            }
            $replacementManifestPath = Join-Path $replacementDirectory 'InstallManifest.json'
            if (Test-Path -LiteralPath $replacementManifestPath -PathType Leaf) {
                Remove-Item -LiteralPath $replacementManifestPath -Force
            }
            foreach ($relativePath in (@($replacementManifest.Directories) | Sort-Object { $_.Length } -Descending)) {
                $oldDirectoryPath = Get-SafeInstallPath -Root $replacementDirectory -RelativePath $relativePath
                if ((Test-Path -LiteralPath $oldDirectoryPath -PathType Container) -and
                    @(Get-ChildItem -LiteralPath $oldDirectoryPath -Force).Count -eq 0) {
                    Remove-Item -LiteralPath $oldDirectoryPath -Force
                }
            }
            foreach ($shortcutPath in @($replacementManifest.Shortcuts)) {
                if (-not [string]::IsNullOrWhiteSpace($shortcutPath) -and
                    (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
                    Remove-Item -LiteralPath $shortcutPath -Force
                }
            }
            if (Test-Path -LiteralPath $uninstallKeyPath) {
                $registeredPath = (Get-ItemProperty -LiteralPath $uninstallKeyPath `
                    -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
                if ($registeredPath -and
                    [IO.Path]::GetFullPath($registeredPath).TrimEnd('\') -eq $replacementDirectory.TrimEnd('\')) {
                    Remove-Item -LiteralPath $uninstallKeyPath -Recurse -Force
                }
            }
        }

        foreach ($shortcutPath in @(
            $startShortcutPath,
            $uninstallShortcutPath,
            $startupShortcutPath,
            $desktopShortcutPath
        )) {
            if (Test-Path -LiteralPath $shortcutPath) {
                throw "Setup will not overwrite an unrelated shortcut: $shortcutPath"
            }
        }

        $appPath = Join-Path $InstallDirectory 'Broski.exe'
        $launcherPath = Join-Path $InstallDirectory 'BroskiLauncher.exe'
        if ((Test-Path -LiteralPath $replacementDirectory -PathType Container) -and
            @(Get-ChildItem -LiteralPath $replacementDirectory -Force).Count -eq 0) {
            Remove-Item -LiteralPath $replacementDirectory -Force
            if ($replacementDirectory.TrimEnd('\') -eq $InstallDirectory.TrimEnd('\')) {
                $createdInstallDirectory = $true
            }
        }

        New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
        Get-ChildItem -LiteralPath $stagingDirectory -Force |
            Copy-Item -Destination $InstallDirectory -Recurse -Force

        $uninstallerPath = Join-Path $InstallDirectory 'Uninstall-ChoppaAI.cmd'
        New-AppShortcut -ShortcutPath $startShortcutPath -TargetPath $launcherPath `
            -WorkingDirectory $InstallDirectory -Description 'Broski AI desktop companion' -IconPath $appPath
        $createdShortcuts.Add($startShortcutPath)
        New-AppShortcut -ShortcutPath $uninstallShortcutPath -TargetPath $uninstallerPath `
            -WorkingDirectory $InstallDirectory -Description 'Uninstall Broski AI' -IconPath $appPath
        $createdShortcuts.Add($uninstallShortcutPath)
        New-AppShortcut -ShortcutPath $startupShortcutPath -TargetPath $launcherPath `
            -WorkingDirectory $InstallDirectory -Description 'Start Broski when I sign in' -IconPath $appPath
        $createdShortcuts.Add($startupShortcutPath)
        New-AppShortcut -ShortcutPath $desktopShortcutPath -TargetPath $launcherPath `
            -WorkingDirectory $InstallDirectory -Description 'Broski AI desktop companion' -IconPath $appPath
        $createdShortcuts.Add($desktopShortcutPath)

        $manifest = [ordered]@{
            ProductId = 'ChoppaAI.DesktopPet'
            Version = '0.0.3'
            InstallDirectory = $InstallDirectory
            Files = $newFiles
            Directories = $newDirectories
            Shortcuts = @($createdShortcuts)
        }
        $manifest | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath (Join-Path $InstallDirectory 'InstallManifest.json') -Encoding UTF8

        New-Item -Path $uninstallKeyPath -Force | Out-Null
        $registryWasWritten = $true
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name DisplayName -Value 'Broski AI'
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name DisplayVersion -Value '0.0.3'
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name Publisher -Value 'Broski AI'
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name InstallLocation -Value $InstallDirectory
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name DisplayIcon -Value $appPath
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name UninstallString -Value "`"$uninstallerPath`""
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name NoModify -Value 1 -Type DWord
        Set-ItemProperty -LiteralPath $uninstallKeyPath -Name NoRepair -Value 1 -Type DWord
    }
    catch {
        if ($registryWasWritten -and (Test-Path -LiteralPath $uninstallKeyPath)) {
            Remove-Item -LiteralPath $uninstallKeyPath -Recurse -Force
        }
        foreach ($shortcut in $createdShortcuts) {
            if (Test-Path -LiteralPath $shortcut) {
                Remove-Item -LiteralPath $shortcut -Force
            }
        }
        if ($createdInstallDirectory -and (Test-Path -LiteralPath $InstallDirectory -PathType Container)) {
            Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
        }
        throw
    }
    finally {
        if (Test-Path -LiteralPath $stagingDirectory -PathType Container) {
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
        }
    }

    $taskbarPinned = $false
    try {
        $shell = New-Object -ComObject Shell.Application
        $folder = $shell.Namespace((Split-Path -Parent $desktopShortcutPath))
        if ($null -ne $folder) {
            $shortcut = $folder.ParseName((Split-Path -Leaf $desktopShortcutPath))
            if ($null -ne $shortcut) {
                $pinVerb = $shortcut.Verbs() |
                    Where-Object { ($_.Name -replace '&', '') -match '^Pin to taskbar$' } |
                    Select-Object -First 1
                if ($null -ne $pinVerb) {
                    $pinVerb.DoIt()
                    $taskbarPinned = $true
                }
            }
        }
    }
    catch {
        Write-Warning "Windows did not allow automatic taskbar pinning: $($_.Exception.Message)"
    }

    return [pscustomobject]@{
        InstallDirectory = $InstallDirectory
        TaskbarPinned = $taskbarPinned
    }
}

function Show-SetupWizard {
    param([string] $DefaultInstallDirectory)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Broski AI Setup 0.0.3'
    $form.StartPosition = 'CenterScreen'
    $form.ClientSize = New-Object System.Drawing.Size(620, 370)
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $form.BackColor = [System.Drawing.Color]::FromArgb(24, 22, 31)
    $form.ForeColor = [System.Drawing.Color]::FromArgb(245, 242, 250)

    $header = New-Object System.Windows.Forms.Panel
    $header.Location = New-Object System.Drawing.Point(0, 0)
    $header.Size = New-Object System.Drawing.Size(620, 145)
    $header.BackColor = [System.Drawing.Color]::FromArgb(105, 61, 190)
    $form.Controls.Add($header)

    $title = New-Object System.Windows.Forms.Label
    $title.Text = 'Install Broski AI'
    $title.Location = New-Object System.Drawing.Point(28, 24)
    $title.Size = New-Object System.Drawing.Size(540, 38)
    $title.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 20)
    $title.ForeColor = [System.Drawing.Color]::White
    $header.Controls.Add($title)

    $description = New-Object System.Windows.Forms.Label
    $description.Text = "Setup replaces the previous Broski installation, then installs version 0.0.3. It creates Start menu, desktop, and sign-in startup shortcuts using the separate Broski launcher.`r`n`r`nOllama is separate and is not included."
    $description.Location = New-Object System.Drawing.Point(30, 73)
    $description.Size = New-Object System.Drawing.Size(555, 62)
    $description.ForeColor = [System.Drawing.Color]::FromArgb(244, 237, 255)
    $header.Controls.Add($description)

    $pathLabel = New-Object System.Windows.Forms.Label
    $pathLabel.Text = 'Install location'
    $pathLabel.Location = New-Object System.Drawing.Point(30, 158)
    $pathLabel.Size = New-Object System.Drawing.Size(150, 25)
    $pathLabel.ForeColor = [System.Drawing.Color]::FromArgb(245, 242, 250)
    $form.Controls.Add($pathLabel)

    $pathInput = New-Object System.Windows.Forms.TextBox
    $pathInput.Text = $DefaultInstallDirectory
    $pathInput.Location = New-Object System.Drawing.Point(30, 186)
    $pathInput.Size = New-Object System.Drawing.Size(430, 30)
    $pathInput.BackColor = [System.Drawing.Color]::FromArgb(39, 36, 49)
    $pathInput.ForeColor = [System.Drawing.Color]::FromArgb(245, 242, 250)
    $pathInput.BorderStyle = 'FixedSingle'
    $form.Controls.Add($pathInput)

    $browseButton = New-Object System.Windows.Forms.Button
    $browseButton.Text = 'Browse...'
    $browseButton.Location = New-Object System.Drawing.Point(472, 184)
    $browseButton.Size = New-Object System.Drawing.Size(105, 32)
    $browseButton.UseVisualStyleBackColor = $false
    $browseButton.BackColor = [System.Drawing.Color]::FromArgb(49, 45, 61)
    $browseButton.ForeColor = [System.Drawing.Color]::FromArgb(245, 242, 250)
    $browseButton.Add_Click({
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = 'Choose a parent folder for the Broski installation folder.'
        $dialog.ShowNewFolderButton = $true
        $currentParent = Split-Path -Parent $pathInput.Text
        if (Test-Path -LiteralPath $currentParent -PathType Container) {
            $dialog.SelectedPath = $currentParent
        }
        if ($dialog.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
            $pathInput.Text = Join-Path $dialog.SelectedPath 'Broski'
        }
        $dialog.Dispose()
    }.GetNewClosure())
    $form.Controls.Add($browseButton)

    $progress = New-Object System.Windows.Forms.ProgressBar
    $progress.Location = New-Object System.Drawing.Point(30, 232)
    $progress.Size = New-Object System.Drawing.Size(547, 12)
    $progress.Style = 'Marquee'
    $progress.Visible = $false
    $form.Controls.Add($progress)

    $status = New-Object System.Windows.Forms.Label
    $status.Location = New-Object System.Drawing.Point(30, 251)
    $status.Size = New-Object System.Drawing.Size(547, 26)
    $status.ForeColor = [System.Drawing.Color]::FromArgb(210, 204, 221)
    $form.Controls.Add($status)

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = 'Cancel'
    $cancelButton.Location = New-Object System.Drawing.Point(382, 315)
    $cancelButton.Size = New-Object System.Drawing.Size(90, 34)
    $cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $cancelButton.UseVisualStyleBackColor = $false
    $cancelButton.BackColor = [System.Drawing.Color]::FromArgb(49, 45, 61)
    $cancelButton.ForeColor = [System.Drawing.Color]::FromArgb(245, 242, 250)
    $form.Controls.Add($cancelButton)

    $installButton = New-Object System.Windows.Forms.Button
    $installButton.Text = 'Install'
    $installButton.Location = New-Object System.Drawing.Point(482, 315)
    $installButton.Size = New-Object System.Drawing.Size(95, 34)
    $installButton.UseVisualStyleBackColor = $false
    $installButton.BackColor = [System.Drawing.Color]::FromArgb(130, 82, 220)
    $installButton.ForeColor = [System.Drawing.Color]::White
    $installButton.Add_Click({
        if ([string]::IsNullOrWhiteSpace($pathInput.Text)) {
            [void][System.Windows.Forms.MessageBox]::Show(
                $form, 'Enter an install location to continue.', 'Install Broski AI',
                [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            return
        }

        $installButton.Enabled = $false
        $browseButton.Enabled = $false
        $cancelButton.Enabled = $false
        $pathInput.Enabled = $false
        $progress.Visible = $true
        $status.Text = 'Removing the old app and installing the new version...'
        [System.Windows.Forms.Application]::DoEvents()
        try {
            $result = Install-ChoppaAI -InstallDirectory $pathInput.Text.Trim()
            $pinMessage = if ($result.TaskbarPinned) {
                'Broski AI was pinned to your taskbar.'
            }
            else {
                'Windows did not allow setup to pin it automatically. To pin it, right-click the desktop shortcut and select Pin to taskbar.'
            }
            $form.Tag = $result
            [void][System.Windows.Forms.MessageBox]::Show(
                $form,
                "The old Broski installation was removed and the new version was installed successfully.`r`n`r`nLocation: $($result.InstallDirectory)`r`n`r`nA separate Broski launcher, Start menu shortcut, desktop shortcut, and sign-in startup launcher were created.`r`n`r`n$pinMessage",
                'Broski installation complete',
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Information)
            $form.DialogResult = [System.Windows.Forms.DialogResult]::OK
            $form.Close()
        }
        catch {
            $status.Text = 'Setup could not complete.'
            $progress.Visible = $false
            $installButton.Enabled = $true
            $browseButton.Enabled = $true
            $cancelButton.Enabled = $true
            $pathInput.Enabled = $true
            [void][System.Windows.Forms.MessageBox]::Show(
                $form,
                "Broski was not installed.`r`n`r`n$($_.Exception.Message)",
                'Broski setup error',
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Error)
        }
    }.GetNewClosure())
    $form.Controls.Add($installButton)
    $form.AcceptButton = $installButton
    $form.CancelButton = $cancelButton

    $dialogResult = $form.ShowDialog()
    $result = $form.Tag
    $form.Dispose()
    if ($dialogResult -eq [System.Windows.Forms.DialogResult]::OK) {
        return $result
    }
    return $null
}

if ($Silent -and [string]::IsNullOrWhiteSpace($DestinationPath)) {
    throw 'Silent installation requires -DestinationPath.'
}

if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
    $DestinationPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Broski'
}

if ($Silent) {
    $result = Install-ChoppaAI -InstallDirectory $DestinationPath
}
else {
    $result = Show-SetupWizard -DefaultInstallDirectory ([IO.Path]::GetFullPath($DestinationPath))
    if ($null -eq $result) {
        return
    }
}

if (-not $Silent) {
    $choice = [System.Windows.Forms.MessageBox]::Show(
        'Start Broski now?',
        'Broski AI',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
        Start-Process -FilePath (Join-Path $result.InstallDirectory 'BroskiLauncher.exe') `
            -WorkingDirectory $result.InstallDirectory
    }
}

Write-Output "Broski AI 0.0.3 installed to $($result.InstallDirectory)"
