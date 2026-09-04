/*
   Tab switcher over the two interchangeable blueprint renderers.

   This file owns nothing about the scene itself — it only builds the tab bar, lazy-loads whichever
   engine module the visitor picks, and swaps one mounted viewer for the other. Each engine module
   (blueprint-three.js, blueprint-babylon.js) is fully self-contained: same dimensioned geometry,
   same assembly legend, same explode/drawer/sleeve controls, built independently per engine so the
   comparison is fair and each can be read on its own.

   A tab's engine module is only imported the first time that tab is opened, same reasoning as the
   old single-engine version: nothing this heavy loads until someone actually asks to see it.
*/

const ENGINES = [
    { id: "three", label: "Three.js", load: () => import("./blueprint-three.js") },
    { id: "babylon", label: "Babylon.js", load: () => import("./blueprint-babylon.js") }
];

export async function mount(root) {
    root.innerHTML =
        '<div class="build-engine-tabs" role="tablist" aria-label="Rendering engine">' +
        ENGINES.map((e, i) =>
            '<button type="button" role="tab" data-engine="' + e.id + '" aria-selected="' + (i === 0) + '">' +
            e.label + "</button>").join("") +
        "</div>" +
        '<div class="build-viewer"><div class="build-fallback">Loading the blueprint…</div></div>';

    const tabs = root.querySelector(".build-engine-tabs");
    const stage = root.querySelector(".build-viewer");

    let current = null;
    let activeId = null;

    async function activate(id) {
        if (id === activeId) {
            return;
        }

        const engine = ENGINES.find(e => e.id === id);
        const module = await engine.load();

        // The old viewer is only torn down once the new module has finished loading, so a slow
        // fetch on first use of a tab leaves the previous drawing on screen instead of a blank one.
        if (current) {
            current.dispose();
            current = null;
        }

        current = await module.mount(stage);
        activeId = id;

        tabs.querySelectorAll("button").forEach(b => b.setAttribute("aria-selected", String(b.dataset.engine === activeId)));
    }

    const onTabClick = e => {
        const btn = e.target.closest("button");
        if (!btn) return;
        activate(btn.dataset.engine);
    };

    tabs.addEventListener("click", onTabClick);

    await activate(ENGINES[0].id);

    return {
        dispose() {
            tabs.removeEventListener("click", onTabClick);
            if (current) {
                current.dispose();
            }
            root.innerHTML = "";
        }
    };
}
