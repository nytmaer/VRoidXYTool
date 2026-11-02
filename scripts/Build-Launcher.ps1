param(
    [string]$RuntimeRoot = '.local/VRoidStudio-2.16',
    [string]$OutputFolder = '.local/Launcher/output'
)
$ErrorActionPreference = 'Stop'
$launcherRepo = Split-Path $PSScriptRoot -Parent
Push-Location $launcherRepo
try {
    $launcherInterop = Join-Path (Resolve-Path $RuntimeRoot).Path 'BepInEx/interop'
    if (!(Test-Path -LiteralPath (Join-Path $launcherInterop 'VRoidStudio.dll'))) { throw 'Generate interop in an isolated supported VRoid application first. See docs/IL2CPP_BOOTSTRAP.md.' }
    $launcherBuild = Join-Path $launcherRepo ('.local/Launcher/build-' + [Guid]::NewGuid().ToString('N'))
    $launcherPayload = Join-Path $launcherBuild 'payload'
    $launcherCompanion = Join-Path $launcherPayload 'VRoidXYTool/Companion'
    $launcherPlugin = Join-Path $launcherPayload 'BepInEx/plugins/VRoidXYTool.IL2CPP'
    $launcherDocs = Join-Path $launcherPayload 'VRoidXYTool/Docs'
    New-Item -ItemType Directory -Path $launcherPlugin,$launcherDocs -Force | Out-Null
    dotnet build src/VRoidXYTool.IL2CPP -c Release "-p:InteropRoot=$launcherInterop" -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    dotnet publish src/VRoidXYTool.Companion -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -o $launcherCompanion -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Companion publish failed.' }
    foreach ($launcherDll in @('VRoidXYTool.IL2CPP.dll','VRoidXYTool.SyncCore.dll','VRoidXYTool.CompanionCore.dll')) {
        Copy-Item -LiteralPath (Join-Path "src/VRoidXYTool.IL2CPP/bin/Release/net6.0" $launcherDll) -Destination $launcherPlugin
    }
    foreach ($launcherDoc in @('README.md','LICENSE','docs/LAUNCHER_GUIDE.md','docs/VROID_216_COMPATIBILITY.md','docs/COMPANION.md','docs/CHARACTER_PROJECTS.md','docs/WORKSPACE_TOOLS.md','docs/RECORDING.md')) {
        Copy-Item -LiteralPath $launcherDoc -Destination $launcherDocs
    }
    $launcherHashes = [ordered]@{}
    foreach ($launcherFile in Get-ChildItem -LiteralPath $launcherPayload -File -Recurse) {
        $launcherRelative = [IO.Path]::GetRelativePath($launcherPayload,$launcherFile.FullName).Replace('\','/')
        if ($launcherRelative -match '(?i)(\.pdb$|\.vroid$|\.vrm$|bridge-state\.json$|BepInEx/(interop|config)/|(^|/)(winhttp\.dll|GameAssembly\.dll|UnityPlayer\.dll|VRoidStudio\.exe)$)') { throw "Excluded package file: $launcherRelative" }
        $launcherHashes[$launcherRelative] = (Get-FileHash -LiteralPath $launcherFile.FullName -Algorithm SHA256).Hash
    }
    $launcherHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $launcherPayload 'package-hashes.json') -Encoding utf8
    $launcherZip = Join-Path $launcherBuild 'payload.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($launcherPayload,$launcherZip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $launcherOutput = [IO.Path]::GetFullPath($OutputFolder)
    dotnet publish src/VRoidXYTool.Launcher -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:NuGetAudit=false -p:UseSharedCompilation=false "-p:PayloadPath=$launcherZip" -m:1 -o $launcherOutput -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed.' }
    Copy-Item -LiteralPath docs/LAUNCHER_GUIDE.md -Destination (Join-Path $launcherOutput 'START-HERE.md')
    Copy-Item -LiteralPath docs/VROID_216_COMPATIBILITY.md -Destination $launcherOutput
    $launcherSums = foreach ($launcherFile in Get-ChildItem -LiteralPath $launcherOutput -File | Where-Object Name -ne 'SHA256SUMS.txt') {
        (Get-FileHash -LiteralPath $launcherFile.FullName -Algorithm SHA256).Hash + '  ' + $launcherFile.Name
    }
    Set-Content -LiteralPath (Join-Path $launcherOutput 'SHA256SUMS.txt') -Value $launcherSums
    Write-Output "Launcher ready: $launcherOutput"
    Write-Output "Payload for integration checks: $launcherZip"
}
finally { Pop-Location }
