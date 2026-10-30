# Broski Geekzit AI

Broski is a Windows desktop AI assistant with local memory, project and topic
organization, alarms, appointments, and device records.

## Download

Download `Broski-0.0.3-Windows-x64.zip` from the repository's GitHub Release,
extract it, and run the installer. The installer is a separate release asset
and is intentionally not stored in Git history.

## Build

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build .\ChoppaAI.csproj
.\installer\Build-Installer.ps1
```

The installer build produces a self-contained Windows x64 setup executable.

## Local data

User memory and organizer data are stored under
`%LOCALAPPDATA%\ChoppaAI\AIData`. The checked-in `AIData` files are starter
templates; do not commit personal records or backups.

