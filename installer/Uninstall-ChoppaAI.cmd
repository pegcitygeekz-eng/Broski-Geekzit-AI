@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Uninstall-ChoppaAI.ps1"
exit /b %errorlevel%
