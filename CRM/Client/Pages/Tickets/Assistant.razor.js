// Modulo JS collocato al componente Assistant: caricato via import dinamico,
// quindi sempre allineato al componente (nessun problema di cache dei file globali).

/**
 * Blocca il ritorno a capo su Invio (senza Shift) nella textarea della chat:
 * l'invio del messaggio è gestito dal keydown lato Blazor, il newline di
 * default va soppresso, altrimenti rientra nel campo dopo la pulizia.
 */
export function preventEnterNewline(id) {
    const el = document.getElementById(id);
    if (!el || el.dataset.enterHooked) return;
    el.dataset.enterHooked = '1';
    el.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
        }
    });
}
