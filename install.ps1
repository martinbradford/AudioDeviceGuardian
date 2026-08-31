# Audio Device Guardian - install + logon task registration
# Run in a normal (non-admin) PowerShell window after a `dotnet publish`:
#   powershell -NoProfile -ExecutionPolicy Bypass -File ".\install.ps1"
#
# Prereq (some antivirus only): if your AV flags the app, add a folder
# exclusion for the install path below. Some products flag unsigned .NET tray
# apps as a generic "Malware-gen" false positive; the framework-dependent
# build published to .\publish-fdd is far less prone to this than a
# self-contained single-file build.

$ErrorActionPreference = 'Stop'

# Resolve paths relative to this script so it works from any clone location.
$srcDir = Join-Path $PSScriptRoot 'publish-fdd'
$dest   = Join-Path $env:LOCALAPPDATA 'Programs\AudioDeviceGuardian'
$exe    = Join-Path $dest 'AudioDeviceGuardian.exe'
$user   = "$env:USERDOMAIN\$env:USERNAME"
$task   = 'AudioDeviceGuardian'

if (-not (Test-Path (Join-Path $srcDir 'AudioDeviceGuardian.exe'))) {
    throw "No published build at $srcDir. Run: dotnet publish -c Release --self-contained false -o `"$srcDir`""
}

Write-Host "Installing to $dest ..."
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $srcDir '*') -Destination $dest -Force
if (-not (Test-Path $exe)) { throw "Install failed: $exe not present after copy." }
Get-ChildItem $dest -Force | Select-Object Name, Length | Format-Table -AutoSize | Out-Host

Write-Host "Registering scheduled task '$task' ..."
Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
$action    = New-ScheduledTaskAction -Execute $exe
$trigger   = New-ScheduledTaskTrigger -AtLogOn -User $user
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                -StartWhenAvailable -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName $task -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings `
    -Description 'Re-asserts preferred default audio devices. Tray app, runs at logon.' | Out-Null
$t = Get-ScheduledTask -TaskName $task
Write-Host "  task state: $($t.State), runs: $($t.Actions[0].Execute)"

Write-Host "Launching for a live check ..."
Start-Process $exe
Start-Sleep -Seconds 7
$proc = Get-Process AudioDeviceGuardian -ErrorAction SilentlyContinue
if ($proc) { Write-Host "  RUNNING - pid $($proc.Id). Look for the tray icon." -ForegroundColor Green }
else       { Write-Host "  NOT running - check whether Norton removed it."     -ForegroundColor Yellow }
Write-Host "Done."
