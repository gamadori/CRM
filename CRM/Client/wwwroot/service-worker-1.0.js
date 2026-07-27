// Service Worker per Push Notifications CRM
// Versione: 2.30 - Production-safe cache strategy

const SERVICE_WORKER_VERSION = '2.30';
const CACHE_NAME = `crm-cache-v${SERVICE_WORKER_VERSION}`;

const urlsToCache = [
    '/css/app.css',
    '/css/ticket-interventions.css',
    '/css/ticket-preview.css',
    '/favicon.ico'
];

const neverCachePathPrefixes = [
    '/_framework/',
    '/api/',
    '/localApi/',
    '/authentication/',
    '/connect/',
    '/Identity/',
    '/.well-known/'
];

function isHttpRequest(requestUrl) {
    return requestUrl.protocol === 'http:' || requestUrl.protocol === 'https:';
}

function isNavigationRequest(event) {
    return event.request.mode === 'navigate'
        || event.request.destination === 'document'
        || event.request.headers.get('accept')?.includes('text/html') === true;
}

function shouldBypassCache(event, requestUrl) {
    return neverCachePathPrefixes.some(prefix => requestUrl.pathname.startsWith(prefix))
        || event.request.destination === 'audio'
        || event.request.destination === 'video';
}

async function putInCache(request, response) {
    if (response.status !== 200 || response.type === 'opaque') {
        return;
    }

    try {
        const cache = await caches.open(CACHE_NAME);
        await cache.put(request, response.clone());
    } catch (error) {
        console.warn('[Service Worker] Cache put skipped:', request.url, error);
    }
}

// ==========================================
// INSTALLAZIONE: Precache risorse critiche
// ==========================================
self.addEventListener('install', event => {
    console.log('[Service Worker] Installing...', SERVICE_WORKER_VERSION, event);
    
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                console.log('[Service Worker] Caching app shell');
                return cache.addAll(urlsToCache);
            })
            .then(() => self.skipWaiting()) // Attiva immediatamente
    );
});

// ==========================================
// ATTIVAZIONE: Pulizia cache vecchie
// ==========================================
self.addEventListener('activate', event => {
    console.log('[Service Worker] Activating...', SERVICE_WORKER_VERSION, event);
    
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        console.log('[Service Worker] Deleting old cache:', cacheName);
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => self.clients.claim()) // Prendi controllo di tutti i client
    );
});

// ==========================================
// FETCH: Network-first per Blazor (hot reload)
// ==========================================
self.addEventListener('fetch', event => {
    // Solo per GET requests
    if (event.request.method !== 'GET') return;
    
    const requestUrl = new URL(event.request.url);

    // La Cache API supporta solo richieste HTTP/HTTPS. Estensioni browser e altri schemi
    // devono restare fuori dal service worker, altrimenti cache.put genera eccezioni.
    if (!isHttpRequest(requestUrl)) {
        return;
    }

    // App shell e manifest Blazor devono arrivare sempre dal server: se si cacheano,
    // dopo un deploy possono puntare a file _framework non piu' validi.
    if (isNavigationRequest(event)) {
        event.respondWith(fetch(event.request));
        return;
    }

    // I flussi di autenticazione, le API e gli asset Blazor non devono mai passare dalla cache.
    if (shouldBypassCache(event, requestUrl)) {
        return; // Lascia che il browser gestisca normalmente la richiesta
    }

    event.respondWith(
        fetch(event.request)
            .then(response => {
                // Cacha solo risposte complete (status 200)
                // La Cache API non supporta risposte parziali (206)
                putInCache(event.request, response);
                return response;
            })
            .catch(async () => {
                // Fallback a cache se offline
                return await caches.match(event.request) ?? Response.error();
            })
    );
});

// ==========================================
// ? PUSH EVENT: Ricevi notifica dal server
// ==========================================
self.addEventListener('push', event => {
    console.log('[Service Worker] Push received:', event);

    let notificationData = {
        title: 'CRM Notification',
        body: 'Hai ricevuto una nuova notifica',
        icon: '/icon-192.png',
        badge: '/favicon.ico',
        tag: 'crm-notification',
        requireInteraction: false,
        data: {
            url: '/'
        }
    };

    // Parse payload JSON
    if (event.data) {
        try {
            const payload = event.data.json();
            console.log('[Service Worker] Push payload:', payload);

            notificationData = {
                title: payload.title || notificationData.title,
                body: payload.body || payload.message || notificationData.body,
                icon: payload.icon || notificationData.icon,
                badge: payload.badge || notificationData.badge,
                tag: payload.tag || `crm-${Date.now()}`,
                requireInteraction: payload.requireInteraction || false,
                vibrate: [200, 100, 200], // Vibrazione mobile
                data: {
                    url: payload.url || payload.data?.url || '/',
                    ticketId: payload.data?.ticketId,
                    action: payload.data?.action,
                    timestamp: Date.now()
                }
            };

            // ? Aggiungi actions se presenti
            if (payload.actions && Array.isArray(payload.actions)) {
                notificationData.actions = payload.actions;
            } else if (notificationData.data.ticketId) {
                // Actions di default per ticket
                notificationData.actions = [
                    {
                        action: 'view',
                        title: '??? Visualizza',
                        icon: '/icon-192.png'
                    },
                    {
                        action: 'close',
                        title: '?? Chiudi'
                    }
                ];
            }

        } catch (error) {
            console.error('[Service Worker] Error parsing push payload:', error);
            // Usa notificationData di default
        }
    }

    // Mostra notifica
    event.waitUntil(
        self.registration.showNotification(notificationData.title, notificationData)
            .then(() => {
                console.log('[Service Worker] Notification displayed:', notificationData.title);
            })
            .catch(error => {
                console.error('[Service Worker] Error showing notification:', error);
            })
    );
});

// ==========================================
// ? NOTIFICATION CLICK: Gestisci click utente
// ==========================================
self.addEventListener('notificationclick', event => {
    console.log('[Service Worker] Notification clicked:', event);

    event.notification.close(); // Chiudi notifica

    const urlToOpen = event.notification.data?.url || '/';
    const action = event.action; // 'view', 'close', undefined

    // Se azione = close, non fare nulla
    if (action === 'close') {
        return;
    }

    // Apri URL: usa una finestra esistente oppure una nuova scheda
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then(clientList => {
                // Cerca un client gia aperto con lo stesso URL
                for (const client of clientList) {
                    if (client.url === urlToOpen && 'focus' in client) {
                        return client.focus();
                    }
                }

                // Altrimenti apri nuovo tab
                if (clients.openWindow) {
                    return clients.openWindow(urlToOpen);
                }
            })
            .catch(error => {
                console.error('[Service Worker] Error opening window:', error);
            })
    );
});

// ==========================================
// ? NOTIFICATION CLOSE: Log chiusura
// ==========================================
self.addEventListener('notificationclose', event => {
    console.log('[Service Worker] Notification closed:', event.notification.tag);
    
    // (Opzionale) Invia analytics al server
    // fetch('/api/analytics/notification-closed', {
    //     method: 'POST',
    //     body: JSON.stringify({ tag: event.notification.tag })
    // });
});

// ==========================================
// ? MESSAGE: Comunicazione con client Blazor
// ==========================================
self.addEventListener('message', event => {
    console.log('[Service Worker] Message received:', event.data);

    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }

    // Risposta al client
    if (event.ports && event.ports[0]) {
        event.ports[0].postMessage({
            type: 'ACK',
            message: 'Service Worker received message'
        });
    }
});

console.log(`[Service Worker] Loaded successfully - Version ${SERVICE_WORKER_VERSION} - Production-safe cache strategy`);
