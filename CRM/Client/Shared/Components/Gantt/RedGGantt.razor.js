// Misura la larghezza utile della timeline (griglia meno colonna attivita') e la
// notifica al componente, che da li' calcola i pixel per giorno lavorativo.
//
// Gli osservatori sono indicizzati per chiave di istanza, non per elemento: a ogni
// ricarica del piano la griglia viene smontata e ricreata, e senza una chiave stabile
// resterebbe attivo un ResizeObserver agganciato all'elemento vecchio.
const observers = new Map();

function usableWidth(grid) {
    const left = grid.querySelector('.rg-left');
    return Math.round(grid.clientWidth - (left ? left.offsetWidth : 0));
}

export function attach(key, grid, dotNetRef) {
    if (!grid) return;
    detach(key);

    let last = -1;
    const measure = () => {
        const width = usableWidth(grid);
        // la soglia evita il ping-pong quando compare/scompare la scrollbar
        if (width <= 0 || Math.abs(width - last) < 3) return;
        last = width;
        dotNetRef.invokeMethodAsync('OnViewportResized', width);
    };

    if (window.ResizeObserver) {
        const ro = new ResizeObserver(() => requestAnimationFrame(measure));
        ro.observe(grid);
        observers.set(key, ro);
    } else {
        const handler = () => measure();
        window.addEventListener('resize', handler);
        observers.set(key, { disconnect: () => window.removeEventListener('resize', handler) });
    }

    measure();
}

export function detach(key) {
    const ro = observers.get(key);
    if (!ro) return;
    try { ro.disconnect(); } catch { /* elemento gia' rimosso */ }
    observers.delete(key);
}

export function scrollToPx(grid, left) {
    if (!grid) return;
    grid.scrollTo({ left: Math.max(0, left), behavior: 'smooth' });
}
