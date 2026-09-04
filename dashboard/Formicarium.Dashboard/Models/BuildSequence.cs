namespace Formicarium.Dashboard.Models;

public sealed record BuildStep(int Number, string Text, string Note);

public sealed record BuildPhase(string Name, IReadOnlyList<BuildStep> Steps);

/// <summary>
/// The build order, held as data rather than markup so the checklist, the progress count and the
/// step numbers all come from one place.
///
/// The order is chosen so that nothing has to be undone. The structural spine — both plates, the
/// three risers and the portal frame — is welded flat on the bench where it can be clamped square,
/// before it is ever bonded to the nest. Electronics are proved on a breadboard before anything is
/// bonded. The colony is ordered last, after the column has already run empty for two weeks.
/// </summary>
public static class BuildSequence
{
    public static IReadOnlyList<BuildPhase> Phases { get; } = Number(
    [
        ("Fabrication",
        [
            ("Cut the eight column panels, both bay plates, the base, the lid and the four bay-enclosure panels from ¼ in cast acrylic.",
             "Cast, not extruded — it solvent-welds and machines far better. Keep every edge square; a weld needs mating faces, not gaps."),

            ("Cut the two portal stiles (½ × 4 in) and the header for the fourth bay face.",
             "These close the load path on the side that has no riser. They are structure, not trim, and their width is what sets the drawer opening at 3¾ in."),

            ("Dry-fit the whole stack with tape before any solvent touches it.",
             "Every dimension error is free to fix now and permanent afterwards."),

            ("Drill both bay plates for the three risers, on the midlines of three faces, 3¼ in from the axis, to the tube OD.",
             "Step drill, slow speed, backing board. Both plates must be drilled together or the risers will not stand plumb."),

            ("Cut twelve collar washers, 2 in OD, from offcut.",
             "Four per riser: one against each face of each plate. These are the bearing surfaces, not decoration."),

            ("Machine the three ring recesses into the underside of the outworld floor, concentric with the riser holes.",
             "The LED rings sit in these and shine up through a frosted annulus around each mouth. Nothing lit ever enters the arena."),

            ("Cut the nest-ceiling exhaust port and the low nest-wall intake port, and screen both with stainless mesh.",
             "Under 0.5 mm aperture. This is the only ventilation the nest will ever get and it cannot be added later."),

            ("Drill the bay plates for cable glands and the nest wall for the capped expansion port.",
             "All flat surfaces — that is the point of the square section."),

            ("Rebate the outworld floor for the shell gasket and fit the toggle clamps.",
             "The outworld has to lift off for cleaning without disturbing the riser welds underneath it."),

            ("Solvent-weld the nest section: four walls onto the base, Weld-On 4.",
             "Clamp square and let it cure fully. This is the containment boundary."),

            ("Weld the three risers through both bay plates: collar each face, fillet with Weld-On 16, clamp the assembly square on a flat bench.",
             "This is the spine. Build it flat, off the column, where both plates can be clamped parallel — it is the one joint that carries load and the one that cannot be allowed to leak."),

            ("Weld the two portal stiles and the header between the plates on the fourth face.",
             "With three risers on three faces the load resultant sits off-axis; the portal takes the fourth corner of it and frames the drawer opening."),

            ("Weld the bay enclosure — three closed panels and the drawer-face panel — inside the spine.",
             "These carry no load at all. That is exactly why one of them can be a hole."),

            ("Fit the drawer rails to the bay floor and check a blank tray runs full travel and stops on its detent.",
             "Prove the travel before anything is soldered to the tray. Full travel means every connector is reachable with the tray still supported."),

            ("Build the drawer: tray, gasketed face plate, two captive thumbscrews into threaded standoffs in the stiles.",
             "The face plate seals the bay; the thumbscrews pull it home against the gasket. Nothing else holds the drawer in."),

            ("Bond the thermal break to the underside of the bay floor.",
             "The heater is below and heat rises; the bay should not soak."),

            ("Bond the spine assembly onto the nest section.",
             "Nest ceiling to nest walls, continuous fillet. After this the column is one piece from the base to the outworld floor."),

            ("Halve the AAC block, trim both pieces to 10.5 × 7.75 in, and carve the galleries into the outward face of each.",
             "One standard 24 × 8 × 4 in block makes both slabs. A spoon works. Back to back they read from two opposite walls, so every chamber you carve is a chamber you can watch."),

            ("Cut the reservoir well and set the core so one foot of it stands in the well.",
             "That foot is the wick. Nothing pumps into the nest — the core draws."),

            ("Build the ballasted base and seat the column in its pocket.",
             "Footprint at least 0.6 × overall height, then put the mass as low in it as it will go."),

            ("Make the opaque sleeve so it lifts clear of the nest section.",
             "Dark by default, full view on demand.")
        ]),

        ("Electronics on the bench",
        [
            ("Breadboard the ESP32 with power only. Confirm 12 V at the rail, 5 V out of the buck, 3V3 on the board.",
             "Nothing else connected. A bad buck converter downstream of everything is a miserable diagnosis."),

            ("Wire the 1-Wire bus: GPIO16 and GPIO17 both to DQ, 4.7 kΩ to 3V3.",
             "RX and TX tied together. nanoFramework runs 1-Wire over a UART, not a bit-banged pin."),

            ("Flash the firmware and read the boot log for three ROM IDs.",
             "No IDs almost always means the pull-up, not the probes."),

            ("Identify each probe by warming it in a closed hand, and record its ID in DeviceConfig.",
             "Never rely on enumeration order. Getting it wrong is silent: the controller heats to the wrong probe and either cooks the nest or never reaches setpoint."),

            ("Add both SHT31s and set one ADDR pin so they land on 0x44 and 0x45.",
             "Only one responding means the ADDR pin is still floating."),

            ("Add the soil probe on GPIO34. Record raw counts in air and in saturated Ytong.",
             "Those two numbers are the calibration. Higher counts mean drier."),

            ("Add the float switch on GPIO32 and confirm it reads LOW with the float up.",
             "Verify this before the pump can ever be enabled."),

            ("Add the five MOSFET stages with gate resistors and pulldowns, and switch each into a dummy load.",
             "Confirm every output is off during reset, not just after boot. A missing 10 kΩ pulldown shows up as an output on while the ESP32 is held in reset."),

            ("Add the three beam pairs and check each counts independently.",
             "Wave a card. Watch the debounced ratio as well as the raw count."),

            ("Add the three WS2812B rings and confirm ring order matches riser order.",
             "Chain order is not riser order unless you make it so. All three lighting the same colour means the chain is out of sequence.")
        ]),

        ("Assembly",
        [
            ("Transfer the breadboard circuit to perfboard sized to the drawer tray, with screw terminals and JST pigtails.",
             "Every module has to disconnect without cutting anything."),

            ("Bond the three LED rings into their recesses in the outworld floor and the exhaust fan over its port in the bay floor, and pigtail both to the bulkhead strip.",
             "These stay in the bay permanently. They are aligned to holes in the plates, and re-aligning them is a worse job than replacing them in place."),

            ("Mount the ESP32 and perfboard on the drawer tray and land its harness on the bulkhead strip.",
             "The drawer carries what has firmware or a failure mode worth swapping. Nothing else."),

            ("Mount both peristaltic pumps and the supply jar on the base, outside the column, and run their lines up.",
             "Plumbing does not belong in a 4 in bay. On the base they are reachable without touching anything else."),

            ("Wrap the heat cable around the outside of the nest section and cover it with the sleeve.",
             "No penetration, no moisture on the heater, no ant contact."),

            ("Run the nest bundle down through the bay floor gland and the outworld bundle up through the ceiling gland.",
             "Service loop and a clamp at both ends of every run, long enough to pull the drawer to its detent with the harness still made up."),

            ("Seat the outworld shell on its gasket and close the toggle clamps.",
             "Hand tight and even. You are compressing a gasket, not making a press fit."),

            ("Fit the lid with its stainless mesh, and lay the Fluon band on the inner wall below the rim.",
             "Fluon is the only thing standing between you and loose Tetramorium. Do not skip it."),

            ("Slide the drawer home and pull it up on both thumbscrews.",
             "Check the face-plate gasket compresses evenly and the tray runs free afterwards."),

            ("Fill the supply jar, power up, and let a refill cycle complete.",
             "Watch for the reservoir to read full and the pump to stop on its own.")
        ]),

        ("Commissioning",
        [
            ("Run the whole column empty for two weeks. Confirm the nest holds 79–83 °F overnight, hydration stabilises rather than trending, and no fault latches.",
             "The failures that matter in this system are slow ones. Two weeks is the shortest window in which a slow failure becomes visible."),

            ("Set the lighting mode to Off and log several days of baseline traffic before introducing any light.",
             "You cannot recover a baseline after the fact."),

            ("Introduce the colony through the outworld lid, then leave it alone for a week.",
             "Order the colony only once the column has already proved itself.")
        ])
    ]);

    public static int StepCount { get; } = Phases.Sum(p => p.Steps.Count);

    private static IReadOnlyList<BuildPhase> Number(
        (string Name, (string Text, string Note)[] Steps)[] source)
    {
        var number = 0;

        return source
            .Select(phase => new BuildPhase(
                phase.Name,
                phase.Steps.Select(step => new BuildStep(++number, step.Text, step.Note)).ToList()))
            .ToList();
    }
}
