$ErrorActionPreference = 'Stop'
$installDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
$manifestPath = Join-Path $installDirectory 'InstallManifest.json'
$uninstallKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ChoppaAI'

Add-Type -AssemblyName System.Windows.Forms

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "The uninstall manifest is missing. To avoid deleting unrelated files, nothing was removed: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestInstallDirectory = [IO.Path]::GetFullPath($manifest.InstallDirectory).TrimEnd([char]92)
if (($manifest.ProductId -ne 'ChoppaAI.DesktopPet') -or
    ($manifestInstallDirectory -ne $installDirectory.TrimEnd([char]92))) {
    throw 'This folder is not a recognized Broski AI installation. No files were removed.'
}

$choice = [System.Windows.Forms.MessageBox]::Show(
    "Uninstall Broski AI from:`r`n$installDirectory`r`n`r`nUnrelated files will be left in place. User data outside the installation folder will be kept.",
    'Uninstall Broski AI',
    [System.Windows.Forms.MessageBoxButtons]::YesNo,
    [System.Windows.Forms.MessageBoxIcon]::Question)
if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) {
    return
}

$appPaths = @(
    (Join-Path $installDirectory 'Broski.exe'),
    (Join-Path $installDirectory 'BroskiLauncher.exe'),
    (Join-Path $installDirectory 'ChoppaAI.exe')
)
foreach ($process in Get-Process -Name Broski, BroskiLauncher, ChoppaAI -ErrorAction SilentlyContinue) {
    try {
        if ($process.Path -and $appPaths -contains [IO.Path]::GetFullPath($process.Path)) {
            Stop-Process -Id $process.Id -Force
        }
    }
    catch [System.ComponentModel.Win32Exception] {
        Write-Warning "Could not check the running Broski process: $($_.Exception.Message)"
    }
}

foreach ($shortcut in @($manifest.Shortcuts)) {
    if (-not [string]::IsNullOrWhiteSpace($shortcut) -and (Test-Path -LiteralPath $shortcut -PathType Leaf)) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}

if (Test-Path -LiteralPath $uninstallKeyPath) {
    $registeredPath = (Get-ItemProperty -LiteralPath $uninstallKeyPath `
        -Name InstallLocation -ErrorAction SilentlyContinue).InstallLocation
    if ($registeredPath -and [IO.Path]::GetFullPath($registeredPath).TrimEnd('\') -eq $installDirectory.TrimEnd('\')) {
        Remove-Item -LiteralPath $uninstallKeyPath -Recurse -Force
    }
}

$root = $installDirectory.TrimEnd([char]92) + '\'
$ownedFiles = @($manifest.Files) + @('InstallManifest.json') | Select-Object -Unique
foreach ($relativePath in $ownedFiles) {
    if ([IO.Path]::IsPathRooted($relativePath)) {
        throw "The uninstall manifest contains a rooted path: $relativePath"
    }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relativePath))
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The uninstall manifest points outside the installation folder: $relativePath"
    }
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}

foreach ($relativePath in (@($manifest.Directories) | Sort-Object { $_.Length } -Descending)) {
    if ([IO.Path]::IsPathRooted($relativePath)) {
        throw "The uninstall manifest contains a rooted directory: $relativePath"
    }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relativePath))
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The uninstall manifest points outside the installation folder: $relativePath"
    }
    if (Test-Path -LiteralPath $path -PathType Container) {
        $remainingEntries = @(Get-ChildItem -LiteralPath $path -Force)
        if ($remainingEntries.Count -eq 0) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

$remainingInstallEntries = @(Get-ChildItem -LiteralPath $installDirectory -Force)
if ((Test-Path -LiteralPath $installDirectory -PathType Container) -and $remainingInstallEntries.Count -eq 0) {
    Remove-Item -LiteralPath $installDirectory -Force
}
[void][System.Windows.Forms.MessageBox]::Show(
    'Broski AI has been removed. User data and unrelated files outside the installation folder were left in place.',
    'Broski uninstall complete',
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information)
