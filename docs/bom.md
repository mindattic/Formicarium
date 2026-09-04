# Bill of Materials

Revised from the original list in `formicarium-project.md`. Removed items are kept visible with
the reason, so the reasoning behind each change stays recoverable.

## What this is

A single rigid **square-section column**, stacked bottom to top:

```
              lid, vented, Fluon rim
        ,-------------------------.
        |        OUTWORLD         |   frosted ring-windows in the floor
        |    o    ~~~~            |   around each riser mouth
   [][] |=========================|   <- bay ceiling / outworld floor
   [][] |   ELECTRONICS BAY       |   RGB rings shine UP through the floor
   [][] |   [ slide-out blade    ] |   thermal break at its own floor
   [][] |=========================|   <- bay floor / nest ceiling
    ^^  |:::::::::::::::::::::::::|
    ||  |::  YTONG NEST CORE    ::|   opaque sleeve lifts off to view
    ||  |::  carved chambers    ::|
    ||  |::  [reservoir]        ::|   wicks; never pumped into
        |_________________________|
       ==============================
       |   ballasted base           |
       |________ moat tray _________|

    ^^ three bypass risers, welded through flat faces
```

Nothing is free-standing, nothing is push-fit, and there is no gate.

---

## Why square and not a cylinder

A round tube looked better and turned out to be wrong on four counts:

1. **Every joint needs a flat surface.** An O-ring or bulkhead cannot seat on a curve, and a
   riser welded to a curved wall cannot be clamped while it cures. This was already the stated
   reason the divider port went on a flat plate — leaving the risers piercing a curved wall was
   simply inconsistent.
