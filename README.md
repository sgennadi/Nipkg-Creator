# Nipkg Creator

Windows GUI application for generating NI Package Manager `nipkg.ini` from a selected `feeds` folder.

Select `C:\NI\LabVIEW 2026 Q3 x64\_Src\feeds` to write `C:\NI\LabVIEW 2026 Q3 x64\nipkg.ini`.

Features: UNC paths, directory browser, preview, collision checks, overwrite confirmation, atomic save, UTF-8 without BOM.

## Download EXE
Open **Actions** -> **Build Windows EXE** -> latest successful run -> **Artifacts** -> **NipkgCreator-win-x64**.

## Build locally
Requires .NET 10 SDK and Windows for GUI. Run:
`dotnet publish src/NipkgCreator/NipkgCreator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish`

## Output destination

By default, the app saves `nipkg.ini` beside `_Src`. You can select another destination using the output **Browse...** button, edit the output path directly, or click **Auto** to restore automatic detection.
