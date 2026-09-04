/*
   The interactive section drawing — Three.js engine.

   One of two interchangeable renderers behind blueprint.js's tab switcher (see
   blueprint-babylon.js for the other). Both build the exact same dimensioned scene from the same
   constants, so switching tabs is a fair side-by-side of the two engines rather than a comparison
   of two different drawings.

   Three.js is served from wwwroot/lib rather than a CDN: this dashboard is a LAN appliance
   sitting next to the column, and the one page you most want to open while elbow-deep in a build
   is the one that must not need the internet. r160 is the last release shipping a UMD build that
   defines a global THREE, which is why it is pinned there and injected as a plain script tag
   instead of imported.

   The script is only fetched the first time this engine is actually selected — 650 kB has no
   business loading behind the live telemetry view, or behind a tab nobody clicked.
*/

const THREE_URL = "lib/three.min.js";

let threeRequest = null;

function loadThree() {
    if (window.THREE) {
        return Promise.resolve(window.THREE);
    }

    threeRequest ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = THREE_URL;
        script.onload = () => (window.THREE ? resolve(window.THREE) : reject(new Error("three.js loaded but defined no global")));
        script.onerror = () => reject(new Error("three.js could not be fetched"));
        document.head.appendChild(script);
    });

    return threeRequest;
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
    let THREE;

    try {
        THREE = await loadThree();
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

    const scene = new THREE.Scene();
    const groups = {};
    const meshes = [];
    const disposables = [];

    // Inches, scaled so the whole column is a comfortable unit height.
    const S = 0.11;
    const OUT = 8.5 * S;      // column external section
    const PLATE = 10 * S;     // bay plates — they overhang, and the overhang is the riser flange land
    const BAY = 5.25 * S;     // bay enclosure, a waist inside the column line
    const RISER_R = 3.25 * S; // riser centres, on the midlines of three faces

    function material(color, opacity) {
        const mat = new THREE.MeshStandardMaterial({
            color, transparent: opacity < 1, opacity, roughness: 0.55, metalness: 0.05
        });
        disposables.push(mat);
        return mat;
    }

    function box(w, h, d, color, opacity) {
        const geo = new THREE.BoxGeometry(w, h, d);
        disposables.push(geo);
        return new THREE.Mesh(geo, material(color, opacity));
    }

    function cylinder(r, h, color, opacity, segments = 20) {
        const geo = new THREE.CylinderGeometry(r, r, h, segments);
        disposables.push(geo);
        return new THREE.Mesh(geo, material(color, opacity));
    }

    function group(id, y) {
        const g = new THREE.Group();
        g.position.y = y;
        g.userData.baseY = y;
        scene.add(g);
        groups[id] = g;
        return g;
    }

    function add(g, mesh, id, x = 0, y = 0, z = 0) {
        mesh.position.set(x, y, z);
        mesh.userData.assembly = id;
        g.add(mesh);
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
    sleeve.userData.assembly = "nest";
    gNest.add(sleeve);

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
    gDrawer.userData.baseZ = 0;
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
        ring.material.emissive = new THREE.Color(ringColors[i]);
        ring.material.emissiveIntensity = 0.6;
        add(gOut, ring, "out", x, outFloor + 0.05 * S, z);
    });

    const dish = cylinder(0.8 * S, 0.35 * S, 0xdedad0, 1, 22);
    add(gOut, dish, "out", 1.4 * S, outFloor + 0.2 * S, 1.6 * S);

    // --- lid -------------------------------------------------------------------
    add(group("lid", 0), box(OUT + 0.3 * S, 0.5 * S, OUT + 0.3 * S, 0x8896a6, 1), "lid", 0, outTop + 0.25 * S, 0);

    // --- lighting and camera ---------------------------------------------------
    scene.add(new THREE.AmbientLight(0xffffff, 0.66));

    const key = new THREE.DirectionalLight(0xffffff, 0.8);
    key.position.set(4, 7, 6);
    scene.add(key);

    const fill = new THREE.DirectionalLight(0xaaccff, 0.32);
    fill.position.set(-5, 2, -4);
    scene.add(fill);

    const camera = new THREE.PerspectiveCamera(38, 1, 0.1, 100);
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));

    // Hand-rolled orbit: OrbitControls is not part of the UMD build, and one more file for thirty
    // lines of spherical maths is not a good trade.
    // Frame the whole column: the target is its vertical midpoint, not the nest's, or the lid
    // falls outside the frustum on a wide canvas.
    const HOME = { yaw: 0.62, pitch: 0.18, dist: 5.6, explodedDist: 8.0 };
    let yaw = HOME.yaw, pitch = HOME.pitch, dist = HOME.dist;
    const target = new THREE.Vector3(0, (outTop + 0.5 * S) / 2, 0);

    let dragging = false, lastX = 0, lastY = 0;

    function place() {
        camera.position.set(
            target.x + dist * Math.cos(pitch) * Math.sin(yaw),
            target.y + dist * Math.sin(pitch),
            target.z + dist * Math.cos(pitch) * Math.cos(yaw)
        );
        camera.lookAt(target);
    }

    function render() {
        renderer.render(scene, camera);
    }

    function resize() {
        const w = canvas.clientWidth || 600;
        const h = canvas.clientHeight || 460;
        renderer.setSize(w, h, false);
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
    }

    const onPointerDown = e => {
        dragging = true;
        lastX = e.clientX;
        lastY = e.clientY;
        canvas.setPointerCapture(e.pointerId);
    };

    const onPointerUp = e => {
        dragging = false;
        try { canvas.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    };

    const onPointerMove = e => {
        if (!dragging) return;
        yaw -= (e.clientX - lastX) * 0.008;
        pitch = Math.max(-0.5, Math.min(1.25, pitch + (e.clientY - lastY) * 0.006));
        lastX = e.clientX;
        lastY = e.clientY;
        place();
        render();
    };

    const onWheel = e => {
        e.preventDefault();
        dist = Math.max(2.6, Math.min(11, dist + Math.sign(e.deltaY) * 0.35));
        place();
        render();
    };

    const onResize = () => { resize(); place(); render(); };

    canvas.addEventListener("pointerdown", onPointerDown);
    canvas.addEventListener("pointerup", onPointerUp);
    canvas.addEventListener("pointermove", onPointerMove);
    canvas.addEventListener("wheel", onWheel, { passive: false });
    window.addEventListener("resize", onResize);

    // --- view state ------------------------------------------------------------
    let exploded = false, drawerOut = false, sleeveOn = true, isolated = null;

    meshes.forEach(m => { m.userData.baseOpacity = m.material.opacity; });
    sleeve.userData.baseOpacity = sleeve.material.opacity;

    // Chosen so the exploded stack still fits the frame at HOME.explodedDist — an explode that
    // pushes the lid out of shot is a worse drawing than no explode at all.
    const SPREAD = { lid: 0.95, out: 0.68, riser: 0.35, portal: 0.35, bay: 0.35, drawer: 0.35, nest: 0, res: -0.3, base: 0 };

    // Zoom to fit, the way a CAD viewer does: exploding is a request to see more, not to have the
    // top of the column leave the frame.
    const fitDistance = () => (exploded ? HOME.explodedDist : HOME.dist);

    function applyState() {
        Object.keys(groups).forEach(id => {
            groups[id].position.y = groups[id].userData.baseY + (exploded ? SPREAD[id] : 0);
        });

        // The drawer is the one thing that moves along its own axis rather than the stack axis,
        // which is the point of it.
        gDrawer.position.z = drawerOut || exploded ? 5.4 * S : 0;

        sleeve.visible = sleeveOn;

        meshes.forEach(m => {
            const dim = isolated && m.userData.assembly !== isolated;
            m.material.transparent = true;
            m.material.opacity = dim ? 0.06 : m.userData.baseOpacity;
        });

        render();
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
                dist = fitDistance();
                place();
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
                yaw = HOME.yaw; pitch = HOME.pitch; dist = fitDistance();
                isolated = null;
                keyList.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", "false"));
                updateResetButton();
                place();
                break;
        }

        applyState();
    };

    keyList.addEventListener("click", onKeyClick);
    controls.addEventListener("click", onControlClick);
    resetBtn.addEventListener("click", onResetClick);

    resize();
    place();
    applyState();

    return {
        dispose() {
            window.removeEventListener("resize", onResize);
            canvas.removeEventListener("pointerdown", onPointerDown);
            canvas.removeEventListener("pointerup", onPointerUp);
            canvas.removeEventListener("pointermove", onPointerMove);
            canvas.removeEventListener("wheel", onWheel);
            keyList.removeEventListener("click", onKeyClick);
            controls.removeEventListener("click", onControlClick);
            resetBtn.removeEventListener("click", onResetClick);

            disposables.forEach(d => d.dispose());
            renderer.dispose();
            root.innerHTML = "";
        }
    };
}
