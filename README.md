# Formicarium

A self-regulating ant habitat: one acrylic column with a heated, self-watering nest, ESP32 firmware in C# on .NET nanoFramework, and a Blazor dashboard that runs the real control loop against a colony simulator.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![nanoFramework ESP32](https://img.shields.io/badge/nanoFramework-ESP32-E7352C) ![Blazor Server](https://img.shields.io/badge/dashboard-Blazor%20Server-5C2D91) ![Tests 58](https://img.shields.io/badge/tests-58-brightgreen) ![Status design complete](https://img.shields.io/badge/status-design%20complete%2C%20not%20built-yellow)

```text
                    lid, vented, Fluon rim
             ,-------------------------------.
             |          OUTWORLD             |   frosted ring-window around
             |     o     ~~~~                |   each riser mouth
        ,----|===============================|----,   <- outworld floor plate, 10 in sq
        ||   |     BAY ENCLOSURE  [drawer]  >|>   ||   RGB rings shine UP through the floor
        ||   |     5.25 in sq, no load       |    ||   the drawer comes out this face
        `----|===============================|----'   <- nest ceiling plate, 10 in sq
        ^^   |:::::::::::::::::::::::::::::::|   ^^
        ||   |::   YTONG NEST CORE         ::|   ||    opaque sleeve lifts off to view
        ||   |::   two slabs, back to back ::|   ||
        ||   |::   [131 mL reservoir]      ::|   ||    wicks; never pumped into
             |_______________________________|
       =========================================
       |  ballasted base -- pumps + supply jar  |
       |______________ moat tray _______________|

    ^^ two of the three load-bearing risers; the third is on the far face
```

Nothing has been ordered yet. The repo is complete on paper and verifiable without hardware: the firmware compiles to a deployable image, the control logic is unit-tested on the desktop, and the dashboard runs the real firmware policy against a colony simulator.

## Why

- Keep a colony of *Tetramorium immigrans* (pavement ants) warm, humid and fed without daily care: heating, hydration, airflow and sugar feeding are automatic.
- Sleep through failures. Every failure worth worrying about here is slow (a cooked nest, a flooded nest, a colony cut off from food), and the simulator fast-forwards a week of them in milliseconds so the safety rules are tested, not hoped about.
- Trust the same code everywhere: one hardware-free control core is compiled into the ESP32 firmware, the desktop tests and the dashboard simulator.
- Run a real behavioural experiment: each riser mouth is lit in a colour that tracks its own traffic, to see whether ants drift toward the channel they can barely see.
- Build it from a manual that works offline: the dashboard's Build page has a 3D blueprint, drawings, parts and pin tables and a 44-step sequence.

## Features

### The column

One rigid square column, 8 x 8 in internal and 26 in tall: ballasted base, nest, electronics bay, outworld, vented lid. Three ¾ in acrylic risers carry the outworld's load down to the nest, welded with collars through two overhanging plates, so the bay enclosure between those plates carries nothing; that is what lets one of its four faces be a drawer. Three faces have a riser; the fourth has the electronics, on a tray that slides out behind a gasketed face plate without disturbing a weld or any part of the ant path.

- Untippable: a 16 in base under the 26 in column gives a tip angle of about 45°, set by where the ballast sits.
- Nothing to disconnect: every riser is solvent-welded through a flat plate with a collar on each face.
- Every sealed joint lands on a flat surface, which is why the section is square.
- The nest medium is carved Ytong (autoclaved aerated concrete), which stays rigid and wicks water evenly.
- A capped expansion port goes into the nest wall during construction, because the colony will outgrow one chamber.

### Subsystems

- Heating: a 12 V silicone heating cable wrapped around the outside of the nest, with hysteresis control from the nest-bottom DS18B20 probe. The nest target is 79 to 83 °F, held flat all year.
- Hydration: passive. A pump fills a 131 mL reservoir that wicks into the core; nothing pumps into the nest itself.
- Circulation: two fans on two zones, the lid fan gated on outworld humidity and a nest exhaust gated on nest humidity as a mould guard, with a passive low intake for cross-flow.
- Traffic and light: one IR break-beam per riser feeding a rolling traffic rate, and an RGB ring under each riser mouth.
- Feeding: a second peristaltic pump doses sugar water into the outworld dish daily. Protein feeding stays manual.
- Camera: an IR-capable ONVIF/RTSP IP camera at 850 nm, invisible to the ants, shown in the dashboard.
- No gate, on purpose: three always-open risers give redundancy, and service closure is a manual plug cap.

### Dashboard

Blazor Server on .NET 10 with telemetry in SQL Server through EF Core. Pages: Home, History, Traffic, Camera and Build. The Build page has an interactive 3D blueprint (Three.js, with a Babylon.js renderer for comparison), elevation and bay-plan drawings, the parts and pin tables, and a 44-step build sequence in four phases whose progress is stored on the server. Three.js and Babylon.js are served from `wwwroot/lib`, so the page works with no internet.

## Quick start

Prerequisites: the .NET 10 SDK.

```bash
git clone https://github.com/mindattic/Formicarium.git
cd Formicarium
dotnet test Formicarium.slnx
dotnet run --project dashboard/Formicarium.Dashboard
```

The tests run the 58 control-logic tests. The dashboard starts against the simulator, which models thermal, hydration and foraging dynamics at 120x wall-clock, so a full day passes in twelve minutes: long enough to watch the midnight colour rotation and the daily traffic cycle. Open `/build` for the build manual.

To use real hardware, set `Controller:UseSimulator` to `false` and point `Controller:BaseAddress` at the ESP32 in `appsettings.json`; nothing else changes. Telemetry uses the `Formicarium` connection string, which defaults to SQL Server LocalDB.

## How it works

`Core/` contains every control decision and touches no hardware type. `Hardware/` is a thin adapter behind two interfaces. That split makes the interesting half of the firmware testable.

```text
firmware/Formicarium.Controller/Core/   Actuation, Config, Control, Sensing, State
        |                 (no LINQ, no generic collections: nanoFramework BCL only)
        +--> Formicarium.Controller.nfproj      direct Compile Include   ships to the ESP32
        +--> Formicarium.Controller.Tests       linked sources, xUnit    proves the logic
        +--> Formicarium.Dashboard              linked sources           simulator drives the real ControlLoop

