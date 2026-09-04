# Self-Sufficient Formicarium — Design Record

The design as built. Parts and sourcing are in [`docs/bom.md`](docs/bom.md); the build sequence,
drawings and interactive blueprint are the dashboard's Build page at `/build`.

## Species

***Tetramorium immigrans*** (pavement ant), from
[statesideants.com](https://www.statesideants.com/product-page/tetramorium-immigrans), $28.99.
Chosen over *Camponotus pennsylvanicus* because it needs no diapause, tolerates a wide temperature
and humidity range, eats almost anything, and grows fast — 1,000–10,000 workers in one to two
years. Nest target **79–83 °F**, held flat year-round, with no cooling or diapause state to manage.

That growth rate is also why a capped expansion port goes into the nest wall during construction:
the colony will outgrow one chamber, and cutting into a populated column later is not a plan.

## Physical form — one rigid square column

Bottom to top: **ballasted base → nest → electronics bay → outworld → vented lid**. 8 × 8 in
internal section, 26 in tall.

```
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

Four properties drive this shape:

- **The risers carry the column.** Load leaves the outworld floor plate through three ¾ in acrylic
  risers on the midlines of three faces and a portal frame on the fourth, and arrives at the nest
  ceiling plate. Both plates are 10 in squares overhanging the column by ¾ in, and that overhang is
  the land the riser collars bear on. The bay enclosure between them carries nothing — which is
  exactly why one of its four faces can be an opening.
- **Untippable.** One rigid stack with the mass low. A 16 in base under a 26 in column gives a tip
  angle of about 45°, dominated by where the ballast sits rather than by height.
- **Nothing to disconnect.** Every riser is solvent-welded through a flat plate with a collar
  against each face. That is a chemical weld, not a friction fit — there is nothing to work loose,
  and 5.4 in² of welded annulus per plate per riser (two 2 in collars at 2.7 in² each) against a
  service load under two pounds.
- **Every sealed joint lands on a flat surface.** An O-ring cannot seat on a curve and a riser
  welded to a curved wall cannot be clamped while it cures, which is why the section is square.

The medium is carved Ytong rather than loose fill or gel: rigid galleries cannot subside, moisture
is uniform, and it wicks — which is what makes passive hydration work at all. Two 100 mm slabs sit
back to back with galleries carved into the outward face of each, so the nest reads from two
opposite walls.

Fluon (PTFE) on the inner wall below the lid rim is the escape barrier, and it is non-negotiable
given how small *Tetramorium* workers are. The whole column stands in a shallow moat tray as a last
line of containment.

## Subsystems

**Heating.** A 12 V DC silicone cable wrapped around the *outside* of the nest section — no
penetration, no moisture on the heater, no ant contact. Hysteresis control with a deadband and
minimum dwell, from the nest-bottom DS18B20. The nest-top probe is reported but never controlled
to: the gap between them *is* the vertical gradient the colony wants.

**Hydration.** Passive. A pump on the base fills a 131 mL reservoir; the reservoir wicks into the
Ytong core with no actuator on that leg at all. The reservoir is smaller than a dose that could harm
the colony, so no pump failure matters. Refilling keys off a float switch rather than the soil probe,
so a dead probe leaves the colony hydrated but unmonitored instead of unwatered.

**Circulation.** Two fans on two zones. One in the lid gated on outworld humidity; one bonded over a
mesh-screened port in the nest ceiling, drawing nest air out, gated on *nest* humidity and set high,
because it is a mould guard rather than a climate control — the colony wants a humid nest and the
wicking core continuously replaces what the fan removes. A passive mesh intake low on the nest wall
completes bottom-to-top cross-flow along the thermal gradient. Nothing mechanical sits inside the
nest.

**Serviceability.** The nest is never opened once the colony is in, so everything else is built to be
reached from outside it, and it splits three ways. The **drawer** carries what has firmware or a
failure mode worth swapping — the ESP32 and the perfboard with all five MOSFET stages — on a tray that
slides out of the bay's fourth face behind a gasketed plate on two captive thumbscrews. **Bonded in
the bay** are the three LED rings, the nest exhaust fan and the bulkhead connector strip, all aligned
to holes in the plates. **On the base**, outside the column entirely, are both peristaltic pumps and
the supply jar. The outworld's four walls and lid lift off a fixed floor on a clamped gasket for
cleaning, with the riser welds and ring-windows staying put in that floor; the three manual riser
plugs go in first.

**Traffic and light.** One IR break-beam per riser, debounced, feeding a rolling rate. Each riser
mouth is lit from below in one of the red/green/blue channels at a brightness tracking its own
traffic — a closed behavioural loop at the point where an ant picks a tube. Ants are effectively
blind to deep red, so the channels are three very different stimulus strengths; the mapping rotates
at local midnight so colour can be separated from tube position.

**Feeding.** A second peristaltic pump doses sugar water into the outworld dish daily. Protein
feeding stays manual — fruit flies are not worth automating and a jammed protein feeder would rot.

**Camera.** An IR-capable ONVIF/RTSP IP camera, consumed directly by the dashboard. 850 nm is
invisible to the ants and to us, so the column can be watched all night without lighting it.

**No gate.** Deliberately. A powered gate between the colony and its food would be the largest single
point of failure in the system, and no firmware can distinguish a gate that is closed from one that
is stuck. Three always-open risers give redundancy against one fouling; closure for servicing is a
manual plug cap.

## Software

C# throughout. `firmware/Formicarium.Controller` runs .NET nanoFramework on an ESP32;
`dashboard/Formicarium.Dashboard` is Blazor Server with a SQLite telemetry log and the build manual
as one of its pages.

The control logic lives in a hardware-free `Core/` folder that is compiled into the firmware, into a
desktop xUnit project, and into the dashboard's colony simulator — the same source in all three, so
the simulator drives the genuine control loop rather than a second implementation of it. See
[`README.md`](README.md) for why that matters and what it constrains.

## Where things stand

Nothing is ordered. The pin map, parts list, firmware, dashboard and build manual are complete. The
firmware compiles to a deployable image and 58 control-logic tests pass on the desktop, though it
has never run on an ESP32. Remaining work is physical: build it, work through the bring-up order on
the Build page, run it empty for two weeks, then introduce the colony.
