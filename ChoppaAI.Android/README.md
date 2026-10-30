# Choppa AI for Android

The Android companion app sends chats to Ollama running on your PC. It does not
download or run models on the phone.

## Build the APK

Install the .NET Android workload and Android SDK, then run this script from
PowerShell or Android Studio's integrated terminal:

```powershell
.\Build-AndroidApk.ps1
```

The signed APK is copied to `release\ChoppaAI.apk`. Use `-InstallOnDevice` to
install it on a connected Android device. The app is a .NET Android project:
Android Studio does not build its C# source with Gradle, so the script invokes
the .NET Android build tools. Android Studio can still be used to inspect and
install the generated APK.

## Connect to Ollama

- The default address, `http://10.0.2.2:11434`, is for the Android emulator.
- On a physical phone, tap **PC connection** and enter the PC's LAN address,
  such as `http://192.168.1.20:11434`. Keep the phone and PC on the same trusted
  network.
- Ollama normally listens only on localhost. Configure it to listen on the
  local network (for example, `OLLAMA_HOST=0.0.0.0:11434`), restart Ollama, and
  allow port 11434 through the PC firewall for private networks only.
- Install at least one model on the PC. The app loads available models from
  Ollama and remembers the chosen address and model on the phone.

The app uses plain HTTP for local-network Ollama connections. Do not expose the
Ollama port to the public internet.
