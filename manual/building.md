# Building from source

You only need this to work on Costas itself. To use it, [install the release](install.md).

Costas is C# on .NET 10 with Avalonia for the UI. Decoding is done by WSJT-X's `jt9` and radio control by Hamlib's
`rigctld`, both run as child processes. The design documents are in
[`docs/`](https://github.com/somedudenamedtony/costas/tree/main/docs) in the repository.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- `jt9` and Hamlib. Fetch them into `third_party/` with:
    - Windows: `pwsh third_party/fetch.ps1` (needs 7-Zip on `PATH` to unpack the WSJT-X installer)
    - Linux: `third_party/fetch.sh` (uses the distribution packages `wsjtx` and `libhamlib-utils`)

## Build, test, run

```
dotnet build
dotnet test
dotnet run --project src/Ft8Client.App
```

Run with no radio, replaying the sample recordings, with a simulated station that answers your calls:

```
dotnet run --project src/Ft8Client.App -- --simulate samples/ft8 --simulate-partner
```

Simulation never keys a radio, never uploads spots and does not write the decode journal or WAV files. The window
title says *simulation*. Add `--home <folder>` to use a separate data folder, so testing does not touch your real
log and settings.

Decode one recording through `jt9`:

```
dotnet run --project tools/Ft8Client.DecodeCli -- samples/ft8/210703_133430.wav
```

## Installer

`scripts/build-installer.ps1` builds `artifacts/Costas-Setup-<version>.exe` with `jt9` and Hamlib bundled. CI builds
it on every push, and every green build of `main` is published as a GitHub Release.

## This manual

The manual is in `manual/`, built with [MkDocs](https://www.mkdocs.org/) and the Material theme, and published to
GitHub Pages by `.github/workflows/manual.yml` on every push to `main`. To preview it locally:

```
pip install mkdocs-material
mkdocs serve
```

Screenshots in `manual/images/` were taken from the app running in simulation under a virtual display
(`Xvfb`), so they show real screens with recorded band activity.
