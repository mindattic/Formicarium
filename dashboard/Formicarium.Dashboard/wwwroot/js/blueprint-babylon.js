/*
   The interactive section drawing — Babylon.js engine.

   The other half of blueprint.js's tab switcher (see blueprint-three.js for the first). Same
   dimensioned geometry, same assembly legend, same explode/drawer/sleeve/isolate controls — built
   idiomatically for Babylon rather than translated line-for-line, so the comparison is between two
   engines doing their own best work on the same drawing, not between one native implementation and
   one port. The concrete difference that follows from that: camera orbit here is Babylon's own
   ArcRotateCamera rather than the hand-rolled spherical maths the Three engine needs (Three's UMD
   build does not carry OrbitControls).

   Babylon is served from wwwroot/lib rather than a CDN for the same reason as Three: this is a LAN
   appliance, and the page most likely to be open with no internet nearby must not need it. It is
   only fetched the first time this tab is actually opened.
*/

const BABYLON_URL = "lib/babylon.js";

let babylonRequest = null;

function loadBabylon() {
    if (window.BABYLON) {
        return Promise.resolve(window.BABYLON);
    }

    babylonRequest ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = BABYLON_URL;
        script.onload = () => (window.BABYLON ? resolve(window.BABYLON) : reject(new Error("babylon.js loaded but defined no global")));
        script.onerror = () => reject(new Error("babylon.js could not be fetched"));
        document.head.appendChild(script);
    });

    return babylonRequest;
}

const ASSEMBLIES = [
    { id: "lid", name: "Vented lid", note: "mesh <0.5 mm · Fluon rim", color: 0x8896a6 },
    { id: "out", name: "Outworld", note: "8 in · lifts off its floor", color: 0x6f9ec4 },
    { id: "riser", name: "Risers ×3", note: "¾ in OD · load-bearing", color: 0xd98324 },
    { id: "portal", name: "Portal frame", note: "fourth face · frames the drawer", color: 0xb56b2a },
    { id: "drawer", name: "Electronics drawer", note: "slides out · carries no load", color: 0xc9a227 },
    { id: "bay", name: "Bay enclosure", note: "5¼ in sq · non-structural", color: 0x7a6a55 },
    { id: "nest", name: "Nest", note: "12 in · two Ytong slabs", color: 0xb0a68f },
    { id: "res", name: "Reservoir", note: "131 mL · wicks up", color: 0x4f86d6 },
    { id: "base", name: "Ballasted base", note: "16 in sq", color: 0x5d6b7a }
];

