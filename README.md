# Formicarium

A self-regulating habitat for *Tetramorium immigrans* — one square acrylic column with a heated
Ytong nest core, passive hydration, three always-open risers, and a closed-loop colour experiment
at the mouth of each riser. Firmware in C# on .NET nanoFramework; dashboard in Blazor.

**Nothing has been ordered yet.** The repo is complete on paper and verifiable without hardware:
the firmware compiles to a deployable image, the control logic is unit-tested on the desktop, and
the dashboard runs the real firmware policy against a colony simulator.

## Layout

```
docs/
  build-guide.html      single-file build manual: 3D blueprint, drawings, 33-step sequence
  bom.md                parts, sourcing, and every part that was cut with the reason
  gpio-map.md           canonical pin assignment — the single source of truth
firmware/
  Formicarium.Controller/
    Core/               hardware-free control logic. Shared verbatim with the tests.
    Hardware/           the only code that knows what a GPIO is
    Net/                HTTP API + a small self-hosted fallback page
  Formicarium.Controller.Tests/   55 tests, desktop, no ESP32 required
dashboard/
  Formicarium.Dashboard/          Blazor Server + SQLite telemetry log
```

## The one architectural idea

`Core/` contains every control decision and touches no hardware type. `Hardware/` is a thin
adapter behind two interfaces. That split is what makes the interesting half of the firmware
testable — and it matters here because **every failure worth worrying about in this system is a
slow one**. A cooked nest, a flooded nest, a colony cut off from food: all of them take hours to
days to develop. Being able to fast-forward a simulated week in milliseconds is the difference
between testing the safety rules and hoping about them.

`Core/` is compiled three times from the same files:

| Consumer | How | Why |
|---|---|---|
| `Formicarium.Controller.nfproj` | direct `<Compile Include>` | ships to the ESP32 |
| `Formicarium.Controller.Tests` | linked sources, xUnit on .NET 10 | proves the logic |
| `Formicarium.Dashboard` | linked sources | the simulator drives the **real** `ControlLoop` |

nanoFramework targets its own `mscorlib`, so a normal `ProjectReference` is impossible — linking
sources is the only way to run the exact shipped code under a desktop runner. The constraint that
imposes on `Core/` is deliberate and worth keeping: no LINQ, no generic collections, nothing
missing from the nanoFramework base class library.

## Running it

```bash
dotnet test Formicarium.slnx                          # 55 control-logic tests
dotnet run --project dashboard/Formicarium.Dashboard  # dashboard against the simulator
pwsh firmware/build-firmware.ps1                      # firmware -> deployable .pe image
```

The dashboard defaults to `Controller:UseSimulator: true`. The simulator models the thermal,
hydration and foraging dynamics and runs at 120× wall-clock, so a full day passes in twelve
minutes — long enough to watch the midnight colour rotation and the diurnal traffic cycle. Point
`Controller:BaseAddress` at the ESP32 and set `UseSimulator: false` to switch to real hardware;
nothing else changes.

Open `docs/build-guide.html` directly in a browser, or read the
[published version](https://claude.ai/code/artifact/0bfd0671-845a-4c76-b151-b84ba596428e).

## Building the firmware

```powershell
./firmware/build-firmware.ps1
```

Produces `firmware/Formicarium.Controller/bin/Release/Formicarium.Controller.pe` plus 23
dependency assemblies — the complete deployable image.

The `.nfproj` is not in the solution and does not build with `dotnet build`: it targets
`netnano1.0`, and the nanoFramework build tasks are .NET Framework assemblies, so it needs real
MSBuild. It normally also needs the Visual Studio extension, but the script sidesteps that — the
extension's VSIX is a zip, and the MSBuild props, targets and build-task assemblies sit inside it
under `$MSBuild/nanoFramework/v1.0/` in exactly the layout a real install produces. The script
downloads that release asset (pinned), extracts the folder into a gitignored `firmware/.build/`,
restores `packages.config`, and points MSBuild at it with `-p:NanoFrameworkProjectSystemPath`.
Nothing is installed machine-wide and no extension is registered.

Prerequisites: Visual Studio or Build Tools (for MSBuild), and `dotnet tool install -g nanoff`
for flashing.

WiFi credentials are deliberately absent from source. They live in the device's own configuration
block, written once with `nanoff --updatessid`, so they never enter git and survive a reflash.

**What compiling it caught.** Five real defects that no amount of desktop testing would have
found, because they were all in the layer `Core/` is insulated from:

- `Math` is not in nanoFramework's `mscorlib` — it is a separate `System.Math` assembly.
- `UnitsNet`'s root namespace is `UnitsNet`, not `nanoFramework.UnitsNet`.
- The WS2812B RMT driver needs `nanoFramework.Hardware.Esp32.Rmt`, a package I had not listed.
- `HttpListener`'s response stream needs `nanoFramework.System.IO.Streams`.
- Ten of the nineteen package versions I had guessed did not exist, and the OneWire package is
  `nanoFramework.Device.OneWire`, not `System.Device.OneWire`.

**Still unverified:** the firmware has never run on an ESP32. It compiles and links against the
real bindings, which rules out wrong types and wrong signatures, but not wrong behaviour.

## Safety rules the tests exist to prove

1. A failed sensor read produces an **invalid** `Reading`, never a stale value.
2. Fail-safe is *off*, not *hold*, and it bypasses the dwell timer — a heater held on because it
   "hasn't been on long enough to turn off" is the bug that ordering prevents.
3. The over-temperature cutout reads **every** nest probe, not just the heater's control probe, so
   one sensor failing low cannot cook the colony while a second watches it happen.
4. `MistMaxRunSeconds` has no equivalent any more, because **the pump no longer touches the nest**.
   It fills a 131 mL reservoir that wicks into the core, and that reservoir is smaller than a
   harmful dose. The guarantee lives in the geometry, where an electrical fault cannot reach it.
5. Hydration keys off the reservoir float switch, not the soil probe, so a dead probe degrades the
   system to "passively hydrated, unmonitored" instead of stopping watering altogether.
6. There is no gate. A servo that jams closed starves the colony unattended, and no firmware can
   tell a closed gate from a stuck one. Three always-open risers replace it; service closure is a
   manual plug cap.
7. Boot state is every output off, and nothing energises until some sensor has read successfully.

## The experiment

Each riser mouth is lit from below through a frosted ring-window, in one of three channels, at a
brightness tracking that riser's own traffic. Ants are most sensitive to UV and blue-green and
effectively **blind to deep red**, so the channels are three very different stimulus strengths —
the prediction is that traffic drifts toward whichever riser currently shows red.

The mapping rotates at local midnight on a three-day cycle and every logged traffic row carries
the channel and brightness in force when it was counted. Without that rotation, "they prefer
green" and "they prefer the tube nearest the food dish" produce identical data. Three conditions
are supported: **Off** (dark baseline), **Fixed** (lit, carrying no information), and **ClosedLoop**
(the treatment). A dark period runs 22:00–07:00 local regardless of mode.

## Still open

- Real-hardware bring-up. `docs/build-guide.html` has the breadboard order and the per-subsystem
  failure modes ready to work through when parts arrive.
- Public hosting of the dashboard (auth, TLS, remote reach). Local only for now; the client
  abstraction is what keeps that a contained change.
- Protein feeding stays manual, per the original design — fruit flies are not worth automating and
  a jammed protein feeder would rot in the dish.
