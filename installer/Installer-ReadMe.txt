BROSKI AI WINDOWS INSTALLER

Run broski-0.0.3.exe and follow the setup wizard. The self-contained Windows
installer includes the desktop app, animation and blueprint assets, launcher,
and a separate executable uninstaller. Installation and uninstallation do not
require PowerShell scripts or administrator access.

The canonical build output is installer\release\broski-0.0.3.exe. The
Build-Installer.ps1 file is developer tooling used to build the executable; it
is not an installer and is not required by end users.

Setup creates Start menu, desktop, and sign-in startup shortcuts. If Windows
does not allow taskbar pinning, right-click the desktop shortcut and choose
"Pin to taskbar".

Ollama is not included. Install Ollama and a model separately to use local AI
chat.

To uninstall, use "Uninstall Broski" in the Start menu. The executable
uninstaller removes only files recorded by setup and leaves unrelated files
and user data outside the installation folder in place.
