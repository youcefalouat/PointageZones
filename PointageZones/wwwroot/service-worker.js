// wwwroot/service-worker.js

// --- Configuration ---
const STATIC_CACHE_NAME = 'pointage-static-v8';
const DYNAMIC_CACHE_NAME = 'pointage-dynamic-v9';

// --- Assets à pré-cacher (App Shell) ---
const STATIC_ASSETS = [
    '/offline.html',
    '/css/site.css',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/js/site.js',
    '/js/db.js',
    '/lib/dexie.js',
    '/lib/html5-qrcode.min.js',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/favicon.ico',
    '/images/logo_mini_s_192x192.png',
    '/images/logo_mini_s.png',
    '/manifest.json'
];

// --- Inclusion de Dexie.js pour la Synchro ---
try {
    importScripts('/lib/dexie.js');
    console.log("[Service Worker] Dexie.js importé avec succès.");
} catch (e) {
    console.error("[Service Worker] Échec de l'importation de Dexie.js. La synchro ne fonctionnera pas:", e);
}

// Redéfinir la DB et ses stores EXACTEMENT comme dans db.js
let dbSync;
if (typeof Dexie !== 'undefined') {
    dbSync = new Dexie('pointageAppDB');
    dbSync.version(6).stores({
        toursData: '&tourId, refTour',
        pendingScans: '&pointageId, planTourId, timestamp, qrCodeText'
    }).upgrade(tx => {
        console.log("Database upgraded to version 6!");
    });
    console.log("[Service Worker] Schéma DB pour la synchro défini.");
} else {
    console.error("[Service Worker] Dexie non défini, impossible de configurer la DB pour la synchro.");
}

// --- Fonction utilitaire pour vérifier si une réponse est la vraie page QR ---
function isValidQRScannerPage(responseText) {
    // Vérifie la présence d'éléments spécifiques à la page QR scanner
    return responseText.includes('id="reader"') &&
        responseText.includes('Html5Qrcode') &&
        !responseText.includes('id="Input_UserName"') && // Pas la page de login
        !responseText.includes('form-signin'); // Pas la page de login
}

// --- Installation du Service Worker ---
self.addEventListener('install', event => {
    console.log('[Service Worker] Installation...');
    event.waitUntil(
        (async () => {
            try {
                // Ouvrir le cache statique
                const staticCache = await caches.open(STATIC_CACHE_NAME);

                // Pré-cacher les assets statiques
                console.log('[Service Worker] Pré-cache de l\'App Shell...');
                const urlsToCache = STATIC_ASSETS.filter(url =>
                    url && typeof url === 'string' && (url.startsWith('/') || url.startsWith('http'))
                );
                console.log('[Service Worker] URLs à pré-cacher:', urlsToCache);
                await staticCache.addAll(urlsToCache);

                // Ouvrir le cache dynamique pour la page QR
                const dynamicCache = await caches.open(DYNAMIC_CACHE_NAME);

                // Pré-cacher la page QR scanner de base (sans paramètres)
                console.log('[Service Worker] Pré-cache de la page QR scanner...');
                try {
                    const qrResponse = await fetch('/Agent/ScannerQRCode', {
                        credentials: 'include',
                        headers: { 'Cache-Control': 'no-cache' }
                    });

                    if (qrResponse.ok) {
                        const responseText = await qrResponse.text();
                        if (isValidQRScannerPage(responseText)) {
                            await dynamicCache.put('/Agent/ScannerQRCode', qrResponse.clone());
                            console.log('[Service Worker] Page QR scanner pré-cachée avec succès');
                        } else {
                            console.log('[Service Worker] Page de login détectée lors du pré-cache, pas de mise en cache');
                        }
                    }
                } catch (qrError) {
                    console.warn('[Service Worker] Impossible de pré-cacher la page QR:', qrError);
                }

                console.log('[Service Worker] Installation terminée avec succès.');
                return self.skipWaiting();

            } catch (error) {
                console.error('[Service Worker] Échec de l\'installation:', error);
                throw error;
            }
        })()
    );
});

// --- Activation du Service Worker ---
self.addEventListener('activate', event => {
    const allowedCaches = [STATIC_CACHE_NAME, DYNAMIC_CACHE_NAME];
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
        }).then(() => {
            return self.clients.claim();
        })
    );
});

