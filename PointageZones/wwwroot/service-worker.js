// wwwroot/service-worker.js

// --- Configuration ---
const STATIC_CACHE_NAME = 'pointage-static-v6'; // Incrémentez pour forcer la màj des assets statiques
const DYNAMIC_CACHE_NAME = 'pointage-dynamic-v6'; // Incrémentez si logique de cache dynamique change

// --- Assets à pré-cacher (App Shell) ---
// !!! ADAPTEZ CETTE LISTE À VOTRE PROJET !!!
const STATIC_ASSETS = [
    '/Agent/ScannerQRCode', 
    '/offline.html',          // Page de fallback hors ligne (ASSUREZ-VOUS DE L'AVOIR CRÉÉE DANS wwwroot)
    // --- CSS ---
    '/css/site.css',          // Votre CSS principal
    '/lib/bootstrap/dist/css/bootstrap.min.css', // Exemple Bootstrap (vérifiez le chemin)
    // Ajoutez d'autres CSS essentiels...
    // --- JavaScript ---
    // Mettez ici les chemins LOCAUX si vous les avez téléchargés, sinon le SW essaiera de cacher les URL CDN si elles sont dans STATIC_ASSETS
    '/js/site.js',            // Votre JS principal
    '/js/db.js',              // Votre définition IndexedDB
    '/lib/dexie.js',          // !! Chemin local vers Dexie (adaptez si besoin: dexie.min.js?)
    '~/lib/chart.js',           // !! Chemin local vers Chart.js (adaptez si besoin: chart.min.js?)
    '/lib/html5-qrcode.min.js',// !! Chemin local vers la lib QR Code (adaptez si besoin)
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js', // Exemple Bootstrap JS (vérifiez le chemin)
    // Ajoutez d'autres JS essentiels...
    // --- Images/Icônes ---
    '/favicon.ico',
    '/images/logo_mini_s_192x192.png', 
    '/images/logo_mini_s.png', 
    // --- Manifest ---
    '/manifest.json'
    // --- Fonts ---
    // Ajoutez les polices si locales...
];

// --- Inclusion de Dexie.js pour la Synchro ---
// !!! ADAPTEZ CE CHEMIN SI DEXIE EST LOCAL !!!
try {
    // Utilisez le chemin vers votre fichier Dexie local
    importScripts('/lib/dexie.js'); // ou '/lib/dexie.min.js'
    console.log("[Service Worker] Dexie.js importé avec succès.");
} catch (e) {
    console.error("[Service Worker] Échec de l'importation de Dexie.js. La synchro ne fonctionnera pas:", e);
}

// Redéfinir la DB et ses stores EXACTEMENT comme dans db.js pour l'utiliser dans le SW
let dbSync;
if (typeof Dexie !== 'undefined') {
    dbSync = new Dexie('pointageAppDB'); // Doit correspondre au nom dans db.js
    dbSync.version(6).stores({ // Doit correspondre à la version et aux stores dans db.js
        toursData: '&tourId, refTour',
        pendingScans: '&pointageId, planTourId, timestamp, qrCodeText'
    }).upgrade(tx => {
        console.log("Database upgraded to version 6!");
        // You can perform data migrations or other tasks here if needed.
    });
    console.log("[Service Worker] Schéma DB pour la synchro défini.");
} else {
    console.error("[Service Worker] Dexie non défini, impossible de configurer la DB pour la synchro.");
}


// --- Installation du Service Worker ---
self.addEventListener('install', event => {
    console.log('[Service Worker] Installation...');
    event.waitUntil(
        caches.open(STATIC_CACHE_NAME)
            .then(cache => {
                console.log('[Service Worker] Pré-cache de l\'App Shell...');
                // Filtrer les URLs potentiellement problématiques avant addAll
                const urlsToCache = STATIC_ASSETS.filter(url => url && typeof url === 'string' && (url.startsWith('/') || url.startsWith('http')));
                console.log('[Service Worker] URLs à pré-cacher:', urlsToCache);
                return cache.addAll(urlsToCache);
            })
            .then(() => {
                console.log('[Service Worker] App Shell pré-cachée avec succès.');
                return self.skipWaiting(); // Active le nouveau SW immédiatement
            })
            .catch(error => {
                console.error('[Service Worker] Échec du pré-cache:', error);
            })
    );
});

