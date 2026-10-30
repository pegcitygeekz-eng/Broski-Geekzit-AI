param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$installerFolder = $PSScriptRoot
$projectDirectory = Get-Item (Join-Path $installerFolder '..') | Select-Object -ExpandProperty FullName
$projectFile = Join-Path $projectDirectory 'ChoppaAI.csproj'
$launcherProject = Join-Path $projectDirectory 'Launcher\BroskiLauncher.csproj'
$bootstrapperProject = Join-Path $installerFolder 'SetupBootstrapper\SetupBootstrapper.csproj'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $installerFolder 'release'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

$buildId = [guid]::NewGuid().ToString('N')
$workDirectory = Join-Path ([IO.Path]::GetTempPath()) "Broski-build-installer-$buildId"
$publishDirectory = Join-Path $workDirectory 'publish'
$stageDirectory = Join-Path $workDirectory 'payload'
$bootstrapperOutput = Join-Path $workDirectory 'setup-exe'
$payloadArchive = Join-Path $workDirectory 'ChoppaAI-Payload.zip'
$installerExecutable = Join-Path $OutputDirectory 'broski-0.0.3.exe'
$installScript = Join-Path $PSScriptRoot 'Install-ChoppaAI.ps1'
$uninstallScript = Join-Path $PSScriptRoot 'Uninstall-ChoppaAI.ps1'
$uninstallCommand = Join-Path $PSScriptRoot 'Uninstall-ChoppaAI.cmd'

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "The project file was not found: $projectFile"
}
if (-not (Test-Path -LiteralPath $launcherProject -PathType Leaf)) {
    throw "The launcher project was not found: $launcherProject"
}
foreach ($sourceFile in @($installScript, $uninstallScript, $uninstallCommand)) {
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "An installer source file was not found: $sourceFile"
    }
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
    $launcherExecutable = Join-Path $launcherOutput 'BroskiLauncher.exe'
    if (-not (Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
        throw "The published Broski launcher was not found in $launcherOutput."
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
    Copy-Item -LiteralPath $uninstallScript -Destination $stageDirectory -Force
    Copy-Item -LiteralPath $uninstallCommand -Destination $stageDirectory -Force
    foreach ($payloadFile in @('Broski.exe', 'BroskiLauncher.exe', 'Uninstall-ChoppaAI.ps1', 'Uninstall-ChoppaAI.cmd')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory $payloadFile) -PathType Leaf)) {
            throw "A required file is missing from the installer payload: $payloadFile"
        }
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
        --output $bootstrapperOutput `
        "-p:InstallerScript=$installScript" `
        "-p:PayloadArchive=$payloadArchive"
    if ($LASTEXITCODE -ne 0) {
        throw "The installer executable build failed with exit code $LASTEXITCODE."
    }

    $builtExecutable = Join-Path $bootstrapperOutput 'broski-0.0.3.exe'

    if (-not (Test-Path -LiteralPath $builtExecutable -PathType Leaf)) {
        throw "The installer executable was not found after publishing: $bootstrapperOutput"
    }
    Copy-Item -LiteralPath $builtExecutable -Destination $installerExecutable -Force

    [pscustomobject]@{
        Installer = $installerExecutable
        SizeMB    = [math]::Round((Get-Item -LiteralPath $installerExecutable).Length / 1MB, 1)
    }
}
finally {
    if (Test-Path -LiteralPath $workDirectory -PathType Container) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}
