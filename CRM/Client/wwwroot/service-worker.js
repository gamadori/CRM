// ==========================================
// SERVICE WORKER PER WEB PUSH NOTIFICATIONS
// ==========================================

self.addEventListener('install', (event) => {
    console.log('[Service Worker] Installing...');
    self.skipWaiting();
});

self.addEventListener('activate', (event) => {
    console.log('[Service Worker] Activating...');
    event.waitUntil(clients.claim());
});

// ? GESTIONE NOTIFICHE PUSH
self.addEventListener('push', (event) => {
    console.log('=====================================');
    console.log('[Service Worker] ? PUSH EVENT RECEIVED!');
    console.log('[Service Worker] Event:', event);
    console.log('[Service Worker] Has data:', !!event.data);
    
    if (event.data) {
        console.log('[Service Worker] Data type:', typeof event.data);
        console.log('[Service Worker] Raw data:', event.data);
    }
    console.log('=====================================');

    let notificationData = {
        title: 'CRM Notification',
        body: 'Hai una nuova notifica',
        icon: '/favicon.ico',
        badge: '/favicon.ico',
        tag: 'crm-notification',
        requireInteraction: false,
        data: {
            url: '/'
        }
    };

    // Deserializza i dati del push
    if (event.data) {
        try {
            const payload = event.data.json();
            console.log('[Service Worker] ? Payload parsed:', payload);

            notificationData = {
                title: payload.title || 'CRM Notification',
                body: payload.body || payload.message || 'Hai una nuova notifica',
                icon: payload.icon || '/favicon.ico',
                badge: payload.badge || '/favicon.ico',
                tag: payload.tag || 'crm-notification',
                requireInteraction: payload.requireInteraction || false,
                data: {
                    url: payload.url || payload.data?.url || '/',
                    ticketId: payload.ticketId || payload.data?.ticketId,
                    action: payload.action || payload.data?.action
                },
                actions: payload.actions || []
            };
            
            console.log('[Service Worker] ? Notification data prepared:', notificationData);
        } catch (e) {
            console.error('[Service Worker] ? Error parsing push data:', e);
            notificationData.body = event.data.text();
        }
    } else {
        console.warn('[Service Worker] ?? No data in push event!');
    }

    console.log('[Service Worker] ?? Showing notification...');
    
    // Mostra la notifica
    event.waitUntil(
        self.registration.showNotification(notificationData.title, notificationData)
            .then(() => {
                console.log('[Service Worker] ? Notification shown successfully!');
            })
            .catch((error) => {
                console.error('[Service Worker] ? Error showing notification:', error);
            })
    );
});

// ? GESTIONE CLICK SULLA NOTIFICA
self.addEventListener('notificationclick', (event) => {
    console.log('[Service Worker] Notification clicked:', event.notification);

    event.notification.close();

    // Estrai URL dalla notifica
    const urlToOpen = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((windowClients) => {
                // Cerca una finestra già aperta con lo stesso origin
                for (let client of windowClients) {
                    if (client.url.startsWith(self.location.origin) && 'focus' in client) {
                        // Naviga alla URL della notifica
                        client.postMessage({
                            type: 'NAVIGATE',
                            url: urlToOpen
                        });
                        return client.focus();
                    }
                }

                // Se nessuna finestra è aperta, aprine una nuova
                if (clients.openWindow) {
                    return clients.openWindow(urlToOpen);
                }
            })
    );
});

// ? GESTIONE CHIUSURA NOTIFICA
self.addEventListener('notificationclose', (event) => {
    console.log('[Service Worker] Notification closed:', event.notification.tag);
});
