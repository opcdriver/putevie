# AGENTS.md

## Cursor Cloud specific instructions

This is a **C# WPF desktop application** (`Putevie`) — a waybill ("путевой лист")
generator. It targets `net8.0-windows` with `<UseWPF>true</UseWPF>` and
`<OutputType>WinExe</OutputType>` (see `Putevie/Putevie.csproj`).

- **This project requires Windows.** WPF (`net8.0-windows`) cannot be built or run
  on the Linux Cursor Cloud VM even with the .NET SDK installed — the Windows
  Desktop targeting packs and XAML compilation are Windows-only. Do not attempt to
  build/run it here; use a Windows machine with the .NET 8 SDK / Visual Studio 2022.
- Static analysis and code review of `.cs`/`.xaml` files can still be done on Linux.
- Coding conventions are documented in `.cursorrules` (MVVM, async/await for heavy
  operations, Microsoft naming conventions, try/catch with logging).