2. **Sourcing.** Large-bore cast acrylic tube is a specialty, cut-to-order, sold-by-the-foot item
   ([ePlastics](https://www.eplastics.com/shapes/plexiglass/tube/clear-cast),
   [TAP](https://www.tapplastics.com/product/plastics/plastic_rods_tubes_shapes/clear_cast_by_ft/653),
   [Curbell](https://www.curbellplastics.com/product-category/material/acrylic/acrylic-tube/),
   [MSC](https://www.mscdirect.com/product/details/16602971)) and was the most expensive single
   part in the build. Flat ¼" cast acrylic sheet is a commodity.
3. **The Ytong core.** Turning a block of aerated concrete to fit a tube bore is real work.
   Cutting it to fit a square box is a saw cut.
4. **The camera.** Curved acrylic distorts; flat faces do not.

**The honest cost:** a tube has zero longitudinal seams and a box has four. But flat-panel
solvent-welded acrylic is the most solved problem in the material — every acrylic aquarium is
built this way and holds hundreds of pounds of water. This box only has to hold ants.

## The riser-through-panel joint

The one joint that carries containment. It is a **weld, not a fitting**:

```
        collar (solvent-welded)
                 |
    ============[|]============    flat panel, drilled to tube OD
                 |
        collar (solvent-welded)
                 |
             riser tube
```

Drill the panel to the tube's outside diameter, slide the tube through, then solvent-weld a
small acrylic washer (the collar) against each face and fillet with Weld-On 16. Solvent welding
chemically fuses the parts into one piece of acrylic — there is no gasket to compress, no
friction fit to work loose, and on a flat face it can be clamped while curing.

---

## Structure

| Item | Qty | Source | Notes |
|---|---|---|---|
| Cast acrylic sheet, ¼", clear | ~6 sq ft | Commodity — hardware store, [TAP](https://www.tapplastics.com), Amazon | All four walls, base, both bay plates, outworld floor, lid. Cast, not extruded: it solvent-welds and machines far better. |
| Clear acrylic tube, ½" OD | ~18" | [Canal Plastics](https://www.canalplastic.com/products/clear-colorless-acrylic-tube) (¼"–12" OD, same-day cut) | Three bypass risers. The only specialty part left, and it is short and cheap. |
| Acrylic offcut for collars | scrap | from the sheet | Six collars, two per riser |
| Weld-On 16 (thickened) + Weld-On 4 (thin) | 1 ea | Amazon / TAP | 4 for panel seams, 16 for collar fillets |
| Ytong / autoclaved aerated concrete block | 1 | Masonry supply, eBay | The nest core. Carves with a spoon, wicks water. Cut to fit the box section. |
| [Aquarium-safe silicone sealant](https://www.amazon.com/Aquarium-Marine-Silicone-Sealant-Adhesive/dp/B012NOVTD2) | 1 | | Gaskets and secondary sealing |
| Closed-cell gasket sheet | 1 | | Module flanges, bay access panel |
| Opaque sleeve stock (thin ply, ABS, or vinyl wrap) | 1 | | **Lifts off to view the nest.** See below. |
| Ballast plate + base stock, ~14–16" square | 1 | | Footprint at least 0.6 × overall height |
| Thumbscrews / captive fasteners | ~12 | | Blade face plate, outworld clamps |
| Toggle clamps or knurled thumbscrews | 4-6 | | Compress the outworld gasket. Must be releasable by hand, with the column in place. |
| Drawer slides or acrylic angle rail | 1 pair | | The electronics blade. Aluminium micro-slides or a simple acrylic channel both work at this weight. |
| JST bulkhead connector block | 1 | | Blade harness lands here so the tray disconnects in one motion |
| Stainless mesh, under 0.5 mm aperture | small | | Lid vent, nest ceiling exhaust port, nest wall intake port. Tetramorium pass anything larger. |
| Riser plug caps | 3 | | **Manual** service closure — deliberately not powered |
| Shallow tray (moat) | 1 | | Whole column stands in it. Last line of containment. |
| [byFormica PTFE Plus Fluon](https://www.amazon.com/byFormica-Insect-Escape-Prevention-Coating/dp/B0FBKSRJFG) | 1 | | Band on the inner wall below the lid rim |
| [Tetramorium immigrans colony](https://www.statesideants.com/product-page/tetramorium-immigrans) | 1 | | $28.99 |

## Hydration

| Item | Qty | Notes |
|---|---|---|
| Small reservoir vessel, ~120 mL | 1 | **Sized so its entire contents cannot harm the nest.** This is the safety guarantee — see below. |
| Float switch, vertical, normally-open | 1 | Reads full. Wired so a broken wire reads "not full". |
| [Kamoer NKP 12 V peristaltic pump](https://www.amazon.com/Kamoer-Peristaltic-Hydroponics-Nutrient-Analytical/dp/B07GWJ78FN) | **2** | One water, one sugar water. Different fluids cannot share a pump or a line. |
| Water supply container | 1 | Any sealed jar. Refilled every few weeks. |
| [Silicone airline tubing, 3/16"](https://www.amazon.com/Wave-point-Aquarium-Silicone-Tubing-Hydroponics/dp/B07NQV84NQ) | 1 | Supply and feed lines only — no ant tube in this design |

## Controller and sensors

| Item | Qty | Notes |
|---|---|---|
| [ESP32 DevKit (ESP-WROOM-32)](https://www.amazon.com/ESP32-DEVKIT-ESP-WROOM-32-4MB-CP2101/dp/B07F1GWJ1N) | 1 | Runs .NET nanoFramework |
| [Hilitchi DS18B20 waterproof probes, 5-pack](https://www.amazon.com/Hilitchi-DS18B20-Waterproof-Temperature-Sensors/dp/B018KFX5X0) | 1 | 3 used (nest bottom, nest top, outworld), 2 spare |
| [Adafruit SHT31-D breakout](https://www.adafruit.com/product/2857) | **2** | 0x44 outworld, 0x45 nest — see below |
| [DFRobot Gravity capacitive soil moisture sensor](https://www.amazon.com/DFROBOT-Gravity-Capacitive-Corrosion-Resistant/dp/B01GHY0N4K) | 1 | In the Ytong core. Must be on ADC1. |
| [Adafruit IR break beam, 3 mm LEDs](https://www.adafruit.com/product/2167) | **3** | One pair per riser |
| [IRLZ44N logic-level MOSFETs, 5-pack](https://www.amazon.com/Bestol-5PCS-IRLZ44N-MOSFET-220AB/dp/B07DWYGNHC) | 1 | All 5 used: heater, refill pump, feed pump, outworld fan, nest exhaust fan |
| WS2812B / NeoPixel rings, ~12 px | 3 | One per riser mouth. Chained on one data line over ESP32 RMT. |

## Power, climate, camera

| Item | Qty | Notes |
|---|---|---|
| 12 V DC silicone heating cable, ~20 W | 1 | **Replaces the AC heat cable.** Wraps the outside of the nest section — no penetration at all. |
| [Gdstime 40 mm 5 V fan](https://www.amazon.com/Gdstime-40mm-Small-Brushless-Cooling/dp/B00MYZADCY) | **2** | One outworld circulation, one nest exhaust. Both RH-gated, on different zones. |
| 12 V 3 A PSU | 1 | Single supply for the whole column |
| Buck converter 12 V to 5 V (MP1584 / LM2596) | 1 | Feeds ESP32, fan, LEDs, IR emitters |
| IR-capable ONVIF / RTSP IP camera | 1 | **Replaces the ESP32-CAM** |
| Rigid foam / cork sheet | small | Thermal break between nest ceiling and bay floor |

## Passives and small parts

| Item | Qty | Notes |
|---|---|---|
| 4.7 kOhm resistor | 1 | **Mandatory** 1-Wire pull-up. Without it no probe enumerates. |
| 150 Ohm resistor | 5 | MOSFET gate resistors |
| 10 kOhm resistor | 5 | Gate pulldowns — hold every output off through boot |
| SS34 / 1N5819 Schottky diode | 4 | Flyback across both pumps and both fans |
| Perfboard + screw terminals | 1 | Final wiring |
| JST pigtails | ~8 | So every module disconnects for service |
| Bulkhead fittings / glands | ~10 | Bay top and bottom plate penetrations — all flat |
| Hookup wire 22-24 AWG, heat-shrink, zip ties | | |

## Tools

- [Vastar 16-in-1 soldering iron kit](https://www.amazon.com/Vastar-Temperature-Adjustable-Desoldering-Anti-static/dp/B0747KYF6S)
- [Performance Tool mini tubing cutter](https://www.amazon.com/Performance-Tool-W82006-Vacuum-Cutter/dp/B07324LG4X)
- [Breadboard + jumper wire kit](https://www.amazon.com/clp/B0GXHDX4QZ)
- [SparkFun basic digital multimeter](https://www.amazon.com/Digital-Multimeter-Basic-by-Sparkfun/dp/B00NBVO2EU)
- Step drill bit (clean holes in acrylic without cracking), clamps, spoon or loop tool for Ytong

---

## Removed, and why

### ~~Zoo Med Repti Heat Cable, 25 W~~

A 120 VAC wall-plug device. An IRLZ44N is a low-voltage DC MOSFET and **cannot switch it**. The
original design would not have worked, and building it anyway would have put mains inside a
plastic enclosure alongside water lines.

### ~~HiLetgo ESP32-CAM~~ and ~~Ansice 850 nm illuminator~~

.NET nanoFramework has no OV2640 driver — an open feature request with no library — so this
would have meant an Arduino C++ component in an otherwise all-C# project.

### ~~Second tall acrylic vase (Outside pod)~~ and ~~large-bore acrylic tube~~

There is no second vessel, and no cylinder. Separate vessels joined by a tube were both a
tipping hazard and a containment failure waiting to happen; the cylinder then turned out to be
the wrong shape for every joint in the design and the hardest part to buy.

### ~~SG90 servo gate~~

Cut deliberately. A powered gate is the largest single point of failure in the system: a servo
that jams closed cuts the colony off from food and water, unattended, for as long as it takes to
notice. Three always-open risers replace it — redundant against one fouling — and service
closure is a manual plug cap, which cannot fail closed while nobody is watching.

### ~~Direct substrate misting~~

Same argument, applied to the pump. With the gate gone the mist pump became the largest
remaining single point of failure: a firmware runtime limit is no defence against a MOSFET that
fails short, a pump head that jams open, or a reservoir siphoning through a stopped pump, and
the nest is sealed with no drain.

---

## Additions worth explaining

### Passive hydration, and why the reservoir volume is the real safety feature

Borrowed from the way [Tar Heel Ants](https://tarheelants.com/) build their nests: a reservoir
wicks into the nest and is topped up every few weeks rather than sprayed on demand.

The pump now fills **only the reservoir**, and the reservoir wicks into the Ytong core. Because
its capacity is smaller than a dose that could harm the colony, **no failure of the pump matters**
— worst case it overfills a vessel that is harmless when full. The guarantee moved out of the
firmware and into the geometry, where an electrical fault cannot reach it.

A second benefit falls out for free: refilling keys off the **float switch**, not the moisture
probe, so a dead soil probe now degrades the system to "passively hydrated, unmonitored" instead
of stopping hydration altogether. Under the old design a failed probe would have dried the colony
out while every actuator sat correctly in its fail-safe state.

### Ytong nest core with carved chambers

A block of aerated concrete cut to the box section, with galleries carved into the face that
sits against the acrylic — so every chamber is visible, and moisture is uniform rather than the
soil probe reading one arbitrary point of loose fill. Ytong also wicks, which is what makes the
passive reservoir work.

### Serviceability: a blade, a lift-off shell, and a nest that never opens

Three different problems, three different answers, all driven by the same rule: **the nest is
never opened once the colony is in, so everything that might need attention has to be reachable
from outside it.**

**The electronics are a blade, not a module.** The bay's top plate is the outworld floor and its
bottom plate is the nest ceiling — both structural, both carrying sealed penetrations — so the bay
itself cannot slide out without the column coming apart. Instead the shell stays bonded and the
electronics ride a tray on rails, out through a gasketed front hatch on captive thumbscrews. The
harness lands on one JST block at the rear plus enough service loop to set the tray beside the
column while still live. The risers already bypass the bay, so pulling the blade never touches the
ant path.

**The outworld shell lifts off a fixed floor.** The risers are solvent-welded through the outworld
floor, so the floor cannot move — but it does not need to. The four walls and lid come off as one
unit, seating on a continuous gasket in a shallow rebate and compressed by toggle clamps. Cleaning
is: plug the three risers with the manual caps, unclamp, lift the shell, wash it, reseat. No weld
ever moves and the riser mouths and ring-windows stay exactly where they were.

**The nest is ventilated without ever being opened.** Three ½ in risers are not airflow. A Ytong
core above 80% RH held at 27 °C is a mould risk that the nest humidity sensor can see and nothing
could act on, so there is now a mesh-screened exhaust port through the nest ceiling with a fan on
the blade, and a passive mesh intake low on the nest wall behind the sleeve. Cross-flow runs bottom
to top, along the thermal gradient. Inside the nest there is only stainless mesh, which has nothing
to fail; every moving part is on the blade.

### Why not the blue gel

The blue gel sold in novelty ant farms is a nutrient agar, and it fails on its own terms here.

It *is* food, so at the 27 °C this nest is deliberately held at, it moulds — reliably, within weeks
to months. It cannot be rehydrated: it shrinks and cracks as it dries, which makes it the opposite
of a non-collapsing medium. It offers no humidity gradient and weeps rather than giving brood a
solid chamber floor. It is sold for a handful of queenless workers on a desk, not as a growth
substrate for a queen-headed colony intended to reach thousands. And it is incompatible with the
wicking reservoir that the entire hydration safety argument rests on.

Carved Ytong gives better visibility than gel anyway, because gel clouds and fractures within
months while a carved gallery pressed against clear acrylic stays a clear window for years.

### An opaque sleeve over the nest — the flaw this fixes

The earlier design left the nest in bare clear acrylic, permanently lit by room light. Ants dig
away from light, so that would have produced chronic stress **and** a nest you could not see into,
because they would have tunnelled against whichever face was darkest. A removable opaque sleeve
gives them darkness by default and gives you a full view on demand.

### A capped expansion port

The Mini Hearth XL carries side ports explicitly for "when they outgrow their habitat", and
*Tetramorium immigrans* was chosen precisely because it reaches 1,000–10,000 workers. A capped
bulkhead port goes into the nest section now, while the box is empty, rather than being cut into
a populated column later.

### Two SHT31s, not one

The ADDR pin gives 0x44 or 0x45, so both share one I2C bus for one extra part. **SHT31-A** reads
outworld air, which is what the fan can act on; **SHT31-B** reads nest airspace, where mould risk
lives. One sensor could not distinguish the two zones, which is what the original design goal of
catching a moist-substrate / dry-air mismatch actually requires.

### Three WS2812B rings

One per riser mouth, inside the sealed bay, shining **up** through frosted ring-windows in the
outworld floor. No ant contact, no moisture contact, serviceable from the bay. Each riser owns an
R/G/B channel whose brightness tracks that riser's traffic, closing a behavioural loop at the
point where an ant picks a tube.

Ants are most sensitive to UV and blue-green and effectively **blind to deep red**, so the three
channels are three very different stimulus strengths rather than three equivalent colours. The
firmware therefore **rotates the riser-to-channel mapping at local midnight and logs it**, so
"they prefer green" can be separated from "they prefer the tube nearest the food dish". Without
that control the experiment is uninterpretable.
