# GtaHeistPlanner

A second-monitor planning and execution companion for the GTA Online Kortz Center heist.

GtaHeistPlanner helps track scope-out information, optimize loot and bag capacity,
plan infiltration routes, monitor guards and cameras, navigate the sewer route,
and control common actions using voice commands.

## Features

- Preparation / scope-out tracking
  - Loot locations and values
  - Buyer's Request items
  - Vault code
  - Persistent heist state

- Planning
  - Bag-capacity optimization
  - Recommended loot haul
  - Prep recommendations
  - Entry recommendations based on selected loot and location

- Infiltration
  - Exterior and sewer maps
  - Guard and camera tracking
  - Entry points and navigation markers
  - Sewer route assistance

- Heist
  - Interior maps
  - Loot tracking
  - Named guard and camera tracking
  - Camera stealth-limit monitoring

- Voice control
  - Local Whisper transcription
  - CPU support
  - Optional NVIDIA CUDA acceleration
  - Optional OpenAI transcription using your own API key
  - Deterministic command parsing

## Download

Pre-built Windows releases are available from the GitHub Releases page.

Download the latest:

    GtaHeistPlanner-<version>-win-x64.zip

Extract the archive and run:

    GtaHeistPlanner.exe

The application is self-contained and does not require a separate .NET
installation.

## Voice Recognition

### Local Whisper

Local Whisper is the default voice provider.

The model is downloaded from inside the application and is not bundled with the
release.

CPU recognition works without CUDA.

If a compatible NVIDIA CUDA runtime is available, Auto mode can use GPU
acceleration.

### OpenAI

OpenAI transcription is optional.

To use it, provide your own API key in Settings.

No API key is included with the application.

## Requirements

- Windows x64
- Microphone recommended for voice control
- Internet connection required only for:
  - downloading a Whisper model
  - OpenAI transcription, if selected

CUDA is optional.

## Building from Source

Requires the .NET 10 SDK.

From the repository root:

```powershell
dotnet restore
dotnet build
dotnet test
