# Broski Geekzit AI

Broski is a Windows desktop AI assistant with local memory, project and topic
organization, alarms, appointments, and device records.

## Download

Download `Broski-0.0.3-Windows-x64.zip` from the repository's GitHub Release,
extract all files, and run `broski-0.0.3.exe`. Keep `Broski-Payload.zip` beside
the installer while setup runs. The ZIP package includes the setup executable
and application payload, along with the setup runtime files; keep them together.
These release files are not stored in Git history.

## Build

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build .\ChoppaAI.csproj
.\installer\Build-Installer.ps1
```

The installer build produces a setup EXE and a separate application payload.
It packages both files into `installer\release\Broski-0.0.3-Windows-x64.zip`
for distribution. The setup EXE displays the wizard first, then reads the
payload ZIP beside it when installation begins.

## Local data

User memory and organizer data are stored under
`%LOCALAPPDATA%\ChoppaAI\AIData`. The checked-in `AIData` files are starter
templates; do not commit personal records or backups.