Hardware/  Esp32SensorSet, Esp32ActuatorSet      the only code that knows what a GPIO is
Net/       HttpApiServer, FallbackPage           HTTP API and a small self-hosted fallback page
```

nanoFramework targets its own `mscorlib`, so a normal `ProjectReference` is impossible; linking sources is the only way to run the exact shipped code under a desktop runner. The constraint that puts on `Core/` is deliberate: nothing missing from the nanoFramework base class library.

## Safety rules

The tests exist to prove these:

1. A failed sensor read produces an invalid `Reading`, never a stale value.
2. Fail-safe is off, not hold, and it bypasses the dwell timer. A heater held on because it "has not been on long enough to turn off" is the bug that ordering prevents.
3. The over-temperature cutout reads every nest probe, not just the heater's control probe, so one sensor failing low cannot cook the colony while a second watches.
4. There is no maximum-mist-runtime rule to get wrong, because the pump never touches the nest. It fills a 131 mL reservoir that is smaller than a harmful dose. The guarantee lives in the geometry, where an electrical fault cannot reach it.
5. Hydration keys off the reservoir float switch, not the soil probe, so a dead probe degrades to "passively hydrated, unmonitored" instead of stopping watering.
6. There is no gate. A servo that jams closed starves the colony unattended, and no firmware can tell a closed gate from a stuck one.
7. Boot state is every output off, and nothing energises until some sensor has read successfully.
8. The two fans watch different zones. The nest exhaust is gated on nest humidity and set high, because it is a mould guard, not a climate control.

## The experiment

Each riser mouth is lit from below through a frosted ring-window, in one of three channels, at a brightness tracking that riser's own traffic. Ants are most sensitive to UV and blue-green and effectively blind to deep red, so the channels are three very different stimulus strengths. The prediction is that traffic drifts toward whichever riser currently shows red.

The mapping rotates at local midnight on a three-day cycle, and every logged traffic row carries the channel and brightness in force when it was counted. Without that rotation, "they prefer green" and "they prefer the tube nearest the food dish" produce identical data.

| Mode | Meaning |
| --- | --- |
| Off | Dark baseline |
| Fixed | Lit, carrying no information |
| ClosedLoop | The treatment |

A dark period runs 22:00 to 07:00 local in every mode.

## Building the firmware

```powershell
./firmware/build-firmware.ps1
```

This produces `firmware/Formicarium.Controller/bin/Release/Formicarium.Controller.pe` plus 23 dependency assemblies, the complete deployable image.

The `.nfproj` is not in the solution and does not build with `dotnet build`: it targets `netnano1.0`, and the nanoFramework build tasks are .NET Framework assemblies, so it needs real MSBuild. It normally also needs the Visual Studio extension, but the script sidesteps that. The extension's VSIX is a zip, and the MSBuild props, targets and build-task assemblies sit inside it in exactly the layout a real install produces. The script downloads that pinned release asset, extracts it into a gitignored `firmware/.build/`, restores `packages.config`, and points MSBuild at it with `-p:NanoFrameworkProjectSystemPath`. Nothing is installed machine-wide.

Prerequisites: Visual Studio or Build Tools (for MSBuild), and `dotnet tool install -g nanoff` for flashing.

Wi-Fi credentials are deliberately absent from source. They live in the device's own configuration block, written once with `nanoff --updatessid`, so they never enter git and survive a reflash.

What compiling it caught: five real defects that desktop testing could not find, because they were all in the layer `Core/` is insulated from.

- `Math` is not in nanoFramework's `mscorlib`; it is a separate `System.Math` assembly.
- The UnitsNet root namespace is `UnitsNet`, not `nanoFramework.UnitsNet`.
- The WS2812B RMT driver needs `nanoFramework.Hardware.Esp32.Rmt`, which was not listed.
- The `HttpListener` response stream needs `nanoFramework.System.IO.Streams`.
- Ten of the nineteen guessed package versions did not exist, and the OneWire package is `nanoFramework.Device.OneWire`, not `System.Device.OneWire`.

## Cost and parts

Parts links were last checked 2026-09-03. The planning total is roughly $560 to $640, excluding tools. Four prices are confirmed against the vendor and the rest are estimates. One part, the AAC nest core, is hard to buy in small quantities in the US and should be settled before any acrylic is cut. The colony itself comes from a US ant supplier; see the design record. Full detail, the cut list and the reasons behind each part are in [docs/bom.md](docs/bom.md).

## Project layout

```text
docs/
  bom.md                  parts, sourcing, dimensions, cut list, and why each choice is what it is
  gpio-map.md             canonical pin assignment, the single source of truth
