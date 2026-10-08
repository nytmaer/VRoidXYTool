param([switch]$Visible)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
dotnet build (Join-Path $repo 'src/VRoidXYTool.IL2CPP') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed; no test plugin deployed.' }
$appRoot = Join-Path $repo '.local/VRoidStudio'
if (Get-Process VRoidStudio -ErrorAction SilentlyContinue) { throw 'Close VRoid Studio before deploying the diagnostic.' }
$pluginRoot = Join-Path $appRoot 'BepInEx/plugins/VRoidXYTool.IL2CPP'
Copy-Item -LiteralPath (Join-Path $repo 'src/VRoidXYTool.IL2CPP/bin/Release/net6.0/VRoidXYTool.IL2CPP.dll') -Destination $pluginRoot -Force
Copy-Item -LiteralPath (Join-Path $repo 'src/VRoidXYTool.SyncCore/bin/Release/net6.0/VRoidXYTool.SyncCore.dll') -Destination $pluginRoot -Force
$windowStyle = if ($Visible) { 'Normal' } else { 'Hidden' }
$proc = Start-Process -FilePath (Join-Path $appRoot 'VRoidStudio.exe') -WorkingDirectory $appRoot -ArgumentList '-logFile document-test-player.log' -WindowStyle $windowStyle -PassThru
Write-Output ('Test PID: ' + $proc.Id)
$timer = [Diagnostics.Stopwatch]::StartNew()
$request = Join-Path $appRoot 'diagnostic/request.txt'
while (!$proc.HasExited -and $timer.Elapsed.TotalSeconds -lt 200) {
    if (Test-Path -LiteralPath $request) {
        $target = [IO.Path]::GetFullPath((Get-Content -LiteralPath $request -Raw))
        $allowed = [IO.Path]::GetFullPath((Join-Path $appRoot 'LinkTextureIL2CPP')) + [IO.Path]::DirectorySeparatorChar
        if (!$target.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected diagnostic write target' }
        Copy-Item -LiteralPath (Join-Path $appRoot 'diagnostic/candidate.png') -Destination $target -Force
        Remove-Item -LiteralPath $request
        Write-Output 'External PNG save performed.'
    }
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
}
if ($proc.HasExited) { Write-Output ('Exit code: ' + $proc.ExitCode) }
else { Write-Output 'Test timeout; process still running.' }
$result = Get-Content -LiteralPath (Join-Path $appRoot 'diagnostic/result.txt') -Raw -ErrorAction SilentlyContinue
Write-Output $result
Get-Content -LiteralPath (Join-Path $appRoot 'BepInEx/LogOutput.log') -Tail 35
if (!$proc.HasExited -or $proc.ExitCode -ne 0 -or $result -notmatch '^PASS:') { throw 'Document diagnostic did not complete successfully.' }
