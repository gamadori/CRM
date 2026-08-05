// Coda locale dei biglietti raccolti in fiera.
//
// Perche' IndexedDB e non localStorage: qui dentro finisce anche la foto del biglietto, e
// localStorage si ferma intorno ai 5 MB totali - una decina di foto e la coda comincia a
// rifiutare scritture, cioe' proprio il fallimento silenzioso che questa coda esiste per evitare.
//
// Il patto e': un lead entra qui PRIMA di qualunque tentativo di rete e ne esce solo quando il
// server ha confermato. Tutto cio' che sta in mezzo - rete assente, pagina chiusa, telefono
// spento - lascia il contatto al sicuro.

const DB_NAME = 'crm-lead-queue';
const DB_VERSION = 1;
const STORE = 'pending';

function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE)) {
                db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function run(mode, work) {
    return openDb().then(db => new Promise((resolve, reject) => {
        const transaction = db.transaction(STORE, mode);
        const store = transaction.objectStore(STORE);
        let result;

        try {
            result = work(store);
        } catch (error) {
            reject(error);
            return;
        }

        transaction.oncomplete = () => resolve(result && result.result !== undefined ? result.result : result);
        transaction.onerror = () => reject(transaction.error);
        transaction.onabort = () => reject(transaction.error);
    }));
}

export async function enqueue(record) {
    return await run('readwrite', store => store.add(record));
}

export async function list() {
    const items = await run('readonly', store => store.getAll());
    return items || [];
}

export async function remove(id) {
    await run('readwrite', store => store.delete(id));
}

export async function count() {
    const value = await run('readonly', store => store.count());
    return value || 0;
}

export function isOnline() {
    return navigator.onLine !== false;
}

// Il ritorno della rete e' l'unico momento in cui ha senso riprovare da soli: interrogare a vuoto
// ogni pochi secondi, con la batteria di un telefono in fiera, e' peggio del problema.
export function watchOnline(dotNetRef) {
    const handler = () => dotNetRef.invokeMethodAsync('OnBackOnline');
    window.addEventListener('online', handler);
    return true;
}

// Riduce la foto prima di metterla in coda: una foto da telefono e' 3-5 MB, e su una rete di fiera
// e' la differenza fra un invio che riesce e uno che scade. 1600px sul lato lungo restano molti
// piu' pixel di quanti ne serva a leggere un biglietto da visita.
export function shrink(dataUrl, maxSide, quality) {
    return new Promise(resolve => {
        const image = new Image();

        image.onload = () => {
            const scale = Math.min(1, maxSide / Math.max(image.width, image.height));
            if (scale === 1) {
                resolve(dataUrl);
                return;
            }

            const canvas = document.createElement('canvas');
            canvas.width = Math.round(image.width * scale);
            canvas.height = Math.round(image.height * scale);

            const context = canvas.getContext('2d');
            context.drawImage(image, 0, 0, canvas.width, canvas.height);

            try {
                resolve(canvas.toDataURL('image/jpeg', quality));
            } catch (error) {
                // Immagine di origine non convertibile: meglio spedirla grande che perderla.
                resolve(dataUrl);
            }
        };

        // Se il browser non riesce nemmeno a caricarla, si tiene l'originale: questa funzione
        // ottimizza, non deve mai essere il punto in cui un biglietto sparisce.
        image.onerror = () => resolve(dataUrl);
        image.src = dataUrl;
    });
}
