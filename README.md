# Input Mapping Solution

A .NET 9 solution to log mouse and Wooting analog keyboard input using a WinForms app and the Wooting SDK.

## Prerequisites
- .NET 9 SDK: install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) and verify with `dotnet --version`.
- Windows 10/11 is required for the Raw Input API and Wooting SDK.
- Wooting Analog SDK: see `SDK-PLUGINS.txt` for download links and instructions.

## Solution Structure
- `InputMappingSolution.sln` – solution file.
 main
  - `ApplicationConfiguration.cs`
  - `Program.cs`
  - `InputToControllerMapper.csproj` (copies DLLs).
- `native_sdks/` – drop all native DLLs here (initially empty).
- `SDK-PLUGINS.txt` – SDK setup and troubleshooting guide.
- `README.md` – this file.

## Getting Started
1. Install the .NET 9 SDK.
2. Download the Wooting Analog SDK and place `wooting_analog_wrapper.dll` in `native_sdks`.
3. Build the solution with `dotnet build` or open it in Visual Studio.
4. Run the `InputToControllerMapper` project and watch the textbox for mouse and analog keyboard logs.
main

## Troubleshooting
If input events or the SDK fail to load, review `SDK-PLUGINS.txt` for hints about DLL placement and architecture.

## Note
The WinForms application requires the **Microsoft.WindowsDesktop.App** runtime, which is only available on Windows 10/11. You can build on Linux using `dotnet build -p:EnableWindowsTargeting=true`, but running the app must be done on Windows.