// --- Activation du Service Worker ---
//self.addEventListener('activate', event => {
//    console.log('[Service Worker] Activation...');
//    event.waitUntil(
//        caches.keys().then(keys => {
//            return Promise.all(keys
//                .filter(key => key !== STATIC_CACHE_NAME && key !== DYNAMIC_CACHE_NAME)
//                .map(key => {
//                    console.log(`[Service Worker] Suppression de l'ancien cache: ${key}`);
//                    return caches.delete(key);
//                })
//            );
//        }).then(() => {
//            console.log('[Service Worker] Anciens caches supprimés.');
//            return self.clients.claim();
//        })
//    );
//});

self.addEventListener('activate', event => {
    const allowedCaches = [STATIC_CACHE_NAME, DYNAMIC_CACHE_NAME]; // Liste des caches autorisés
    event.waitUntil(
        caches.keys().then(keys => {
            return Promise.all(
                keys.map(key => {
                    if (!allowedCaches.includes(key)) {
                        console.log(`[Service Worker] Suppression de l'ancien cache : ${key}`);
                        return caches.delete(key);
                    }
                })
            );
        })
    );
});

// --- Interception des requêtes (Fetch) ---
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (event.request.method !== 'GET') { return; }
    if (!url.protocol.startsWith('http')) { return; }

    const isStaticAssetRequest = STATIC_ASSETS.some(assetUrl => url.href === new URL(assetUrl, self.location.origin).href) ||
        STATIC_ASSETS.includes(url.pathname) ||
        url.pathname.startsWith('/lib/') ||
        url.pathname.startsWith('/css/') ||
        url.pathname.startsWith('/js/') ||
        url.pathname.startsWith('/images/') ||
        url.pathname.startsWith('/fonts/');

    // Stratégie 1: Cache First pour les assets statiques
    if (isStaticAssetRequest) {
        event.respondWith(
            caches.match(event.request).then(cachedResponse => {
                if (cachedResponse) return cachedResponse;
                return fetch(event.request).catch(err => console.error(`[SW Fetch] Erreur fetch asset statique ${url.pathname}:`, err));
            })
        );
    }
    else if (event.request.mode === 'navigate') {
        // Case 1: DebutTour (Network First)
        if (url.pathname.startsWith('/Agent/DebutTour')) {
            event.respondWith(
                fetch(event.request)
                    .then(fetchResponse => {
                        const clonedResponse = fetchResponse.clone();
                        if (fetchResponse.ok) {
                            caches.open(DYNAMIC_CACHE_NAME)
                                .then(cache => cache.put(event.request, clonedResponse));
                        }
                        return fetchResponse;
                    })
                    .catch(() => caches.match(event.request)
                        .then(cachedResponse => cachedResponse || caches.match('/offline.html'))
                    )
            );
        }
        // Case 2: ScannerQRCode - Amélioration pour fonctionnement hors ligne
        else if (url.pathname.startsWith('/Agent/ScannerQRCode')) {
            event.respondWith(
                // Essayer d'abord le cache pour tout chemin ScannerQRCode
                caches.match(event.request)
                    .then(cachedResponse => {
                        // Si trouvé en cache, retourner immédiatement
                        if (cachedResponse) {
                            console.log(`[SW Fetch] Réponse trouvée en cache pour ${url.pathname}`);
                            return cachedResponse;
                        }

                        // Sinon essayer le réseau et mettre en cache
                        console.log(`[SW Fetch] Requête réseau pour ${url.pathname}`);
                        return fetch(event.request)
                            .then(networkResponse => {
                                // Mettre en cache pour utilisation hors ligne future
                                const clonedResponse = networkResponse.clone();
                                if (networkResponse.ok) {
                                    caches.open(DYNAMIC_CACHE_NAME)
                                        .then(cache => {
                                            console.log(`[SW Fetch] Mise en cache de ${url.pathname}`);
                                            cache.put(event.request, clonedResponse);
                                        });
                                }
                                return networkResponse;
                            })
                            .catch(error => {
                                console.warn(`[SW Fetch] Échec réseau pour ${url.pathname}. Recherche alternatives...`);

                                // Si c'est une URL avec paramètre et qu'on est hors ligne,
                                // essayer de servir la page ScannerQRCode de base
                                if (url.pathname !== '/Agent/ScannerQRCode') {
                                    return caches.match('/Agent/ScannerQRCode')
                                        .then(basePageResponse => {
                                            if (basePageResponse) {
                                                console.log(`[SW Fetch] Retourne la page ScannerQRCode de base`);
                                                return basePageResponse;
                                            }

                                            // En dernier recours, page hors ligne
                                            console.log(`[SW Fetch] Retourne la page offline.html`);
                                            return caches.match('/offline.html');
                                        });
                                }

                                // Fallback à la page hors ligne
                                return caches.match('/offline.html');
                            });
                    })
            );
        }
    }
    // Stratégie 3: Network First, Cache Fallback pour les appels API
    else if (url.pathname.startsWith('/api/')) {
        event.respondWith(
            // Essayer d'abord le cache pour les API
            caches.match(event.request)
                .then(cachedResponse => {
                    if (cachedResponse) {
                        console.log(`[SW Fetch] Réponse API trouvée en cache pour ${url.pathname}`);

                        // Mise à jour du cache en arrière-plan (stale-while-revalidate)
                        fetch(event.request)
                            .then(networkResponse => {
                                if (networkResponse.ok) {
                                    caches.open(DYNAMIC_CACHE_NAME)
                                        .then(cache => cache.put(event.request, networkResponse.clone()));
                                }
                            })
                            .catch(() => console.log(`[SW Fetch] Échec de mise à jour du cache API pour ${url.pathname}`));

                        return cachedResponse;
                    }

                    // Sinon, essayer le réseau
                    return fetch(event.request)
                        .then(networkResponse => {
                            const clonedResponse = networkResponse.clone();
                            if (networkResponse.ok) {
                                caches.open(DYNAMIC_CACHE_NAME)
                                    .then(cache => cache.put(event.request, clonedResponse));
                            }
                            return networkResponse;
                        })
                        .catch(error => {
                            console.warn(`[SW Fetch] Échec réseau API ${url.pathname}. Pas de cache disponible.`);
                            return new Response(JSON.stringify({
                                error: true,
                                message: 'Hors ligne et donnée non disponible en cache.',
                                offline: true
                            }), {
                                status: 503,
                                headers: { 'Content-Type': 'application/json' }
                            });
                        });
                })
        );
    }
});
// --- Gestion des Notifications Push ---
self.addEventListener('push', event => {
    console.log('[Service Worker] Push received');

    let notificationData = {
        title: 'Pointage Zones',
        body: 'Notification',
        icon: '/images/icons/icon-192x192.png',
        data: { url: '/' }
    };

    if (event.data) {
        try {
            const data = event.data.json();
            notificationData.title = data.title || notificationData.title;
            notificationData.body = data.body || notificationData.body;
            notificationData.icon = data.icon || notificationData.icon;
            if (data.url) notificationData.data = { url: data.url };
        } catch (e) {
            notificationData.body = event.data.text();
        }
    }

    const options = {
        body: notificationData.body,
        icon: notificationData.icon,
        badge: '/images/icons/icon-96x96.png',
        vibrate: [100, 50, 100], 
        data: notificationData.data,
        tag: 'pointage-notification',
        /*
        actions: [
            {
                action: 'open',
                title: 'Ouvrir'
            }
        ],*/
        // The notification will be automatically closed when clicked
        requireInteraction: true,
        renotify: true
    };

    event.waitUntil(
        self.registration.showNotification(notificationData.title, options)
    );
});

