# GPIO and Wiring Map

Canonical pin assignment for the Formicarium controller (ESP32-WROOM-32 DevKit,
.NET nanoFramework). **This file, `firmware/Formicarium.Controller/Core/Config/PinMap.cs`,
and the pin-out diagram in `docs/build-guide.html` must always agree.** A mismatch between
the three is the most likely latent bug in this repo.

## Constraints that drove these choices

These are not arbitrary preferences. Every one of them will silently break the build if
ignored:

| Constraint | Consequence |
|---|---|
| GPIO **6–11** are wired to the SPI flash | Unusable. Touching them bricks the boot. |
| GPIO **34–39** are input-only and have **no internal pull-ups** | Fine for analog and for pushed signals; wrong for an open-collector sensor unless you add an external pull-up. |
| **ADC2 is unavailable whenever WiFi is active** | The capacitive soil probe *must* live on ADC1 (GPIO 32–36, 39). This is the single most common ESP32 sensor bug. GPIO 25/26/27 are ADC2 but are perfectly fine as *digital* outputs. |
| Strapping pins **0, 2, 5, 12, 15** are sampled at boot | Avoided for anything load-bearing. GPIO 2 is used only for the onboard status LED, which is harmless. |
| GPIO **1/3** are UART0 | Reserved for the console and flashing. |
| nanoFramework's ESP32 1-Wire is **UART-backed, not bit-banged** | It needs a COM port's RX *and* TX pins, tied together to the DS18B20 DQ line. See the wiring note below — this is the easiest detail in the whole build to get wrong. |

## Assignments

| Function | GPIO | Kind | Notes |
|---|---|---|---|
| SHT31 ×2 — SDA | 21 | I2C1 | `Configuration.SetPinFunction(21, DeviceFunction.I2C1_DATA)` |
| SHT31 ×2 — SCL | 22 | I2C1 | `...I2C1_CLOCK`. 100 kHz. Addresses 0x44 (outworld) / 0x45 (nest). |
| DS18B20 bus — RX | 16 | COM3_RX | Tied together with TX to the DQ line |
| DS18B20 bus — TX | 17 | COM3_TX | 3 probes share one bus, addressed by ROM ID |
| Soil moisture | 34 | ADC1_CH6 | Input-only pin — correct for analog. **Must be ADC1.** |
| IR beam RX — riser 1 | 27 | GPIO in | Internal pull-up, interrupt on falling edge |
| IR beam RX — riser 2 | 14 | GPIO in | Internal pull-up, interrupt on falling edge |
| IR beam RX — riser 3 | 13 | GPIO in | Internal pull-up, interrupt on falling edge |
| IR emitter enable | 19 | GPIO out | Powers all three emitters together |
| Heater | 25 | GPIO out | IRLZ44N low-side. 150 Ω gate, 10 kΩ pulldown. 12 V DC cable. |
| Refill pump (water → reservoir) | 26 | GPIO out | IRLZ44N + SS34 flyback. Fills the reservoir; never reaches the nest. |
| Feed pump (syrup → outworld) | 4 | GPIO out | IRLZ44N + SS34 flyback diode |
| Fan (outworld) | 33 | GPIO out | IRLZ44N + SS34 flyback diode |
| WS2812B ring data | 23 | RMT | All three riser-mouth rings on **one** data line, chained |
| Reservoir float switch | 32 | GPIO in | Internal pull-up, switch to ground. **LOW = full.** See polarity note below. |
| Status LED | 2 | GPIO out | Onboard DevKit LED — heartbeat and fault blink |

Free and unused: 0, 1, 3, 5, 12, 15, 18, 35, 36, 39.

## The float-switch polarity note

The reservoir switch is wired to ground and read against the internal pull-up, so a risen float
reads LOW and means *full*. That direction is chosen deliberately, not for convenience:

- A **broken wire** or unplugged connector floats HIGH, which reads "not full", which asks for a
  refill. That refill is bounded twice over — by `RefillMaxRunSeconds` and by the fact that the
  reservoir physically cannot hold a harmful dose — so the failure is a non-event.
- The **opposite** polarity would read "full" forever on a broken wire, silently stop hydrating
  the colony, and look completely healthy on the dashboard. That is the failure nobody notices
  until it is too late.

Fail-safe here means "fails toward a bounded action", not "fails toward doing nothing", because
doing nothing is what kills a colony slowly.

## The 1-Wire wiring note

nanoFramework drives 1-Wire through a UART, so the bus is **not** a single GPIO:

```
   GPIO16 (COM3_RX) ---+
                       +---- DQ  ---> all three DS18B20 probes (parallel)
   GPIO17 (COM3_TX) ---+
                            |
                          4.7 kΩ
                            |
                          3V3
```

TX and RX are tied to the same node. The 4.7 kΩ pull-up to 3V3 is mandatory — without it
the bus reads all-ones and every probe enumerates as absent. All three probes share this
one bus and are told apart by their 64-bit ROM IDs, which are read once during bring-up and
recorded in `Setpoints`.

## Cable routing — the payoff of putting Electronics in the middle of the stack

The electronics bay sits between the Colony (below) and the Outworld (above), so every run exits
through a flat plate and reaches its destination in inches rather than feet. Since the column is
a square section, *every* surface a cable or riser passes through is flat, which is what lets
every penetration use a proper bulkhead seat or a clamped solvent weld:

**Through the bay's bottom plate, into the Colony:**
- DS18B20 — nest bottom
- DS18B20 — nest top
- SHT31-B (nest airspace, 0x45)
- Capacitive soil probe (into the Ytong core)
- Water supply line to the reservoir

**Through the bay's top plate, into the Outworld:**
- DS18B20 — outworld
- SHT31-A (outworld air, 0x44)
- Fan
- Feed line (sugar water)
- WS2812B rings ×3 — these do **not** penetrate at all; they sit inside the bay and shine
  up through frosted ring-windows in the outworld floor
- IR beam pairs ×3 — mounted at the riser mouths, wiring dropping back into the bay

**Not penetrating anything:**
- The 12 V heat cable is wrapped around the **outside** of the Colony section under an
  insulating sleeve. No seal, no moisture on the heater, no ant contact.
- The three WS2812B rings sit inside the bay and shine **up** through frosted ring-windows in
  the outworld floor. Light crosses the boundary; wiring does not.

Every penetration goes through a **flat** surface with a bulkhead fitting, a gland, or a
solvent-welded collar. An O-ring cannot seat on a curve, and a riser welded to a curved wall
cannot be clamped while it cures — which is the main reason the column is a square section
rather than a tube.

## Power

```
  12 V 3 A PSU ──┬── heat cable (via MOSFET)
                 ├── refill pump (via MOSFET)
                 ├── feed pump (via MOSFET)
                 └── buck converter 12 V → 5 V ──┬── ESP32 VIN
                                                 ├── fan (via MOSFET)
                                                 ├── WS2812B rings
                                                 └── IR emitters (via enable)
```

All grounds common. The ESP32's 3V3 rail feeds only the logic-level sensors (SHT31, DS18B20
pull-up, soil probe, beam receivers, float switch) — never a motor or the heater.

## Cross-checking this file

`Core/Config/PinMap.cs` and the pin-out diagram in `docs/build-guide.html` must agree with the
table above. Before wiring anything, read all three and confirm they match: a disagreement here
produces a system that boots, reports plausible numbers, and heats to the wrong probe.
