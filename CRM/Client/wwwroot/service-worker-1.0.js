// ? Service Worker per Push Notifications CRM
// Versione: 2.1 - Fix audio/video 206 status code

const CACHE_NAME = 'crm-cache-v2.1';

const urlsToCache = [
    '/',
    '/index.html',
    '/css/app.css',
    '/favicon.ico'
];

// ==========================================
// INSTALLAZIONE: Precache risorse critiche
// ==========================================
self.addEventListener('install', event => {
    console.log('[Service Worker] Installing...', event);
    
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
    console.log('[Service Worker] Activating...', event);
    
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
    
    // Skip _framework, API calls e file multimediali (sempre network)
    const shouldSkip = event.request.url.includes('_framework') || 
                      event.request.url.includes('/api/') ||
                      event.request.destination === 'audio' ||
                      event.request.destination === 'video';
    
    if (shouldSkip) {
        return; // Lascia che il browser gestisca normalmente la richiesta
    }

    event.respondWith(
        fetch(event.request)
            .then(response => {
                // ? Caccia solo risposte complete (status 200)
                // La Cache API non supporta risposte parziali (206)
                if (response.status === 200) {
                    const responseClone = response.clone();
                    
                    caches.open(CACHE_NAME).then(cache => {
                        cache.put(event.request, responseClone);
                    });
                }
                
                return response;
            })
            .catch(() => {
                // Fallback a cache se offline
                return caches.match(event.request);
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

    // Apri URL (focus se già aperto, altrimenti nuova tab)
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then(clientList => {
                // Cerca client già aperto con stesso URL
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

console.log('[Service Worker] Loaded successfully - Version 2.1 - Audio/Video fix');
