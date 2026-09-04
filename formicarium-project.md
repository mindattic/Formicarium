# Self-Sufficient Formicarium — Design Record

This is the current design. It began as a very different sketch; everything that changed is
recorded with its reasoning in [`docs/bom.md`](docs/bom.md) and in the Design Decisions section of
[`docs/build-guide.html`](docs/build-guide.html), because the reasoning turned out to be more
reusable than the conclusions.

## Species

***Tetramorium immigrans*** (pavement ant), from
[statesideants.com](https://www.statesideants.com/product-page/tetramorium-immigrans), $28.99.
Chosen over *Camponotus pennsylvanicus* because it needs no diapause, tolerates a wide temperature
and humidity range, eats almost anything, and grows fast — 1,000–10,000 workers in one to two
years. Nest target **79–83 °F**, held flat year-round, with no cooling or diapause state to manage.

That growth rate is also why a capped expansion port goes into the nest wall during construction:
the colony will outgrow one chamber, and cutting into a populated column later is not a plan.

## Physical form — one rigid square column

Bottom to top: **ballasted base → nest → electronics bay → outworld → vented lid**, with three
straight bypass risers running up the *outside* of the bay to connect nest to outworld.

```
        vented lid, mesh <0.5 mm, Fluon rim
   ,-------------------------------.
   |          OUTWORLD             |  frosted ring-windows in the floor,
   |    o    ~~~~                  |  one around each riser mouth
[] |===============================|
[] |      ELECTRONICS BAY          |  sealed; RGB rings shine UP through the floor
[] |      [ side access panel ]    |  thermal break at its own floor
[] |===============================|
^^ |:::::::::::::::::::::::::::::::|
|| |::   YTONG NEST CORE         ::|  opaque sleeve lifts off to view
|| |::   carved chambers         ::|
|| |::   [131 mL reservoir]      ::|  wicks; never pumped into
   |_______________________________|
  ===================================
  |     ballasted base, 16 in sq     |
  |__________ moat tray _____________|

  ^^ three risers, solvent-welded through flat faces
```

Three properties drove this shape, and each one killed an earlier design:

- **Untippable.** Separate vessels joined by a tube were both a tipping hazard and a containment
  failure waiting to happen. One rigid stack with the mass low has a tip angle of about 45°.
- **Nothing to disconnect.** Every riser is solvent-welded through a flat panel with a collar
  either side. That is a chemical weld, not a friction fit — there is nothing to work loose.
- **Every sealed joint lands on a flat surface.** An O-ring cannot seat on a curve and a riser
  welded to a curved wall cannot be clamped while it cures, which is why the column is square
  rather than the cylinder it started as.

Fluon (PTFE) on the inner wall below the lid rim is the escape barrier, and it is non-negotiable
given how small *Tetramorium* workers are. The whole column stands in a shallow moat tray as a
last line of containment.

## Subsystems

**Heating.** A 12 V DC silicone cable wrapped around the *outside* of the nest section — no
penetration, no moisture on the heater, no ant contact. Hysteresis control with a deadband and
minimum dwell, from the nest-bottom DS18B20. The nest-top probe is reported but never controlled
to: the gap between them *is* the vertical gradient the colony wants.

**Hydration.** Passive. A pump fills a 131 mL reservoir; the reservoir wicks into the Ytong core
with no actuator on that leg at all. The reservoir is smaller than a dose that could harm the
colony, so no pump failure matters. Refilling keys off a float switch rather than the soil probe,
so a dead probe leaves the colony hydrated but unmonitored instead of unwatered.

**Circulation.** A 5 V fan in the lid, gated on outworld humidity, not run continuously —
constant airflow would dry the nest through the risers.

**Traffic and light.** One IR break-beam per riser, debounced, feeding a rolling rate. Each riser
mouth is lit from below in one of the red/green/blue channels at a brightness tracking its own
traffic — a closed behavioural loop at the point where an ant picks a tube. Ants are effectively
blind to deep red, so the channels are three very different stimulus strengths; the mapping
rotates at local midnight so colour can be separated from tube position.

**Feeding.** A second peristaltic pump doses sugar water into the outworld dish daily. Protein
feeding stays manual — fruit flies are not worth automating and a jammed protein feeder would rot.

**Camera.** An IR-capable ONVIF/RTSP IP camera, consumed directly by the dashboard. 850 nm is
invisible to the ants and to us, so the column can be watched all night without lighting it.

**No gate.** Deliberately. A powered gate between the colony and its food was the largest single
point of failure in the system, and no firmware can distinguish a gate that is closed from one
that is stuck. Three always-open risers give redundancy against one fouling; closure for servicing
is a manual plug cap.

## Software

C# throughout. `firmware/Formicarium.Controller` runs .NET nanoFramework on an ESP32;
`dashboard/Formicarium.Dashboard` is Blazor Server with a SQLite telemetry log.

The control logic lives in a hardware-free `Core/` folder that is compiled into the firmware, into
a desktop xUnit project, and into the dashboard's colony simulator — the same source in all three,
so the simulator drives the genuine control loop rather than a second implementation of it. See
[`README.md`](README.md) for why that matters and what it constrains.

## Where things stand

Nothing is ordered. The pin map, parts list, firmware, dashboard and build guide are complete;
55 control-logic tests pass on the desktop. Remaining work is physical: build it, work through the
bring-up order in the guide, run it empty for two weeks, then introduce the colony.
