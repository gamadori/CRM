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

// ============================================================
// Input vocale
// ============================================================

/** True se il browser supporta la dettatura locale (Web Speech API). */
export function isBrowserDictationSupported() {
    return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
}

/** True se il browser supporta la registrazione audio (per la trascrizione Whisper). */
export function isRecordingSupported() {
    return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
}

// ---- Modalità Browser: dettatura con Web Speech API ----

let recognition = null;

/**
 * Avvia la dettatura vocale del browser. Ogni segmento riconosciuto come "finale"
 * viene inviato a .NET (OnDictationResult) e appeso al campo domanda. A fine sessione
 * chiama OnDictationEnd; in caso di errore OnVoiceError.
 */
export function startBrowserDictation(dotNetRef, lang) {
    const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) {
        dotNetRef.invokeMethodAsync('OnVoiceError', 'Il browser non supporta la dettatura vocale.');
        return false;
    }

    recognition = new SR();
    recognition.lang = lang || 'it-IT';
    recognition.continuous = true;
    recognition.interimResults = true;

    recognition.onresult = (e) => {
        // Invia solo i segmenti finalizzati, una volta ciascuno (niente duplicati).
        for (let i = e.resultIndex; i < e.results.length; i++) {
            if (e.results[i].isFinal) {
                const text = e.results[i][0].transcript;
                if (text && text.trim()) {
                    dotNetRef.invokeMethodAsync('OnDictationResult', text.trim());
                }
            }
        }
    };
    recognition.onerror = (e) => {
        const err = e && e.error ? e.error : 'errore';
        // "no-speech"/"aborted" sono interruzioni normali: non le segnaliamo come errore.
        if (err !== 'no-speech' && err !== 'aborted') {
            dotNetRef.invokeMethodAsync('OnVoiceError', 'Microfono: ' + err);
        }
    };
    recognition.onend = () => {
        recognition = null;
        dotNetRef.invokeMethodAsync('OnDictationEnd');
    };

    try {
        recognition.start();
        return true;
    } catch {
        recognition = null;
        dotNetRef.invokeMethodAsync('OnVoiceError', 'Impossibile avviare la dettatura.');
        return false;
    }
}

export function stopBrowserDictation() {
    if (recognition) {
        try { recognition.stop(); } catch { /* già ferma */ }
        recognition = null;
    }
}

// ---- Modalità Server: registrazione audio per Whisper ----

let mediaRecorder = null;
let mediaStream = null;
let chunks = [];

/** Avvia la registrazione dal microfono. Restituisce true se partita. */
export async function startRecording(dotNetRef) {
    if (!isRecordingSupported()) {
        dotNetRef.invokeMethodAsync('OnVoiceError', 'Il browser non supporta la registrazione audio.');
        return false;
    }
    try {
        mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        chunks = [];
        const mime = window.MediaRecorder.isTypeSupported('audio/webm') ? 'audio/webm' : '';
        mediaRecorder = mime
            ? new MediaRecorder(mediaStream, { mimeType: mime })
            : new MediaRecorder(mediaStream);
        mediaRecorder.ondataavailable = (e) => {
            if (e.data && e.data.size > 0) chunks.push(e.data);
        };
        mediaRecorder.start();
        return true;
    } catch {
        stopStream();
        dotNetRef.invokeMethodAsync('OnVoiceError', 'Permesso microfono negato o non disponibile.');
        return false;
    }
}

/**
 * Ferma la registrazione e restituisce { base64, mimeType } con l'audio catturato,
 * oppure null. .NET lo invia poi all'endpoint di trascrizione.
 */
export function stopRecording() {
    return new Promise((resolve) => {
        if (!mediaRecorder) { stopStream(); resolve(null); return; }

        mediaRecorder.onstop = async () => {
            try {
                const type = mediaRecorder.mimeType || 'audio/webm';
                const blob = new Blob(chunks, { type });
                stopStream();

                if (!blob.size) { resolve(null); return; }

                const bytes = new Uint8Array(await blob.arrayBuffer());
                let binary = '';
                const step = 0x8000; // conversione a blocchi: evita lo stack overflow su audio grandi
                for (let i = 0; i < bytes.length; i += step) {
                    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + step));
                }
                resolve({ base64: btoa(binary), mimeType: type });
            } catch {
                stopStream();
                resolve(null);
            }
        };

        try { mediaRecorder.stop(); }
        catch { stopStream(); resolve(null); }
    });
}

function stopStream() {
    if (mediaStream) {
        try { mediaStream.getTracks().forEach(t => t.stop()); } catch { /* ignora */ }
        mediaStream = null;
    }
    mediaRecorder = null;
}
