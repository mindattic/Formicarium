# Bill of Materials

Everything the column is made of, with sourcing and a costed total. **Links last checked
2026-09-03** — see [Cost and sourcing status](#cost-and-sourcing-status) for what is verified and
what is still an estimate. The build order, drawings and pin table live on
the dashboard's **Build** page (`/build`).

## What this is

A single rigid **square-section acrylic column**, stacked bottom to top. Three load-bearing risers
carry the outworld down to the nest, which frees the fourth face of the electronics bay to open as
a drawer.

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

    ^^ two of the three risers; the third is on the far face
```

Nothing is free-standing, nothing is push-fit, and there is no gate.

---

## The load path

The outworld, its substrate and the lid come to about five pounds, and they sit above a bay that
has to be open on one side. Everything about the middle of the column follows from that.

Load leaves the **outworld floor plate** — a 10 in square that overhangs the column by ¾ in on
every side — through **three ¾ in OD acrylic risers** on the midlines of three faces, and through a
**portal frame** of two stiles and a header on the fourth. It arrives at an identical **nest ceiling
plate** and passes into the nest walls and the base.

The plate overhang is not decoration: it is the land the riser collars bear on, and the rebate the
outworld gasket clamps into.

**The bay enclosure carries nothing.** It is a 5¼ in box standing between the plates — a waist
inside the column line — and its only job is keeping light and dust off the electronics. That is
exactly why one of its four faces can be a hole. Three faces are closed panels with a riser
standing in the step outside each; the fourth is the drawer, framed by the portal.

Three supports on a four-sided plate put the resultant off-axis, which is what the portal frame is
for: its stiles take the fourth quarter of the load, so the plate is supported at four points and
the drawer slides through a frame already carrying its share.

## The riser-through-plate joint

The joint that carries both the load and the containment. It is a **weld, not a fitting**:

```
        collar, 2 in OD (solvent-welded)
                 |
    ============[|]============    bay plate, ¼ in, drilled to tube OD
                 |
        collar, 2 in OD (solvent-welded)
                 |
             riser tube, ¾ in OD, ½ in bore
```

Drill the plate to the tube's outside diameter, slide the tube through, then solvent-weld an
acrylic collar against each face and fillet with Weld-On 16. Solvent welding chemically fuses the
parts into one piece of acrylic — no gasket to compress, no friction fit to work loose — and the
collars turn the joint into a bearing surface rather than a hole the tube happens to sit in. About
5.4 in² of welded annulus per plate per riser — two 2 in collars at 2.7 in² each — against a
service load under two pounds each.

Weld this assembly **flat on the bench**, both plates clamped parallel, before it is ever bonded to
the nest. A flat face can be clamped while it cures; that is what a curved wall makes impossible,
and it is the main reason the column is square.

## Why square and not a cylinder

1. **Every sealed joint needs a flat surface.** An O-ring or bulkhead cannot seat on a curve, and a
   riser welded to a curved wall cannot be clamped while it cures.
2. **Sourcing.** Large-bore cast acrylic tube is a specialty, cut-to-order, sold-by-the-foot item.
   Flat ¼ in cast acrylic sheet is a commodity.
3. **The Ytong core.** Turning a block of aerated concrete to fit a bore is real work. Cutting it to
   fit a square box is a saw cut.
4. **The camera.** Curved acrylic distorts; flat faces do not.

The honest cost is four longitudinal seams where a tube has none. Flat-panel solvent welding is the
most solved problem in the material — every acrylic aquarium is built this way and holds hundreds of
pounds of water. This one only has to hold ants.

---

## Key dimensions

| | |
|---|---|
| Column section | 8 × 8 in internal, 8½ in external, ¼ in wall |
| Nest | 12 in tall |
| Bay | 4½ in tall (two ¼ in plates + 4 in clear) |
| Outworld | 8 in tall |
| Overall | 26 in / 660 mm |
| Bay plates | 10 × 10 in, ¾ in overhang on all four sides |
| Bay enclosure | 5¼ in square external |
| Risers | 3 × ¾ in OD × ⅛ in wall, centres 3¼ in from the axis |
| Riser collars | 2 in OD, four per riser |
| Drawer opening | 4 × 2¾ in; tray 3¾ × 4½ in |
| Base | 16 × 16 in, ballasted |
| Reservoir | 131 mL |

The section is 8 in rather than 6 because the bay has to hold a drawer wide enough for the
controller board *and* leave three ½ in riser bores inside the outworld floor, clear of the walls.
Below 8 in those two demands collide.

---

## Cost and sourcing status

**Links last checked: 2026-09-03.** A ✔ price was read off the vendor's own page on that date. A ~
price is an estimate from typical US retail and has **not** been quoted — Amazon serves no price to
an automated fetch, and Amazon prices move weekly anyway. Treat the total as a planning figure, not
a quote.

| Line | Qty | Price | Basis |
|---|---|---|---|
| Adafruit SHT31-D breakout | 2 | **$13.95 ea — $27.90** | ✔ adafruit.com, in stock |
| Adafruit IR break beam, 3 mm | 3 | **$2.95 ea — $8.85** | ✔ adafruit.com, in stock |
| *Tetramorium immigrans* colony | 1 | **$28.99** | ✔ statesideants.com |
| Acrylic tube, ¾" OD × ⅛" wall | 2 ft | **$1.10/ft — $2.20** + cutting | ✔ canalplastic.com |
| Cast acrylic sheet, ¼", cut to size | ~9 sq ft | ~$75–110 | ~ est. $8–12/sq ft cut-to-size |
| AAC / Ytong block | 1 block | ~$20–60 | ~ **see the sourcing risk below** |
| Kamoer NKP peristaltic pump | 2 | ~$35 ea — $70 | ~ est. |
| IR ONVIF / RTSP IP camera | 1 | ~$35 | ~ est. |
| Weld-On 4 + Weld-On 16 | 1 ea | ~$25 | ~ est. |
| ESP32 DevKit | 1 | ~$12 | ~ est. |
| DS18B20 probes, 5-pack | 1 | ~$13 | ~ est. |
| Soil moisture sensor | 1 | ~$8 | ~ est. |
| IRLZ44N MOSFETs, 5-pack | 1 | ~$8 | ~ est. |
| WS2812B rings | 3 | ~$15 | ~ est. |
| 12 V DC heating cable, ~20 W | 1 | ~$18 | ~ est. |
| 12 V 3 A PSU + buck converter | 1 ea | ~$22 | ~ est. |
| 40 mm 5 V fans | 2 | ~$10 | ~ est. |
| Float switch | 1 | ~$8 | ~ est. |
| Drawer slides, 4" travel | 1 pair | ~$12 | ~ est. |
| Fluon PTFE Plus, 10 mL | 1 | ~$15 | ~ est. |
| Gasket sheet, toggle clamps, thumbscrews | — | ~$30 | ~ est. |
| Ballast, base stock, moat tray | — | ~$30 | ~ est. |
| Silicone, mesh, tubing, riser caps | — | ~$33 | ~ est. |
| Perfboard, JST, glands, passives, wire | — | ~$35 | ~ est. |

**Planning total: roughly $560–640**, excluding tools. Two lines dominate it — the acrylic sheet and
the pair of peristaltic pumps — and between them they are about a third of the build.

### Sourcing risk: the AAC block

This is the one part that could actually stop the build, and it is the nest core.

Raw autoclaved aerated concrete is a commodity in Europe and **genuinely hard to buy in small
quantities in the United States** — most US builders moved away from it, so the domestic
manufacturers that exist ([Aercon](https://aerconaac.com/), [Litecon](https://liteconusa.com/))
sell by the pallet, and a pallet runs into the hundreds of dollars for a part worth about $30.

What the ant-keeping market sells instead is *finished* Ytong nests
([Esthetic Ants via American Ant Supply](https://americanantsupply.com/products/copy-of-esthetic-ants-small-ytong-type-b),
[Just Ants](https://justants.shop/products/ytong-aac-block-nest)) — but those run about
108 × 63 × 28 mm, an order of magnitude too small for the 8 in core this column needs. They are not
a substitute.

So, in order of preference:

1. **eBay, or a local masonry supplier willing to break a pallet.** A single block is all that is
   needed and it cuts with a hand saw.
2. **A US AAC manufacturer's sample or offcut**, asked for directly.
3. **Fall back to a poured medium** — hydrostone, or a plaster and sand mix. It wicks, it is rigid,
   and it pours to any dimension, which removes the sourcing problem entirely. It is heavier and
   cannot be re-carved, and this design has not been checked against it.

**Settle this before cutting any acrylic.** Every internal dimension in the build is set by the
core, and the column is not worth building without one.

### Two other gaps worth naming

- **Cast acrylic sheet is priced, not quoted.** The ~9 sq ft is arithmetic off the panel schedule,
  and cut-to-size pricing depends on how many separate cuts the shop makes. Get a real quote from
  [TAP](https://www.tapplastics.com), [Acme](https://www.acmeplastics.com/acrylic-sheets-cut-to-size)
  or a local shop with the panel list in hand before ordering anything else.
- **Amazon links resolve, but ASINs rot.** Every Amazon link in this file was confirmed on
  2026-09-03 to still point at the right product. None of them are irreplaceable — they are all
  commodity parts, and the Notes column is the specification, not the link.

---

## Structure

| Item | Qty | Source | Notes |
|---|---|---|---|
| Cast acrylic sheet, ¼", clear | ~9 sq ft | Commodity — hardware store, [TAP](https://www.tapplastics.com), Amazon | Column walls, base, lid, both 10 in plates, bay enclosure, portal frame, drawer, collars. Cast, not extruded: it solvent-welds and machines far better. |
| Clear acrylic tube, ¾" OD × ⅛" wall | 24" | [Canal Plastics](https://www.canalplastic.com/products/clear-colorless-acrylic-tube) — **$1.10/ft ✔**, sold in 12/24/36/72" lengths, cut to order | Three load-bearing risers. An ⅛" wall on ¾" OD gives exactly the ½" bore this design wants — confirmed on their spec table. The only specialty part in the build. |
| Acrylic offcut for collars | scrap | from the sheet | Twelve collars, 2" OD — four per riser |
| Weld-On 16 (thickened) + Weld-On 4 (thin) | 1 ea | Amazon / TAP | 4 for panel seams, 16 for collar fillets |
| Ytong / autoclaved aerated concrete, 100 mm slab | 2 | eBay, or a masonry supplier willing to break a pallet — **read the sourcing risk above** | The nest core, set back to back. Galleries carved into the outward face of each, so the nest reads from two opposite walls. Hardest part in the build to buy; settle it first. |
| Drawer rails, 4" travel ball-bearing | 1 pair | [uxcell 4" full-extension](https://www.amazon.com/uxcell-4-inch-Sections-Telescoping-Bearing/dp/B01MY4IDEK), or [Rockler mini slides](https://www.rockler.com/mini-ball-bearing-drawer-slides-select-length) | 45 mm wide, 33 lb per pair — far more than a 1 lb tray needs; chosen for the full extension, not the load. Rockler mini slides are 3/8" thick if the bay gets tight. |
| Captive thumbscrews + threaded standoffs | 2 + 2 | | Pull the drawer face plate home against its gasket. Nothing else holds the drawer in. |
| Panel-mount terminal strip | 1 | [DigiKey panel-mount terminal blocks](https://www.digikey.com/products/en/connectors-interconnects/terminal-blocks-panel-mount/425), or a [panel-mount JST-XH board](https://kc3dprint.com/products/panel-mount-jst-xh-board) | Rear of the bay. The rings and the nest fan land here; the drawer harness plugs into it. JST wire-to-wire is keyed and awkward to panel-mount, so a barrier terminal strip is the simpler answer. |
| Closed-cell gasket sheet | 1 | | Drawer face plate, outworld shell |
| Toggle clamps or knurled thumbscrews | 4–6 | | Compress the outworld gasket. Must be releasable by hand with the column in place. |
| [Aquarium-safe silicone sealant](https://www.amazon.com/Aquarium-Marine-Silicone-Sealant-Adhesive/dp/B012NOVTD2) | 1 | | Gaskets and secondary sealing |
| Opaque sleeve stock (thin ply, ABS, or vinyl wrap) | 1 | | Lifts off to view the nest |
| Ballast plate + base stock, 16" square | 1 | | Footprint at least 0.6 × overall height, mass as low in it as it will go |
| Stainless mesh, under 0.5 mm aperture | small | | Lid vent, nest ceiling exhaust port, nest wall intake port. *Tetramorium* pass anything larger. |
| Riser plug caps | 3 | | **Manual** service closure — deliberately not powered |
| Shallow tray (moat) | 1 | | The whole column stands in it. Last line of containment. |
| [byFormica PTFE Plus Fluon](https://www.amazon.com/byFormica-Insect-Escape-Prevention-Coating/dp/B0FBKSRJFG) | 1 | | Band on the inner wall below the lid rim |
| [Tetramorium immigrans colony](https://www.statesideants.com/product-page/tetramorium-immigrans) | 1 | | **$28.99 ✔.** Ordered last, after two weeks of empty running. |

## Hydration

| Item | Qty | Notes |
|---|---|---|
| Small reservoir vessel, ~131 mL | 1 | **Sized so its entire contents cannot harm the nest.** This is the safety guarantee — see below. |
| Float switch, vertical, normally-open | 1 | Reads full. Wired so a broken wire reads "not full". |
| [Kamoer NKP 12 V peristaltic pump](https://www.amazon.com/Kamoer-Peristaltic-Hydroponics-Nutrient-Analytical/dp/B07GWJ78FN) | **2** | On the base, outside the column. One water, one sugar water — different fluids cannot share a pump or a line. |
| Water supply container | 1 | Any sealed jar, on the base. Refilled every few weeks. |
| [Silicone airline tubing, 3/16"](https://www.amazon.com/Wave-point-Aquarium-Silicone-Tubing-Hydroponics/dp/B07NQV84NQ) | 1 | Supply and feed lines only — no ant tube in this design |

## Controller and sensors

| Item | Qty | Notes |
|---|---|---|
| [ESP32 DevKit (ESP-WROOM-32)](https://www.amazon.com/ESP32-DEVKIT-ESP-WROOM-32-4MB-CP2101/dp/B07F1GWJ1N) | 1 | Runs .NET nanoFramework. On the drawer. |
| [Hilitchi DS18B20 waterproof probes, 5-pack](https://www.amazon.com/Hilitchi-DS18B20-Waterproof-Temperature-Sensors/dp/B018KFX5X0) | 1 | 3 used (nest bottom, nest top, outworld), 2 spare |
| [Adafruit SHT31-D breakout](https://www.adafruit.com/product/2857) | **2** | **$13.95 ea ✔** — 0x44 outworld, 0x45 nest, see below |
| [DFRobot Gravity capacitive soil moisture sensor](https://www.amazon.com/DFROBOT-Gravity-Capacitive-Corrosion-Resistant/dp/B01GHY0N4K) | 1 | In the Ytong core. Must be on ADC1. |
| [Adafruit IR break beam, 3 mm LEDs](https://www.adafruit.com/product/2167) | **3** | **$2.95 ea ✔** — one pair per riser |
| [IRLZ44N logic-level MOSFETs, 5-pack](https://www.amazon.com/Bestol-5PCS-IRLZ44N-MOSFET-220AB/dp/B07DWYGNHC) | 1 | All 5 used: heater, refill pump, feed pump, outworld fan, nest exhaust fan |
| WS2812B / NeoPixel rings, ~12 px | 3 | Bonded into recesses under the outworld floor, one concentric with each riser mouth. Chained on one data line over ESP32 RMT. |

## Power, climate, camera

| Item | Qty | Notes |
|---|---|---|
| 12 V DC silicone heating cable, ~20 W | 1 | [OEM Heaters 12 V DC, 5 W/ft](https://www.oemheaters.com/product/6109/12v-dc-heat-cable-5-wattsfoot) at 4 ft = 20 W, or a pre-assembled 12 V silicone heating wire. Wraps the outside of the nest section — no penetration at all. |
| [Gdstime 40 mm 5 V fan](https://www.amazon.com/Gdstime-40mm-Small-Brushless-Cooling/dp/B00MYZADCY) | **2** | One in the lid, one bonded over the nest exhaust port. Both RH-gated, on different zones. |
| 12 V 3 A PSU | 1 | Single supply for the whole column |
| Buck converter 12 V to 5 V (MP1584 / LM2596) | 1 | Feeds ESP32, fans, LEDs, IR emitters |
| IR-capable ONVIF / RTSP IP camera | 1 | Consumed directly by the dashboard. 850 nm is invisible to the ants and to us. |
| Rigid foam / cork sheet | small | Thermal break between nest ceiling and bay floor |

## Passives and small parts

| Item | Qty | Notes |
|---|---|---|
| 4.7 kOhm resistor | 1 | **Mandatory** 1-Wire pull-up. Without it no probe enumerates. |
| 150 Ohm resistor | 5 | MOSFET gate resistors |
| 10 kOhm resistor | 5 | Gate pulldowns — hold every output off through boot |
| SS34 / 1N5819 Schottky diode | 4 | Flyback across both pumps and both fans |
| Perfboard + screw terminals | 1 | Sized to the drawer tray |
| JST pigtails | ~8 | So every module disconnects for service |
| Bulkhead fittings / glands | ~10 | Bay plate penetrations — all flat |
| Hookup wire 22-24 AWG, heat-shrink, zip ties | | |

## Tools

- [Vastar 16-in-1 soldering iron kit](https://www.amazon.com/Vastar-Temperature-Adjustable-Desoldering-Anti-static/dp/B0747KYF6S)
- [Performance Tool mini tubing cutter](https://www.amazon.com/Performance-Tool-W82006-Vacuum-Cutter/dp/B07324LG4X)
- Breadboard + jumper wire kit — any 830-point board with a jumper assortment. (The link that
  was here pointed at an Amazon `/clp/` landing page, which no longer resolves.)
- [SparkFun basic digital multimeter](https://www.amazon.com/Digital-Multimeter-Basic-by-Sparkfun/dp/B00NBVO2EU)
- Step drill bit (clean holes in acrylic without cracking), clamps, spoon or loop tool for Ytong

---

## Why the parts are what they are

### Serviceability: three places to work, none of them the nest

The nest is never opened once the colony is in, so everything that might need attention has to be
reachable from outside it. That splits into three:

**The drawer** carries what has firmware or a failure mode worth swapping: the ESP32 and the
perfboard with all five MOSFET stages, terminal blocks and passives. It is a tray on rails behind a
gasketed face plate on two captive thumbscrews. Pull it and nothing structural moves, nothing in the
ant path is touched, and no weld is disturbed. The harness has enough service loop to reach the
detent while still made up.

**Bonded in the bay** are the three LED rings in their recesses, the nest exhaust fan over its port,
and the bulkhead connector strip. These are dumb and aligned to holes in the plates; re-aligning one
is a worse job than replacing it in place.

**On the base**, outside the column, are both peristaltic pumps and the supply jar. Plumbing does not
belong in a four-inch bay, and out there it is reachable without opening anything.

The outworld follows the same rule from the other side: its floor is welded to the risers and cannot
move, so the four walls and lid lift off that floor as one unit on a clamped gasket. Cleaning is plug
the risers, unclamp, lift, wash, reseat. No weld ever moves and the ring-windows stay put.

### Passive hydration, and why the reservoir volume is the real safety feature

Borrowed from the way [Tar Heel Ants](https://tarheelants.com/) build their nests: a reservoir wicks
into the nest and is topped up every few weeks rather than sprayed on demand.

The pump fills **only the reservoir**, and the reservoir wicks into the Ytong core. Because its
capacity is smaller than a dose that could harm the colony, **no failure of the pump matters** —
worst case it overfills a vessel that is harmless when full. The guarantee lives in the geometry,
where an electrical fault cannot reach it.

A second benefit falls out for free: refilling keys off the **float switch**, not the moisture probe,
so a dead soil probe degrades the system to "passively hydrated, unmonitored" instead of stopping
hydration altogether.

### No gate

A powered gate between the colony and its food would be the largest single point of failure in the
system: a servo that jams closed cuts the colony off from food and water, unattended, for as long as
it takes someone to notice — and no firmware can distinguish a gate that is closed from one that is
stuck. Three always-open risers give redundancy against one fouling with debris or a dead ant.
Service closure is a manual plug cap, which cannot fail shut while nobody is watching.

### Ytong nest core with carved chambers

Two 100 mm slabs of aerated concrete cut to the internal section and set back to back, with galleries
carved into the outward face of each — so the nest reads from two opposite walls, and moisture is
uniform rather than the soil probe reading one arbitrary point of loose fill. Rigid galleries cannot
subside. Ytong also wicks, which is what makes the passive reservoir work at all.

### An opaque sleeve over the nest

Bare clear acrylic leaves the nest permanently lit by room light. Ants dig away from light, so that
produces both chronic stress and a nest you cannot see into — they tunnel against whichever face is
darkest. A removable opaque sleeve gives them darkness by default and gives you a full view on demand.

### Nest ventilation without ever opening the nest

Three ½ in bores are not airflow. A Ytong core above 80% RH held at 27 °C is a mould risk the nest
humidity sensor can see and nothing else could act on. A mesh-screened exhaust port through the nest
ceiling pairs with a passive mesh intake low on the nest wall behind the sleeve, giving cross-flow
bottom to top along the thermal gradient. The fan is bonded over the port on the bay side. Inside the
nest there is only stainless mesh, which has nothing to fail.

### A capped expansion port

*Tetramorium immigrans* was chosen precisely because it reaches 1,000–10,000 workers. A capped
bulkhead port goes into the nest section during construction, while the box is empty, rather than
being cut into a populated column later.

### Two SHT31s, not one

The ADDR pin gives 0x44 or 0x45, so both share one I2C bus for one extra part. **SHT31-A** reads
outworld air, which is what the lid fan can act on; **SHT31-B** reads nest airspace, where mould risk
lives. Catching a moist-substrate / dry-air mismatch needs both.

### Three WS2812B rings

One per riser mouth, in a recess under the outworld floor, shining **up** through a frosted annulus
around each mouth. No ant contact, no moisture contact. Each riser owns an R/G/B channel whose
brightness tracks that riser's traffic, closing a behavioural loop at the point where an ant picks a
tube.

Ants are most sensitive to UV and blue-green and effectively **blind to deep red**, so the three
channels are three very different stimulus strengths rather than three equivalent colours. The
firmware **rotates the riser-to-channel mapping at local midnight and logs it**, so "they prefer
green" can be separated from "they prefer the tube nearest the food dish". Without that control the
experiment is uninterpretable.
