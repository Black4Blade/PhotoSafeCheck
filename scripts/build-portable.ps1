param(
  [ValidateSet('win-x64','linux-x64','osx-x64','osx-arm64')]
  [string]$Rid = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$published = Join-Path $root "out/publish-$Rid"
$bundle = Join-Path $root "out/PhotoSafeCheck-$Rid-portable"
if (Test-Path $published) { throw "Refusing to overwrite existing publish directory: $published. Remove it manually or use a fresh clone." }
if (Test-Path $bundle) { throw "Refusing to overwrite existing bundle: $bundle. Remove it manually or use a fresh clone." }
dotnet publish (Join-Path $root 'PhotoSafeCheck/PhotoSafeCheck.csproj') -c Release -r $Rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $published
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
python (Join-Path $root 'scripts/package_preview.py') --publish $published --dest $bundle --rid $Rid
if ($LASTEXITCODE -ne 0) { throw 'packaging failed' }
Write-Host "Portable bundle ready: $bundle"
