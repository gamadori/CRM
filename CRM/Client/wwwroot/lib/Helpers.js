"use strict";
function clickElement(element) {
    element.click();
}
function downloadFromUrl(options) {
    var _a;
    var anchorElement = document.createElement('a');
    anchorElement.href = options.url;
    anchorElement.download = (_a = options.fileName) !== null && _a !== void 0 ? _a : '';
    anchorElement.click();
    anchorElement.remove();
}
function downloadFromByteArray(options) {
    try {
        var blob = new Blob([options.byteArray], {
            type: options.contentType
        });
        var url = window.URL.createObjectURL(blob);
        downloadFromUrl({ url: url, fileName: options.fileName });
    }
    catch (error) {
        alert(error);
    }
}
// ✅ AGGIUNTO: Funzione per scaricare file da byte array
window.downloadFileFromBytes = (fileName, contentType, byteArray) => {
    const blob = new Blob([byteArray], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? 'download';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
};



function isDevice() {
    return /android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile/i.test(navigator.userAgent);
}
window.getWindowDimensions = function () {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
};
function scrollToBottom(id) {
    var pos = $('#' + id)[0].scrollHeight;
    $('#' + id).scrollTop(pos);
}
function scrollToTop(id) {
    $('#' + id).scrollTop(0);
}
function ShowCanvas(convasId) {
    const myOffcanvas = document.getElementById(convasId);
    var bsOffcanvas = new bootstrap.Offcanvas(myOffcanvas);
    bsOffcanvas.show();
}
window.PlayAudio = (elementName) => {
    document.getElementById(elementName).play();
}
window.dialogSizing = (function () {
    const observers = new WeakMap();
    function setCanvasToContainer(container, canvas) {
        if (!container || !canvas)
            return;
        function adjust() {
            try {
                const h = container.clientHeight;
                canvas.style.height = h + 'px';
            }
            catch (e) {
                console.error(e);
            }
        }
        // misura subito
        adjust();
        // usa ResizeObserver per reagire a cambi di layout
        if (window.ResizeObserver) {
            const ro = new ResizeObserver(adjust);
            try {
                ro.observe(container);
            }
            catch (e) {
                console.error(e);
            }
            observers.set(container, ro);
        }
        else {
            const handler = () => adjust();
            window.addEventListener('resize', handler);
            observers.set(container, { disconnect: () => window.removeEventListener('resize', handler) });
        }
    }
    function disconnectObserver(container) {
        const ro = observers.get(container);
        if (ro) {
            try {
                ro.disconnect();
            }
            catch (e) { }
            observers.delete(container);
        }
    }
    return {
        setCanvasToContainer,
        disconnectObserver
    };
})();

// Aggiungi alla fine del file esistente

window.displayFileInElement = (element, contentType, byteArray, suggestedName) => {
    try {
        const el = element instanceof Element ? element : document.getElementById(element) || null;
        if (!el) { console.error("displayFileInElement: element not found"); return; }

        // cleanup precedente
        if (el._blobUrl) {
            try { URL.revokeObjectURL(el._blobUrl); } catch (e) { }
            el._blobUrl = null;
        }
        el.innerHTML = '';

        // crea blob
        const uint8 = new Uint8Array(byteArray);
        const blob = new Blob([uint8], { type: contentType });
        const url = URL.createObjectURL(blob);
        el._blobUrl = url;

        // comportamento per tipo
        if (contentType === 'application/pdf') {
            const iframe = document.createElement('iframe');
            iframe.src = url;
            iframe.style.width = '100%';
            iframe.style.height = '100%';
            iframe.style.border = '0';
            el.appendChild(iframe);
            return;
        }

        if (contentType.startsWith('image/')) {
            const img = document.createElement('img');
            img.src = url;
            img.style.maxWidth = '100%';
            img.style.maxHeight = '100%';
            img.style.display = 'block';
            el.appendChild(img);
            return;
        }

        // DOCX e altri: mostra link Apri / Scarica
        const p = document.createElement('div');
        p.style.padding = '10px';

        const open = document.createElement('a');
        open.href = url;
        open.target = '_blank';
        open.rel = 'noopener';
        open.textContent = 'Apri in una nuova scheda';
        p.appendChild(open);

        const sep = document.createTextNode(' — ');
        p.appendChild(sep);

        const dl = document.createElement('a');
        dl.href = url;
        dl.download = suggestedName || '';
        dl.textContent = 'Scarica';
        p.appendChild(dl);

        const note = document.createElement('div');
        note.style.marginTop = '8px';
        note.style.fontSize = '0.9em';
        note.style.color = '#666';
        note.textContent = 'Anteprima DOCX non supportata nel browser. Scarica o apri con Office/Google Docs.';
        p.appendChild(note);

        el.appendChild(p);
    } catch (e) {
        console.error(e);
    }
};

window.cleanupFileHost = (element) => {
    try {
        const el = element instanceof Element ? element : document.getElementById(element) || null;
        if (!el) return;
        if (el._blobUrl) {
            try { URL.revokeObjectURL(el._blobUrl); } catch (e) { }
            el._blobUrl = null;
        }
    } catch (e) { console.error(e); }
};