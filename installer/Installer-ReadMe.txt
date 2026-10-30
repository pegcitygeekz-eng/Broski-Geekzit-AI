BROSKI AI WINDOWS INSTALLER

Extract all files from Broski-0.0.3-Windows-x64.zip, then run
broski-0.0.3.exe and follow the setup wizard. Keep all extracted files
together; the setup runtime and Broski-Payload.zip are required. The package
includes the desktop app, animation and blueprint assets, launcher, and
separate executable uninstaller.
Installation and uninstallation do not require PowerShell scripts or
administrator access.

The canonical distribution package is
installer\release\Broski-0.0.3-Windows-x64.zip. The EXE opens the setup wizard;
the companion payload ZIP contains the app files. Build-Installer.ps1 is
developer tooling and is not required by end users.

Setup creates Start menu, desktop, and sign-in startup shortcuts. If Windows
does not allow taskbar pinning, right-click the desktop shortcut and choose
"Pin to taskbar".

Ollama is not included. Install Ollama and a model separately to use local AI
chat.

To uninstall, use "Uninstall Broski" in the Start menu. The executable
uninstaller removes only files recorded by setup and leaves unrelated files
and user data outside the installation folder in place.
