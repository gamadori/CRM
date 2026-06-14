export function initialize(zoneId, inputId) {
    const zone = document.getElementById(zoneId);
    const input = document.getElementById(inputId);
    if (!zone || !input || zone.dataset.dropReady === "true") return;
    zone.dataset.dropReady = "true";

    const prevent = event => { event.preventDefault(); event.stopPropagation(); };
    ["dragenter", "dragover"].forEach(name => zone.addEventListener(name, event => {
        prevent(event);
        zone.classList.add("is-dragging");
    }));
    ["dragleave", "drop"].forEach(name => zone.addEventListener(name, event => {
        prevent(event);
        zone.classList.remove("is-dragging");
    }));
    zone.addEventListener("drop", event => {
        const files = event.dataTransfer?.files;
        if (!files?.length) return;
        const transfer = new DataTransfer();
        transfer.items.add(files[0]);
        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    });
}

export function clear(inputId) {
    const input = document.getElementById(inputId);
    if (input) input.value = "";
}
