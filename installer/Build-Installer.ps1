param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$installerFolder = $PSScriptRoot
$projectDirectory = Get-Item (Join-Path $installerFolder '..') | Select-Object -ExpandProperty FullName
$projectFile = Join-Path $projectDirectory 'ChoppaAI.csproj'
$launcherProject = Join-Path $projectDirectory 'Launcher\BroskiLauncher.csproj'
$bootstrapperProject = Join-Path $installerFolder 'SetupBootstrapper\SetupBootstrapper.csproj'
$uninstallerProject = Join-Path $installerFolder 'Uninstaller\BroskiUninstaller.csproj'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $installerFolder 'release'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

$buildId = [guid]::NewGuid().ToString('N')
$workDirectory = Join-Path ([IO.Path]::GetTempPath()) "Broski-build-installer-$buildId"
$publishDirectory = Join-Path $workDirectory 'publish'
$stageDirectory = Join-Path $workDirectory 'payload'
$bootstrapperOutput = Join-Path $workDirectory 'setup-exe'
$uninstallerOutput = Join-Path $workDirectory 'uninstaller'
$payloadArchive = Join-Path $workDirectory 'ChoppaAI-Payload.zip'
$installerExecutable = Join-Path $OutputDirectory 'broski-0.0.3.exe'
$distributionDirectory = Join-Path $workDirectory 'distribution'
$distributionArchive = Join-Path $OutputDirectory 'Broski-0.0.3-Windows-x64.zip'
$distributionChecksum = "$distributionArchive.sha256"
$payloadReleaseFile = Join-Path $OutputDirectory 'Broski-Payload.zip'

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "The project file was not found: $projectFile"
}
if (-not (Test-Path -LiteralPath $launcherProject -PathType Leaf)) {
    throw "The launcher project was not found: $launcherProject"
}
if (-not (Test-Path -LiteralPath $uninstallerProject -PathType Leaf)) {
    throw "The Broski uninstaller project was not found: $uninstallerProject"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null

try {
    & dotnet publish $projectFile `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDirectory `
        '-p:PublishSingleFile=false'
    if ($LASTEXITCODE -ne 0) {
        throw "The desktop application publish failed with exit code $LASTEXITCODE."
    }

    $launcherOutput = Join-Path $workDirectory 'launcher'
    & dotnet publish $launcherProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $launcherOutput `
        '-p:PublishSingleFile=true'
    if ($LASTEXITCODE -ne 0) {
        throw "The Broski launcher publish failed with exit code $LASTEXITCODE."
    }

    & dotnet publish $uninstallerProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $uninstallerOutput `
        '-p:PublishSingleFile=true'
    if ($LASTEXITCODE -ne 0) {
        throw "The Broski uninstaller publish failed with exit code $LASTEXITCODE."
    }

    $launcherExecutable = Join-Path $launcherOutput 'BroskiLauncher.exe'
    if (-not (Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
        throw "The published Broski launcher was not found in $launcherOutput."
    }
    $uninstallerExecutable = Join-Path $uninstallerOutput 'BroskiUninstaller.exe'
    if (-not (Test-Path -LiteralPath $uninstallerExecutable -PathType Leaf)) {
        throw "The published Broski uninstaller was not found in $uninstallerOutput."
    }

    $appExe = Join-Path $publishDirectory 'Broski.exe'
    if (-not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
        throw "The published desktop app was not found in $publishDirectory."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'animations') -PathType Container)) {
        throw "The published animation folder was not found in $publishDirectory."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'blueprints') -PathType Container)) {
        throw "The published blueprint folder was not found in $publishDirectory."
    }

    Copy-Item -LiteralPath $publishDirectory -Destination $stageDirectory -Recurse
    Copy-Item -LiteralPath $launcherExecutable -Destination (Join-Path $stageDirectory 'BroskiLauncher.exe')
    Copy-Item -LiteralPath $uninstallerExecutable -Destination (Join-Path $stageDirectory 'BroskiUninstaller.exe')
    foreach ($payloadFile in @('Broski.exe', 'BroskiLauncher.exe', 'BroskiUninstaller.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory $payloadFile) -PathType Leaf)) {
            throw "A required file is missing from the installer payload: $payloadFile"
        }
    }
    $scriptPayload = Get-ChildItem -LiteralPath $stageDirectory -File -Recurse |
        Where-Object { $_.Extension -in @('.ps1', '.cmd') }
    if ($scriptPayload) {
        throw "The user-facing installer payload must contain executables only, not scripts: $($scriptPayload.Name -join ', ')"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $stageDirectory,
        $payloadArchive,
        [IO.Compression.CompressionLevel]::Optimal,
        $false)

    & dotnet publish $bootstrapperProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $bootstrapperOutput
    if ($LASTEXITCODE -ne 0) {
        throw "The installer executable build failed with exit code $LASTEXITCODE."
    }

    $builtExecutable = Join-Path $bootstrapperOutput 'broski-0.0.3.exe'

    if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
        throw "The installer executable was not found after publishing: $bootstrapperOutput"
    }
    Get-ChildItem -LiteralPath $bootstrapperOutput -Force |
        Copy-Item -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath $payloadArchive -Destination $payloadReleaseFile -Force

    New-Item -ItemType Directory -Path $distributionDirectory | Out-Null
    Get-ChildItem -LiteralPath $bootstrapperOutput -Force |
        Copy-Item -Destination $distributionDirectory -Recurse -Force
    Copy-Item -LiteralPath $payloadReleaseFile -Destination $distributionDirectory
    if (Test-Path -LiteralPath $distributionArchive -PathType Leaf) {
        Remove-Item -LiteralPath $distributionArchive -Force
    }
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $distributionDirectory,
        $distributionArchive,
        [IO.Compression.CompressionLevel]::Optimal,
        $false)
    $distributionHash = (Get-FileHash -LiteralPath $distributionArchive -Algorithm SHA256).Hash
    [IO.File]::WriteAllText(
        $distributionChecksum,
        "$distributionHash  $(Split-Path -Leaf $distributionArchive)$([Environment]::NewLine)",
        [Text.Encoding]::ASCII)

    [pscustomobject]@{
        Installer = $installerExecutable
        SizeMB    = [math]::Round((Get-Item -LiteralPath $installerExecutable).Length / 1MB, 1)
        Package   = $distributionArchive
        PackageMB = [math]::Round((Get-Item -LiteralPath $distributionArchive).Length / 1MB, 1)
    }
}
finally {
    if (Test-Path -LiteralPath $workDirectory -PathType Container) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}