// --- Gestion du clic sur la notification ---
self.addEventListener('notificationclick', event => {
    console.log('[Service Worker] Notification click received');
    event.notification.close();

    const urlToOpen = event.notification.data?.url || '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
            const matchingClient = windowClients.find(client =>
                client.url === new URL(urlToOpen, self.location.origin).href && 'focus' in client
            );

            if (matchingClient) {
                return matchingClient.focus();
            } else {
                return clients.openWindow(urlToOpen);
            }
        })
    );
});


// --- Gestion de la Synchronisation en Arrière-Plan ---
self.addEventListener('sync', event => {
    if (event.tag === 'sync-pending-scans') {
        console.log('[Service Worker] Événement Sync reçu pour "sync-pending-scans"');
        if (!dbSync) { console.error("[Service Worker] La base de données (dbSync) n'est pas disponible pour la synchro !"); return; }
        event.waitUntil(syncPendingScans());
    }
});

// --- Fonction pour synchroniser les scans en attente ---
async function syncPendingScans() {
    if (!dbSync) { console.error("[syncPendingScans] dbSync n'est pas disponible."); return Promise.reject("dbSync not available"); }
    console.log("[syncPendingScans] Début de la tentative de synchronisation...");
    let firstError = null;

    try {
        const scansToSync = await dbSync.pendingScans.toArray();
        if (!scansToSync.length) { console.log('[syncPendingScans] Aucun scan en attente.'); return Promise.resolve(); }

        console.log(`[syncPendingScans] ${scansToSync.length} scan(s) trouvé(s).`);

        for (const scan of scansToSync) {
            console.log(`[syncPendingScans] Tentative pour scan ID local: ${scan.pointageId}`, scan);
            try {
                const response = await fetch('/Agent/ValiderQRCode', { // !! VÉRIFIEZ URL !!
                    method: 'POST', headers: { 'Content-Type': 'application/json', },
                    body: JSON.stringify({
                        qrCodeText: scan.qrCodeText, 
                        PlanTourId: scan.planTourId,
                        datetimescan: scan.timestamp,
                        PointageId: scan.pointageId

                    })
                });

                if (response.ok) {
                    const result = await response.json();
                    console.log(`[syncPendingScans] Succès synchro ID local ${scan.pointageId}. Réponse:`, result);
                    await dbSync.pendingScans.delete(scan.pointageId);
                    console.log(`[syncPendingScans] Scan ID local ${scan.pointageId} supprimé.`);
                } else {
                    const errorText = await response.text();
                    console.error(`[syncPendingScans] Échec synchro ID local ${scan.pointageId}. Statut: ${response.status}. Réponse: ${errorText}`);
                    if (response.status >= 400 && response.status < 500) {
                        console.warn(`[syncPendingScans] Suppression ID local ${scan.pointageId} car rejet permanent (${response.status}).`);
                        await dbSync.pendingScans.delete(scan.pointageId);
                    } else {
                        if (!firstError) firstError = new Error(`Server error ${response.status} for scan ID ${scan.pointageId}`);
                    }
                }
            } catch (networkError) {
                console.error(`[syncPendingScans] Erreur réseau pour ID local ${scan.pointageId}.`, networkError);
                if (!firstError) firstError = networkError;
                // Ne pas supprimer, la synchro réessaiera. Arrêter éventuellement ici : throw networkError;
            }
        } // Fin boucle for

        console.log('[syncPendingScans] Traitement de la file terminé.');
        if (firstError) {
            console.warn("[syncPendingScans] Au moins une erreur serveur/réseau rencontrée, la synchro réessaiera.");
            throw firstError; // Rejeter pour que le SyncManager réessaie
        }
        return Promise.resolve();

    } catch (dbError) {
        console.error('[syncPendingScans] Échec global de la synchronisation.', dbError);
        throw dbError; // Rejeter pour que le SyncManager réessaie
    }
}