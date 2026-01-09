// ==========================================
// HELPER PER WEB PUSH NOTIFICATIONS
// ==========================================

window.PushNotifications = {
    
    // ? Verifica supporto browser
    isSupported: function() {
        return 'serviceWorker' in navigator && 
               'PushManager' in window && 
               'Notification' in window;
    },

    // ? Ottieni stato permesso
    getPermissionState: function() {
        if (!this.isSupported()) {
            return 'unsupported';
        }
        return Notification.permission; // 'granted', 'denied', 'default'
    },

    // ? Richiedi permesso notifiche
    requestPermission: async function() {
        if (!this.isSupported()) {
            console.error('[PushNotifications] Browser non supporta le notifiche push');
            return { success: false, error: 'Browser non supportato' };
        }

        try {
            const permission = await Notification.requestPermission();
            console.log('[PushNotifications] Permission:', permission);
            
            return {
                success: permission === 'granted',
                permission: permission
            };
        } catch (error) {
            console.error('[PushNotifications] Errore richiesta permesso:', error);
            return { success: false, error: error.message };
        }
    },

    // ? Registra Service Worker e ottieni subscription
    subscribe: async function(vapidPublicKey) {
        if (!this.isSupported()) {
            return { success: false, error: 'Browser non supportato' };
        }

        try {
            // ? FIX: Usa service worker già registrato in index.html
            const registration = await navigator.serviceWorker.ready;

            console.log('[PushNotifications] Service Worker ready:', registration);

            // Ottieni subscription esistente o creane una nuova
            let subscription = await registration.pushManager.getSubscription();

            if (!subscription) {
                // Converti VAPID key da Base64 a Uint8Array
                const convertedVapidKey = this.urlBase64ToUint8Array(vapidPublicKey);

                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: convertedVapidKey
                });

                console.log('[PushNotifications] New subscription created:', subscription);
            } else {
                console.log('[PushNotifications] Existing subscription:', subscription);
            }

            // Serializza subscription per invio al server
            const subscriptionJson = JSON.stringify(subscription);

            return {
                success: true,
                subscription: subscriptionJson,
                endpoint: subscription.endpoint
            };

        } catch (error) {
            console.error('[PushNotifications] Errore subscribe:', error);
            return { success: false, error: error.message };
        }
    },

    // ? Annulla subscription
    unsubscribe: async function() {
        try {
            const registration = await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();

            if (subscription) {
                await subscription.unsubscribe();
                console.log('[PushNotifications] Unsubscribed successfully');
                return { success: true };
            }

            return { success: false, error: 'Nessuna subscription attiva' };

        } catch (error) {
            console.error('[PushNotifications] Errore unsubscribe:', error);
            return { success: false, error: error.message };
        }
    },

    // ? Test notifica locale (senza server)
    showTestNotification: async function() {
        if (Notification.permission !== 'granted') {
            alert('Permesso notifiche non concesso');
            return { success: false, error: 'Permission denied' };
        }

        try {
            const registration = await navigator.serviceWorker.ready;
            
            await registration.showNotification('Test Notifica CRM', {
                body: 'Questa è una notifica di test',
                icon: '/favicon.ico',
                badge: '/favicon.ico',
                tag: 'test-notification',
                requireInteraction: false,
                data: {
                    url: '/'
                }
            });

            return { success: true };

        } catch (error) {
            console.error('[PushNotifications] Errore test notification:', error);
            return { success: false, error: error.message };
        }
    },

    // ? Utility: Converti VAPID key da Base64 a Uint8Array
    urlBase64ToUint8Array: function(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/\-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    },

    // ? Listener per messaggi dal Service Worker
    listenToServiceWorker: function(callback) {
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.addEventListener('message', (event) => {
                console.log('[PushNotifications] Message from SW:', event.data);
                
                if (event.data.type === 'NAVIGATE' && event.data.url) {
                    // Naviga all'URL ricevuto
                    window.location.href = event.data.url;
                }

                if (callback) {
                    callback(event.data);
                }
            });
        }
    }
};

console.log('[PushNotifications] Helper loaded. Supported:', window.PushNotifications.isSupported());
