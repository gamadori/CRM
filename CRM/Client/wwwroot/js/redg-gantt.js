// Modulo drag/resize/creazione-dipendenze per il componente RedGGantt.
// Interazione a pointer events con anteprima live; a fine gesto richiama .NET.
// Nessuna dipendenza esterna.

export function init(container, dotNetRef) {
    if (!container) return null;

    let drag = null;

    const pxPerDay = () => parseFloat(container.dataset.pxperday || "24");

    function onPointerDown(e) {
        const handle = e.target.closest('.gantt-bar-handle, .gantt-link-handle');
        const bar = e.target.closest('.gantt-bar');
        if (!bar) return;

        const taskId = parseInt(bar.dataset.taskId, 10);
        if (isNaN(taskId)) return;

        let role = 'move';
        if (handle) {
            if (handle.classList.contains('gantt-link-handle')) role = 'link';
            else if (handle.classList.contains('start')) role = 'resize-start';
            else if (handle.classList.contains('end')) role = 'resize-end';
        }

        drag = {
            taskId, role, bar,
            startX: e.clientX,
            origLeft: bar.offsetLeft,
            origWidth: bar.offsetWidth,
            ghost: null
        };

        if (role === 'link') {
            const g = document.createElement('div');
            g.className = 'gantt-link-ghost';
            // Elemento creato via JS (fuori dallo scope CSS Blazor): stile inline.
            g.style.cssText = 'position:absolute;height:2px;background:#f59e0b;transform-origin:left center;' +
                'pointer-events:none;z-index:30;border-radius:2px;';
            container.appendChild(g);
            drag.ghost = g;
        } else {
            bar.classList.add('dragging');
        }

        bar.setPointerCapture?.(e.pointerId);
        e.preventDefault();
    }

    function onPointerMove(e) {
        if (!drag) return;
        const dx = e.clientX - drag.startX;

        if (drag.role === 'link') {
            const rect = container.getBoundingClientRect();
            const g = drag.ghost;
            const x1 = drag.origLeft + drag.origWidth;
            const y1 = drag.bar.offsetTop + drag.bar.offsetHeight / 2;
            const x2 = e.clientX - rect.left + container.scrollLeft;
            const y2 = e.clientY - rect.top + container.scrollTop;
            const len = Math.hypot(x2 - x1, y2 - y1);
            const ang = Math.atan2(y2 - y1, x2 - x1) * 180 / Math.PI;
            g.style.left = x1 + 'px';
            g.style.top = y1 + 'px';
            g.style.width = len + 'px';
            g.style.transform = `rotate(${ang}deg)`;
            return;
        }

        if (drag.role === 'move') {
            drag.bar.style.left = (drag.origLeft + dx) + 'px';
        } else if (drag.role === 'resize-start') {
            const w = Math.max(pxPerDay(), drag.origWidth - dx);
            drag.bar.style.left = (drag.origLeft + (drag.origWidth - w)) + 'px';
            drag.bar.style.width = w + 'px';
        } else if (drag.role === 'resize-end') {
            drag.bar.style.width = Math.max(pxPerDay(), drag.origWidth + dx) + 'px';
        }
    }

    async function onPointerUp(e) {
        if (!drag) return;
        const d = drag;
        drag = null;

        if (d.role === 'link') {
            d.ghost?.remove();
            const target = document.elementFromPoint(e.clientX, e.clientY)?.closest('.gantt-bar');
            const toId = target ? parseInt(target.dataset.taskId, 10) : NaN;
            if (!isNaN(toId) && toId !== d.taskId) {
                await dotNetRef.invokeMethodAsync('OnLinkCreate', d.taskId, toId);
            }
            return;
        }

        d.bar.classList.remove('dragging');
        const ppd = pxPerDay();
        const dxDays = Math.round((e.clientX - d.startX) / ppd);
        if (dxDays !== 0) {
            await dotNetRef.invokeMethodAsync('OnBarDragEnd', d.taskId, d.role, dxDays);
        } else {
            // reset eventuale anteprima
            d.bar.style.left = d.origLeft + 'px';
            d.bar.style.width = d.origWidth + 'px';
        }
    }

    container.addEventListener('pointerdown', onPointerDown);
    window.addEventListener('pointermove', onPointerMove);
    window.addEventListener('pointerup', onPointerUp);

    return {
        dispose() {
            container.removeEventListener('pointerdown', onPointerDown);
            window.removeEventListener('pointermove', onPointerMove);
            window.removeEventListener('pointerup', onPointerUp);
        }
    };
}