formicarium-project.md    design record: species, physical form, subsystems, status
firmware/
  build-firmware.ps1      builds the deployable .pe image without installing anything
  Formicarium.Controller/
    Core/                 hardware-free control logic, shared verbatim with the tests
    Hardware/             the only code that knows what a GPIO is
    Net/                  HTTP API and a small self-hosted fallback page
  Formicarium.Controller.Tests/   58 tests, desktop, no ESP32 required
dashboard/
  Formicarium.Dashboard/          Blazor Server, EF Core telemetry
    Components/Pages/Build.razor  the build manual
Formicarium.slnx
```

## Limitations

- The firmware has never run on an ESP32. It compiles and links against the real bindings, which rules out wrong types and signatures, but not wrong behaviour.
- Nothing has been built. The Build page has the breadboard bring-up order and per-subsystem failure modes ready for when parts arrive.

## Roadmap

- Real-hardware bring-up, then two weeks running empty before the colony is introduced.
- Public hosting of the dashboard (auth, TLS, remote reach). It is local only for now; the client abstraction keeps that a contained change.
- Protein feeding stays manual: fruit flies are not worth automating, and a jammed protein feeder would rot in the dish.

## Documentation

- [formicarium-project.md](formicarium-project.md): the design record
- [docs/bom.md](docs/bom.md): bill of materials, load path, dimensions, cut list, sourcing status
- [docs/gpio-map.md](docs/gpio-map.md): pin assignments, wiring notes, cable routing, power
- [AGENTS.md](AGENTS.md): instructions for AI agents working in this repo

## License

This repo has no LICENSE file. All rights reserved.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [Claudia](https://github.com/mindattic/Claudia), [ChiMesh](https://github.com/mindattic/ChiMesh).
