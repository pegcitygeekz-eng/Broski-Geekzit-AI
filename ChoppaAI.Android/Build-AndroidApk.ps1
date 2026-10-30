param(
    [string] $AndroidSdkDirectory = $env:ANDROID_HOME,
    [switch] $InstallOnDevice
)

$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$projectFile = Join-Path $projectDirectory 'ChoppaAI.Android.csproj'
if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) {
    $AndroidSdkDirectory = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
}
if (-not (Test-Path -LiteralPath $AndroidSdkDirectory -PathType Container)) {
    throw "Android SDK not found at '$AndroidSdkDirectory'. Set ANDROID_HOME or pass -AndroidSdkDirectory."
}

$outputDirectory = Join-Path $projectDirectory 'bin\Release\net8.0-android'
& dotnet build $projectFile `
    --configuration Release `
    --framework net8.0-android `
    "-p:AndroidSdkDirectory=$AndroidSdkDirectory" `
    '-p:AndroidPackageFormat=apk'
if ($LASTEXITCODE -ne 0) {
    throw "Android APK build failed with exit code $LASTEXITCODE."
}

$signedApk = Join-Path $outputDirectory 'com.choppai.mobile-Signed.apk'
if (-not (Test-Path -LiteralPath $signedApk -PathType Leaf)) {
    throw "The signed APK was not produced: $signedApk"
}

$releaseDirectory = Join-Path $projectDirectory 'release'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$releaseApk = Join-Path $releaseDirectory 'ChoppaAI.apk'
Copy-Item -LiteralPath $signedApk -Destination $releaseApk -Force

if ($InstallOnDevice) {
    $adb = Join-Path $AndroidSdkDirectory 'platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $adb -PathType Leaf)) {
        throw "Android Debug Bridge was not found: $adb"
    }
    & $adb install -r $releaseApk
    if ($LASTEXITCODE -ne 0) {
        throw "Installing the APK on the connected device failed with exit code $LASTEXITCODE."
    }
}

Get-Item -LiteralPath $releaseApk |
    Select-Object FullName, @{Name = 'SizeMB'; Expression = { [math]::Round($_.Length / 1MB, 2) }}