// --- Interception des requêtes (Fetch) ---
self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (event.request.method !== 'GET') { return; }
    if (!url.protocol.startsWith('http')) { return; }

    const isStaticAssetRequest = STATIC_ASSETS.some(assetUrl =>
        url.href === new URL(assetUrl, self.location.origin).href
    ) ||
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
                return fetch(event.request).catch(err =>
                    console.error(`[SW Fetch] Erreur fetch asset statique ${url.pathname}:`, err)
                );
            })
        );
        return;
    }

    // Stratégie spéciale pour la page ScannerQRCode
    if (url.pathname.startsWith('/Agent/ScannerQRCode')) {
        event.respondWith(
            (async () => {
                console.log(`[SW Fetch] Requête pour ${url.pathname}${url.search}`);

                // D'abord vérifier le cache pour la page de base
                const cachedResponse = await caches.match('/Agent/ScannerQRCode');

                try {
                    // Essayer le réseau avec les credentials
                    console.log(`[SW Fetch] Tentative réseau pour ${url.pathname}${url.search}`);
                    const networkResponse = await fetch(event.request, {
                        credentials: 'include',
                        headers: {
                            'Cache-Control': 'no-cache'
                        }
                    });

                    if (networkResponse.ok) {
                        const responseText = await networkResponse.text();

                        // Vérifier si c'est la vraie page QR scanner
                        if (isValidQRScannerPage(responseText)) {
                            console.log(`[SW Fetch] Page QR scanner valide reçue du réseau`);

                            // Mettre à jour le cache avec la nouvelle version
                            const cache = await caches.open(DYNAMIC_CACHE_NAME);
                            const responseToCache = new Response(responseText, {
                                status: networkResponse.status,
                                statusText: networkResponse.statusText,
                                headers: networkResponse.headers
                            });
                            await cache.put('/Agent/ScannerQRCode', responseToCache);
                            console.log(`[SW Fetch] Cache de la page QR scanner mis à jour`);

                            // Retourner la réponse réseau
                            return new Response(responseText, {
                                status: networkResponse.status,
                                statusText: networkResponse.statusText,
                                headers: networkResponse.headers
                            });
                        } else {
                            console.log(`[SW Fetch] Page de login détectée du réseau`);

                            // Si on a une page de login du réseau mais une page QR en cache, utiliser le cache
                            if (cachedResponse) {
                                const cachedText = await cachedResponse.text();
                                if (isValidQRScannerPage(cachedText)) {
                                    console.log(`[SW Fetch] Utilisation de la page QR scanner en cache à la place de la page de login`);
                                    return new Response(cachedText, {
                                        status: cachedResponse.status,
                                        statusText: cachedResponse.statusText,
                                        headers: cachedResponse.headers
                                    });
                                }
                            }

                            // Sinon retourner la page de login
                            return networkResponse;
                        }
                    } else {
                        console.warn(`[SW Fetch] Réponse réseau non-OK (${networkResponse.status})`);

                        // En cas d'erreur serveur, essayer le cache
                        if (cachedResponse) {
                            const cachedText = await cachedResponse.text();
                            if (isValidQRScannerPage(cachedText)) {
                                console.log(`[SW Fetch] Utilisation de la page QR scanner en cache (erreur serveur)`);
                                return new Response(cachedText, {
                                    status: cachedResponse.status,
                                    statusText: cachedResponse.statusText,
                                    headers: cachedResponse.headers
                                });
                            }
                        }

                        return networkResponse;
                    }
                } catch (networkError) {
                    console.warn(`[SW Fetch] Erreur réseau pour ${url.pathname}:`, networkError);

                    // En cas d'erreur réseau, essayer le cache
                    if (cachedResponse) {
                        const cachedText = await cachedResponse.text();
                        if (isValidQRScannerPage(cachedText)) {
                            console.log(`[SW Fetch] Utilisation de la page QR scanner en cache (hors ligne)`);
                            return new Response(cachedText, {
                                status: cachedResponse.status,
                                statusText: cachedResponse.statusText,
                                headers: cachedResponse.headers
                            });
                        }
                    }

                    // Fallback vers la page offline
                    console.log(`[SW Fetch] Aucune page QR valide en cache, redirection vers offline`);
                    return caches.match('/offline.html') ||
                        new Response('Page non disponible hors ligne', { status: 503 });
                }
            })()
        );
        return;
    }

    // Stratégie pour les pages de navigation (Network First)
    if (event.request.mode === 'navigate') {
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
                .catch(() =>
                    caches.match(event.request)
                        .then(cachedResponse => cachedResponse || caches.match('/offline.html'))
                )
        );
        return;
    }

    // Stratégie pour les appels API (Cache First avec mise à jour en arrière-plan)
    if (url.pathname.startsWith('/api/')) {
        event.respondWith(
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
        return;
    }
});

// --- Gestion des Notifications Push ---
self.addEventListener('push', event => {
    console.log('[Service Worker] Push received');

    let notificationData = {
        title: 'Pointage Zones',
        body: 'Notification',
        icon: '/images/icons/icon-192x192.png',
        badge: '/images/icons/icon-192x192.png',
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
        if (!dbSync) {
            console.error("[Service Worker] La base de données (dbSync) n'est pas disponible pour la synchro !");
            return;
        }
        event.waitUntil(syncPendingScans());
    }
});

// --- Fonction pour synchroniser les scans en attente ---
async function syncPendingScans() {
    if (!dbSync) {
        console.error("[syncPendingScans] dbSync n'est pas disponible.");
        return Promise.reject("dbSync not available");
    }
    console.log("[syncPendingScans] Début de la tentative de synchronisation...");
    let firstError = null;

    try {
        const scansToSync = await dbSync.pendingScans.toArray();
        if (!scansToSync.length) {
            console.log('[syncPendingScans] Aucun scan en attente.');
            return Promise.resolve();
        }

        console.log(`[syncPendingScans] ${scansToSync.length} scan(s) trouvé(s).`);

        for (const scan of scansToSync) {
            console.log(`[syncPendingScans] Tentative pour scan ID local: ${scan.pointageId}`, scan);
            try {
                const response = await fetch('/Agent/ValiderQRCode', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'include',
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
            }
        }

        console.log('[syncPendingScans] Traitement de la file terminé.');
        if (firstError) {
            console.warn("[syncPendingScans] Au moins une erreur serveur/réseau rencontrée, la synchro réessaiera.");
            throw firstError;
        }
        return Promise.resolve();

    } catch (dbError) {
        console.error('[syncPendingScans] Échec global de la synchronisation.', dbError);
        throw dbError;
    }
}