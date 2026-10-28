# Nipkg Creator

A lightweight Windows x64 utility for creating an NI Package Manager repository configuration (`nipkg.ini`) from a package's `feeds` directory. Built in **C# / .NET 10 / WinForms**.

## Download

**[Latest stable release](https://github.com/sgennadi/Nipkg-Creator/releases/latest)** — download `NipkgCreator.exe`. It is a standalone, single-file, self-contained EXE; installing .NET is not required.

## Features

- Modern blue and green GUI with a custom multi-resolution application icon embedded in the EXE.
- Choose **any** software/package installation root; no product name or version is hard-coded.
- Also accepts a direct selection of `_Src` or `feeds`.
- Looks for `_Src\feeds` beneath the root and generates repo sections from its immediate subdirectories.
- Automatic output path `<selected package root>\nipkg.ini`; optional custom destination with **Browse** and **Auto** reset.
- Syntax-colored configuration preview and **Copy**.
- Network / UNC paths and high-DPI Windows displays.
- Overwrite confirmation, UTF-8 without BOM, atomic replacement of existing files.

## Example folder structure

```text
AnyPackage/
  _Src/
    feeds/
      ni-package-manager/
      other.repository/
  nipkg.ini          (created here)
```

Selecting `AnyPackage`, `AnyPackage\_Src` or `AnyPackage\_Src\feeds` automatically writes `AnyPackage\nipkg.ini`, unless you choose a different output folder.

## Building locally

Requirements: Windows and .NET 10 SDK.

```powershell
dotnet publish src/NipkgCreator/NipkgCreator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish
```

The project automatically runs `tools/GenerateAppIcon.ps1` to generate the icon if it is missing.

## Automated builds and releases

- `.github/workflows/build.yml` uploads the current Windows EXE as an Actions artifact.
- `.github/workflows/release.yml` builds, validates the icon and publishes versioned Windows EXE assets under [Releases](https://github.com/sgennadi/Nipkg-Creator/releases).

License: no explicit license has been selected yet.
