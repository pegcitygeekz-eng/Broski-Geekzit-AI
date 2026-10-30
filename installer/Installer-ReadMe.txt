BROSKI AI WINDOWS INSTALLER

Run broski-0.0.2.exe and follow the setup wizard. Setup installs the Windows
desktop app for your account and creates Start menu and desktop shortcuts.
The single installer includes the app, animation and blueprint assets, setup
wizard, and uninstall scripts. No separate PowerShell or CMD files are needed
to install or uninstall.

The canonical release is installer\release\broski-0.0.2.exe. The PowerShell,
CMD, and bootstrapper project files elsewhere in installer are build sources,
not additional release packages.

Windows may prevent installers from pinning apps to the taskbar. If setup cannot
pin Choppa AI automatically, right-click the desktop shortcut and choose
"Pin to taskbar".

Ollama is not included. Install Ollama and a model separately to use local AI
chat. The installer does not require administrator access.

To uninstall, use "Uninstall Choppa AI" in the Start menu or run
Uninstall-ChoppaAI.cmd in the installation folder. Uninstall removes only files
recorded by setup and leaves unrelated files in place.
