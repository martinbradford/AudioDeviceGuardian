# Audio Device Guardian

A tray-only Windows app that watches the default playback/recording (and
communications-role) audio devices, and re-asserts your preferred ones
whenever Windows changes them — without a visible window, and without
fighting you when you deliberately want to switch devices.

## Build

Requires the .NET SDK matching `net10.0-windows` in the `.csproj` (adjust the
`TargetFramework` if you're on an earlier SDK — this only needs Windows
Forms + the NuGet package below, nothing version-specific).

```
dotnet restore
dotnet build -c Release
```

To publish a single self-contained exe (no separate .NET install needed on
the target machine):

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The output lands in `bin\Release\net10.0-windows\win-x64\publish\AudioDeviceGuardian.exe`.

**Note:** this depends on `AudioSwitcher.AudioApi.CoreAudio`, which wraps the
undocumented `IPolicyConfig` COM interface Windows uses internally — there's
no public Core Audio API for setting the default device. The package ships
only .NET Framework assemblies, so restoring it against a modern `net*-windows`
target raises `NU1701`; that's expected and harmless here (it's a Windows-only
COM-interop wrapper that runs fine on modern .NET on Windows), and the
`.csproj` suppresses it deliberately.

`AudioDeviceChanged` is exposed as a bare `IObservable<T>`, so we subscribe
with a tiny `IObserver` adapter rather than pulling in `System.Reactive` for
its `Subscribe(Action<T>)` convenience overload.

## Installing

For personal use on a machine that already has the .NET runtime, a
**framework-dependent** build is smaller and much less likely to upset
antivirus (see below) than the self-contained single-file exe:

```
dotnet publish -c Release --self-contained false -o publish-fdd
```

Then run [`install.ps1`](install.ps1) from a normal (non-admin) PowerShell
window. It copies the build to `%LocalAppData%\Programs\AudioDeviceGuardian`,
registers a "run at logon" scheduled task, and launches it:

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

## Antivirus false positives

Because this is an **unsigned** background app that **auto-starts** and reaches
into the system's default audio devices, some antivirus products (Norton /
Symantec among them) may flag it with a *generic* heuristic verdict such as
`Win64:Malware-gen`. That's a false positive — there's no code-signing
certificate to vouch for it and the behaviour pattern looks suspicious to an
ML scanner — but it can silently quarantine the exe, kill the process, and even
delete the scheduled task.

If that happens:

- Prefer the **framework-dependent** build over the self-contained single-file
  one — the bundled-runtime self-extraction in single-file builds is a big
  heuristic trigger.
- Add a **folder exclusion** for `%LocalAppData%\Programs\AudioDeviceGuardian`
  in your AV (for real-time / behavioural protection, not just scans).
- If your AV already quarantined and blocked the exact exe path, clear that
  block (or restore the file) after adding the exclusion.
- The proper long-term fix is to **code-sign** the executable, which removes
  the "unsigned" strike entirely.

## First run

1. Run the exe once. It'll create `%AppData%\AudioDeviceGuardian\config.json`
   with everything blank (so it won't change anything yet).
2. Manually set your input/output devices the way you want them in Windows
   Sound settings.
3. Right-click the tray icon → **Capture current devices as preferred**.
   That's now the baseline it'll enforce.

From then on, whenever you want to *temporarily* use different devices,
either:
- Right-click → **Suspend for...** → 15 min / 1 hour / until resumed, or
- Switch devices, then **Capture current devices as preferred** again if you
  want the new setup to stick permanently.

Config is plain JSON, so you can also hand-edit device name substrings via
**Edit config file...** if the auto-capture doesn't quite match what you want
(e.g. you want to match "Scarlett" rather than the full endpoint name).

## Run at logon

`install.ps1` (above) already registers this task for you. If you'd rather set
it up by hand, Task Scheduler is more reliable than the Startup folder for
something you want to be quietly always-on:

1. Open **Task Scheduler** → **Create Task...** (not "Basic Task", so you get
   the extra options below).
2. **General** tab: name it, tick **Run only when user is logged on**
   (it's a tray app, it needs a desktop session).
3. **Triggers** tab: **New...** → **At log on** → specific user (you).
4. **Actions** tab: **New...** → point at the published `.exe`.
5. **Conditions** tab: untick "Start the task only if the computer is on AC
   power" if this is a laptop.

## Known limitations

- Matches devices by **name substring**, not stable device ID — deliberate,
  since IDs tend to change across reboots, driver updates, and Bluetooth
  reconnects more than display names do. If two devices share a substring
  (e.g. two "Realtek" entries) it'll pick the first match, so keep the
  configured strings as specific as you need.
- If your *actual* default device is unplugged/disabled, there's nothing to
  fall back to — it just won't find a match and leaves Windows' own choice
  alone.
