# Validation

Run the desktop checks from the repository:

    dotnet run --project tests/Core.Tests.csproj
    dotnet run --project tools/PcSetup/PcSetup.csproj -- --self-test

Core tests link production button, queue, encryption, and Spotify service code. Mock boundaries simulate HTTP, clock, storage, and JSON. Coverage includes debounce, volume repeat, stale commands, authenticated encryption, playback restrictions, token rotation, rate limits, and slow responses.

PC checks cover configuration validation, source escaping, authorization revision stability, and callback field parsing.

Compile the firmware using tools/Build.ps1 after PC setup. Dependencies remain pinned to the installed runtime, including System.IO.FileSystem 1.1.94.

Physical verification is listed in ACCEPTANCE.md. Compilation and mock tests do not verify device TLS, Wi-Fi, physical pins, or Spotify account permissions.