export async function mount(root) {
    let BABYLON;

    try {
        BABYLON = await loadBabylon();
    } catch (error) {
        root.innerHTML =
            '<div class="build-fallback"><strong>The 3D view could not load.</strong> ' +
            "Nothing is lost — every dimension it shows is also in the elevation, the bay plan and " +
            "the key figures below.</div>";
        return { dispose() {} };
    }

    root.innerHTML =
        '<canvas class="build-stage" aria-label="Interactive 3D section drawing of the formicarium column"></canvas>' +
        '<div class="build-viewer-side">' +
        '<div class="build-key-head"><div class="build-eyebrow">Assemblies</div>' +
        '<button type="button" class="build-key-reset danger" data-act="reset-select" disabled>Reset</button></div>' +
        '<ul class="build-key">' +
        ASSEMBLIES.map(a =>
            '<li><button type="button" aria-pressed="false" data-id="' + a.id + '">' +
            '<i style="background:#' + a.color.toString(16).padStart(6, "0") + '"></i>' +
            "<span>" + a.name + "<small>" + a.note + "</small></span></button></li>").join("") +
        "</ul>" +
        '<div class="build-viewer-controls">' +
        '<button type="button" data-act="drawer" aria-pressed="false">Drawer</button>' +
        '<button type="button" data-act="explode" aria-pressed="false">Explode</button>' +
        '<button type="button" data-act="sleeve" aria-pressed="true">Sleeve</button>' +
        '<button type="button" data-act="recentre">Recentre</button>' +
        "</div></div>";

    const canvas = root.querySelector(".build-stage");
    const keyList = root.querySelector(".build-key");
    const controls = root.querySelector(".build-viewer-controls");
    const resetBtn = root.querySelector('[data-act="reset-select"]');

    const engine = new BABYLON.Engine(canvas, true, { alpha: true, preserveDrawingBuffer: false }, true);
    engine.setHardwareScalingLevel(1 / Math.min(window.devicePixelRatio || 1, 2));

    const scene = new BABYLON.Scene(engine);
    scene.clearColor = new BABYLON.Color4(0, 0, 0, 0);

    // Right-handed to match the same (x, y, z) placements the Three engine uses — every asymmetric
    // fixture (the reservoir corner, the dish, which riser gets which channel colour) then lands in
    // the same place under both engines instead of mirroring.
    scene.useRightHandedSystem = true;

    const groups = {};
    const meshes = [];

    // Inches, scaled so the whole column is a comfortable unit height. Identical to the Three
    // engine's constants — this is the same drawing, not a similar one.
    const S = 0.11;
    const OUT = 8.5 * S;      // column external section
    const PLATE = 10 * S;     // bay plates — they overhang, and the overhang is the riser flange land
    const BAY = 5.25 * S;     // bay enclosure, a waist inside the column line
    const RISER_R = 3.25 * S; // riser centres, on the midlines of three faces

    function color3(hex) {
        return new BABYLON.Color3(((hex >> 16) & 255) / 255, ((hex >> 8) & 255) / 255, (hex & 255) / 255);
    }

    function material(color, opacity) {
        // StandardMaterial rather than PBRMaterial: with no environment texture in this scene,
        // Babylon's PBR path still adds enough ambient reflectance to render a near-black albedo
        // (the nest sleeve, 0x1b2430) as a washed-out slate grey — a known PBR-without-IBL
        // calibration pitfall. StandardMaterial's plain Phong response answers directly to
        // diffuseColor under the scene's directional/hemispheric lights, with nothing to recalibrate.
        const mat = new BABYLON.StandardMaterial("m" + meshes.length, scene);
        mat.diffuseColor = color3(color);
        mat.specularColor = new BABYLON.Color3(0.12, 0.12, 0.12);
        mat.alpha = opacity;

        // Babylon's blended materials skip the depth write by default; Three's never do, blended
        // or not. Without this, a nearer translucent shell (the nest sleeve, the outworld shell)
        // fails to occlude whatever sits inside it, and the inner geometry paints over the shell
        // instead of showing faintly through it.
        mat.forceDepthWrite = true;

        return mat;
    }

    function box(w, h, d, color, opacity) {
        const mesh = BABYLON.MeshBuilder.CreateBox("b" + meshes.length, { width: w, height: h, depth: d }, scene);
        mesh.material = material(color, opacity);
        return mesh;
    }

    function cylinder(r, h, color, opacity, segments = 20) {
        const mesh = BABYLON.MeshBuilder.CreateCylinder("c" + meshes.length, { diameter: r * 2, height: h, tessellation: segments }, scene);
        mesh.material = material(color, opacity);
        return mesh;
    }

    function group(id, y) {
        const g = new BABYLON.TransformNode(id, scene);
        g.position.y = y;
        g.metadata = { baseY: y };
        groups[id] = g;
        return g;
    }

    function add(g, mesh, id, x = 0, y = 0, z = 0) {
        mesh.position.set(x, y, z);
        mesh.parent = g;
        mesh.metadata = { assembly: id };
        meshes.push(mesh);
        return mesh;
    }

    // --- heights, bottom to top ------------------------------------------------
    const baseTop = 0.5 * S;
    const nestH = 12 * S;
    const nestTop = baseTop + nestH;            // 12.5
    const bayClear = 4 * S;
    const ceilingY = nestTop + 0.125 * S;       // nest ceiling plate, ¼ in
    const bayFloor = nestTop + 0.25 * S;
    const bayCeil = bayFloor + bayClear;
    const floorY = bayCeil + 0.125 * S;         // outworld floor plate, ¼ in
    const outFloor = bayCeil + 0.25 * S;
    const outH = 8 * S;
    const outTop = outFloor + outH;

    // --- base ------------------------------------------------------------------
    add(group("base", 0), box(16 * S, 1 * S, 16 * S, 0x5d6b7a, 1), "base");

    // --- nest ------------------------------------------------------------------
    const gNest = group("nest", baseTop + nestH / 2);
    add(gNest, box(OUT, nestH, OUT, 0x9fc2d8, 0.2), "nest");
    // Two Ytong slabs back to back: galleries face outward, so the nest reads from two walls.
    add(gNest, box(3.7 * S, 10.5 * S, 7.6 * S, 0xb0a68f, 0.95), "nest", -1.9 * S, -0.4 * S, 0);
    add(gNest, box(3.7 * S, 10.5 * S, 7.6 * S, 0xb0a68f, 0.95), "nest", 1.9 * S, -0.4 * S, 0);

    const sleeve = box(OUT + 0.14 * S, nestH, OUT + 0.14 * S, 0x1b2430, 0.9);
    sleeve.metadata = { assembly: "nest" };
    sleeve.parent = gNest;

    const gRes = group("res", baseTop + nestH / 2);
    add(gRes, box(2 * S, 2 * S, 2 * S, 0x4f86d6, 0.85), "res", -2.2 * S, -nestH / 2 + 1.1 * S, -2.2 * S);

    // --- bay plates ------------------------------------------------------------
    // Both plates overhang the column by ¾ in. That overhang is what the riser collars bear on,
    // and what the outworld gasket is clamped against.
    const gPlates = group("riser", 0);
    add(gPlates, box(PLATE, 0.25 * S, PLATE, 0xa9bccb, 0.55), "riser", 0, ceilingY, 0);
    add(gPlates, box(PLATE, 0.25 * S, PLATE, 0xa9bccb, 0.55), "riser", 0, floorY, 0);

    // --- risers: three faces, load-bearing ------------------------------------
    const riserLen = (floorY + 0.25 * S) - (ceilingY - 0.25 * S);
    const riserY = (ceilingY + floorY) / 2;
    const riserAt = [[-RISER_R, 0], [RISER_R, 0], [0, -RISER_R]];

    riserAt.forEach(([x, z]) => {
        add(gPlates, cylinder(0.375 * S, riserLen, 0xd98324, 0.8, 18), "riser", x, riserY, z);

        // Four collars per riser: one against each face of each plate. The weld is in bearing,
        // not just sealing, which is the whole reason the load can come down this path.
        [ceilingY - 0.2 * S, ceilingY + 0.2 * S, floorY - 0.2 * S, floorY + 0.2 * S].forEach(y => {
            add(gPlates, cylinder(1 * S, 0.12 * S, 0xd98324, 0.95, 18), "riser", x, y, z);
        });
    });

    // --- portal frame: the fourth face ----------------------------------------
    const gPortal = group("portal", 0);
    // ½ in stiles standing exactly at the edges of the 3¾ in opening: any wider and they eat
    // into the gap the tray has to pass through.
    [-2.25 * S, 2.25 * S].forEach(x => {
        add(gPortal, box(0.5 * S, bayClear, 0.25 * S, 0xb56b2a, 0.9), "portal", x, bayFloor + bayClear / 2, BAY / 2);
    });
    add(gPortal, box(BAY, 0.6 * S, 0.25 * S, 0xb56b2a, 0.9), "portal", 0, bayCeil - 0.3 * S, BAY / 2);

    // --- bay enclosure: three closed faces, one open --------------------------
    const gBay = group("bay", 0);
    const bayY = bayFloor + bayClear / 2;
    add(gBay, box(BAY, bayClear, 0.15 * S, 0x7a6a55, 0.45), "bay", 0, bayY, -BAY / 2);
    [-BAY / 2, BAY / 2].forEach(x => {
        add(gBay, box(0.15 * S, bayClear, BAY, 0x7a6a55, 0.45), "bay", x, bayY, 0);
    });

    // --- the drawer ------------------------------------------------------------
    const gDrawer = group("drawer", 0);
    add(gDrawer, box(3.5 * S, 0.2 * S, 4.5 * S, 0xc9a227, 0.95), "drawer", 0, bayFloor + 0.35 * S, -0.2 * S);
    add(gDrawer, box(4.6 * S, 2.9 * S, 0.25 * S, 0xc9a227, 0.9), "drawer", 0, bayFloor + 1.6 * S, BAY / 2 + 0.2 * S);
    // the controller board riding on it
    add(gDrawer, box(2.2 * S, 0.5 * S, 1.1 * S, 0x2f6f4f, 1), "drawer", -0.6 * S, bayFloor + 0.7 * S, -0.6 * S);

    // --- outworld --------------------------------------------------------------
    const gOut = group("out", 0);
    add(gOut, box(OUT, outH, OUT, 0x6f9ec4, 0.16), "out", 0, outFloor + outH / 2, 0);

    // ring-windows: a frosted annulus around each riser mouth, lit from the recess below
    const ringColors = [0xd4614a, 0x7fb069, 0x5b8dd9];
    riserAt.forEach(([x, z], i) => {
        const ring = cylinder(0.72 * S, 0.1 * S, ringColors[i], 1, 22);
        ring.material.emissiveColor = color3(ringColors[i]).scale(0.6);
        add(gOut, ring, "out", x, outFloor + 0.05 * S, z);
    });

    const dish = cylinder(0.8 * S, 0.35 * S, 0xdedad0, 1, 22);
    add(gOut, dish, "out", 1.4 * S, outFloor + 0.2 * S, 1.6 * S);

    // --- lid -------------------------------------------------------------------
    add(group("lid", 0), box(OUT + 0.3 * S, 0.5 * S, OUT + 0.3 * S, 0x8896a6, 1), "lid", 0, outTop + 0.25 * S, 0);

    // --- lighting ----------------------------------------------------------------
    const ambient = new BABYLON.HemisphericLight("ambient", new BABYLON.Vector3(0, 1, 0), scene);
    ambient.intensity = 0.66;
    ambient.diffuse = new BABYLON.Color3(1, 1, 1);
    ambient.groundColor = new BABYLON.Color3(1, 1, 1);

    const key = new BABYLON.DirectionalLight("key", new BABYLON.Vector3(-4, -7, -6), scene);
    key.intensity = 0.8;

    const fill = new BABYLON.DirectionalLight("fill", new BABYLON.Vector3(5, -2, 4), scene);
    fill.intensity = 0.32;
    fill.diffuse = new BABYLON.Color3(0.67, 0.8, 1);

    // --- camera ------------------------------------------------------------------
    // Frame the whole column: the target is its vertical midpoint, not the nest's, or the lid
    // falls outside the frustum on a wide canvas.
    const HOME = { alpha: Math.PI / 4, beta: Math.PI / 2 - 0.18, dist: 5.6, explodedDist: 8.0 };
    const target = new BABYLON.Vector3(0, (outTop + 0.5 * S) / 2, 0);

    const camera = new BABYLON.ArcRotateCamera("cam", HOME.alpha, HOME.beta, HOME.dist, target, scene);
    camera.fov = 38 * Math.PI / 180;
    // Babylon's camera defaults to a 1..10000 clip range, which starves the depth buffer of
    // precision at this scene's ~0.1-unit scale and z-fights surfaces a few hundredths apart —
    // the nest sleeve sitting just 0.014 units outside its inner box, for one. Match Three's
    // near/far exactly rather than just tightening it, so both engines get the same precision.
    camera.minZ = 0.1;
    camera.maxZ = 100;
    camera.lowerRadiusLimit = 2.6;
    camera.upperRadiusLimit = 11;
    camera.lowerBetaLimit = 0.32;
    camera.upperBetaLimit = 2.07;
    camera.panningSensibility = 0; // orbit and zoom only, matching the Three engine's interaction
    camera.attachControl(canvas, true);

    engine.runRenderLoop(() => scene.render());

    const onResize = () => engine.resize();
    window.addEventListener("resize", onResize);

    // --- view state ------------------------------------------------------------
    let exploded = false, drawerOut = false, sleeveOn = true, isolated = null;

    meshes.forEach(m => { m.metadata.baseOpacity = m.material.alpha; });
    sleeve.metadata.baseOpacity = sleeve.material.alpha;

    // Chosen so the exploded stack still fits the frame at HOME.explodedDist — an explode that
    // pushes the lid out of shot is a worse drawing than no explode at all.
    const SPREAD = { lid: 0.95, out: 0.68, riser: 0.35, portal: 0.35, bay: 0.35, drawer: 0.35, nest: 0, res: -0.3, base: 0 };

    // Zoom to fit, the way a CAD viewer does: exploding is a request to see more, not to have the
    // top of the column leave the frame.
    const fitDistance = () => (exploded ? HOME.explodedDist : HOME.dist);

    function applyState() {
        Object.keys(groups).forEach(id => {
            groups[id].position.y = groups[id].metadata.baseY + (exploded ? SPREAD[id] : 0);
        });

        // The drawer is the one thing that moves along its own axis rather than the stack axis,
        // which is the point of it.
        gDrawer.position.z = drawerOut || exploded ? 5.4 * S : 0;

        sleeve.setEnabled(sleeveOn);

        meshes.forEach(m => {
            const dim = isolated && m.metadata.assembly !== isolated;
            m.material.alpha = dim ? 0.06 : m.metadata.baseOpacity;
        });
    }

    // Selecting an assembly and clearing that selection are separate concerns from framing the
    // shot — Recentre resets the camera too, which is a bigger reset than someone who just wants
    // to drop the isolate and keep the angle they were looking from is asking for.
    const updateResetButton = () => { resetBtn.disabled = isolated === null; };

    const onKeyClick = e => {
        const btn = e.target.closest("button");
        if (!btn) return;
        isolated = isolated === btn.dataset.id ? null : btn.dataset.id;
        keyList.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.id === isolated)));
        updateResetButton();
        applyState();
    };

    const onResetClick = () => {
        if (isolated === null) return;
        isolated = null;
        keyList.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", "false"));
        updateResetButton();
        applyState();
    };

    const onControlClick = e => {
        const btn = e.target.closest("button");
        if (!btn) return;

        switch (btn.dataset.act) {
            case "explode":
                exploded = !exploded;
                btn.setAttribute("aria-pressed", String(exploded));
                controls.querySelector('[data-act="drawer"]').setAttribute("aria-pressed", String(drawerOut || exploded));
                camera.radius = fitDistance();
                break;
            case "drawer":
                drawerOut = !drawerOut;
                btn.setAttribute("aria-pressed", String(drawerOut || exploded));
                break;
            case "sleeve":
                sleeveOn = !sleeveOn;
                btn.setAttribute("aria-pressed", String(sleeveOn));
                break;
            case "recentre":
                camera.alpha = HOME.alpha;
                camera.beta = HOME.beta;
                camera.radius = fitDistance();
                isolated = null;
                keyList.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", "false"));
                updateResetButton();
                break;
        }

        applyState();
    };

    keyList.addEventListener("click", onKeyClick);
    controls.addEventListener("click", onControlClick);
    resetBtn.addEventListener("click", onResetClick);

    applyState();

    return {
        dispose() {
            window.removeEventListener("resize", onResize);
            keyList.removeEventListener("click", onKeyClick);
            controls.removeEventListener("click", onControlClick);
            resetBtn.removeEventListener("click", onResetClick);

            engine.stopRenderLoop();
            scene.dispose();
            engine.dispose();
            root.innerHTML = "";
        }
    };
}
