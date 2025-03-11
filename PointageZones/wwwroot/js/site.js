if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        // Chemin relatif à la racine du site
        navigator.serviceWorker.register('/service-worker.js')
            .then(registration => {
                // Succès ! Log important à vérifier
                console.log('Service Worker enregistré avec succès, scope:', registration.scope);
            })
            .catch(error => {
                // Erreur ! Log important à vérifier
                console.log('Échec de l\'enregistrement du Service Worker:', error);
            });
    });
} else {
    console.log('Service Worker non supporté par ce navigateur.');
}

/*
const publicVapidKey = 'BBF7sQPoubtDw1QK9Xu0GOpcpQBAGUj9L-sX_HBrRVqeZsR40PnPZV0mthVv-VWLsksV2LwDTnYCqVM7yJUaOQM';

async function subscribeUserToPush() {
    const registration = await navigator.serviceWorker.ready;

    const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicVapidKey)
    });

    // Send to backend
    await fetch('/api/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(subscription)
    });
}

// Utility to convert base64 to UInt8Array
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/\-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    return Uint8Array.from([...rawData].map(char => char.charCodeAt(0)));
}

// Call it once user is logged in or visiting the app
subscribeUserToPush();*/
