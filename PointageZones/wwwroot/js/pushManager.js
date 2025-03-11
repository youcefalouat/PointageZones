console.log("trying subscription to server");

// VAPID Public Key provided by the server (ensure ViewBag.VapidPublicKey is set in your C# controller)
async function getVapidKey() {
    try {
        const response = await fetch('/api/config');
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        const config = await response.json();
        console.log("Received VAPID config:", config);
        return config.VapidPublicKey;
    } catch (error) {
        console.error("Error fetching VAPID key:", error);
        throw error; // Re-throw to let calling code handle it
    }
}
// --- Push Notification Logic Directly in Layout ---

// Function to convert base64 string to Uint8Array for the VAPID key
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding)
        .replace(/-/g, '+')
        .replace(/_/g, '/');
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

// Function to convert ArrayBuffer to base64 string
function arrayBufferToBase64(buffer) {
    if (!buffer) return null;
    const bytes = new Uint8Array(buffer);
    const binary = bytes.reduce((acc, byte) => acc + String.fromCharCode(byte), '');
    return window.btoa(binary);
}

// Function to get the anti-forgery token (if using ASP.NET Core forms authentication)
function getAntiForgeryToken() {
    const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenElement ? tokenElement.value : '';
}

// Send the subscription to the server
async function sendSubscriptionToServer(subscription) {
    const subscriptionData = {
        Endpoint: subscription.endpoint,
        P256DH: arrayBufferToBase64(subscription.getKey('p256dh')),
        Auth: arrayBufferToBase64(subscription.getKey('auth'))
    };
    console.log("Sending subscription to server:", subscriptionData);

    try {
        const response = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken() // Include if your endpoint validates it
            },
            body: JSON.stringify(subscriptionData)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Failed to store subscription on server. Status: ${response.status}, Message: ${errorText}`);
        }
        const responseData = await response.json();
        console.log('Subscription stored on server successfully:', responseData);
        return true;
    } catch (error) {
        console.error('Error storing subscription on server:', error);
        return false;
    }
}

// Register and subscribe to push notifications
async function registerPushSubscription() {
    try {
        console.log("Attempting to register for push notifications...");

        if (!('serviceWorker' in navigator)) {
            console.error('Service Worker not supported in this browser.');
            return false;
        }
        if (!('PushManager' in window)) {
            console.error('PushManager not supported in this browser.');
            return false;
        }

        if (!window.VAPID_PUBLIC_KEY || window.VAPID_PUBLIC_KEY.startsWith('@')) { // Check if it's the placeholder
            console.error('VAPID public key not available or not replaced by server. Ensure ViewBag.VapidPublicKey is set.');
            alert('Push notification setup error: VAPID key missing. Please contact support.');
            return false;
        }

        // Await service worker readiness
        const registration = await navigator.serviceWorker.ready;
        console.log('Service Worker is ready.');

        // Check current subscription status
        let currentSubscription = await registration.pushManager.getSubscription();
        if (currentSubscription) {
            console.log('User is already subscribed:', currentSubscription);
            // Optional: You might want to re-send to server to ensure it's up-to-date
            // await sendSubscriptionToServer(currentSubscription);
            return true; // Already subscribed
        }

        // Request notification permission
        const permissionState = await Notification.requestPermission();
        if (permissionState !== 'granted') {
            console.log('Notification permission not granted by user.');
            // alert('You denied notification permission. Please enable it in browser settings if you want to receive updates.');
            return false;
        }
        console.log('Notification permission granted.');

        // Subscribe to push service
        const applicationServerKey = urlBase64ToUint8Array(window.VAPID_PUBLIC_KEY);
        currentSubscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: applicationServerKey
        });

        console.log('Push subscription successful:', currentSubscription);

        // Send the subscription to the server
        return await sendSubscriptionToServer(currentSubscription);

    } catch (error) {
        console.error('Error registering push subscription:', error);
        if (error.name === 'InvalidStateError') {
            alert('Could not subscribe for push notifications. The VAPID public key might be invalid or the push service is unavailable. Please try again later or contact support.');
        } else if (error.message && error.message.includes("permission")) {
            // Handled by permission check, but good to log
        } else {
            alert('An error occurred while setting up push notifications. Please try again.');
        }
        return false;
    }
}

// Check if push is supported and registered
async function checkPushRegistration() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.log('Push notification not supported by this browser.');
        return false;
    }
    try {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return !!subscription; // true if subscription exists, false otherwise
    } catch (error) {
        console.error('Error checking push registration:', error);
        return false;
    }
}

// Send unsubscribe request to server
async function sendUnsubscribeToServer(subscription) {
    if (!subscription) return false;
    const subscriptionData = {
        endpoint: subscription.endpoint,
        // p256dh and auth might not be strictly needed by server for unsubscribe,
        // but good to send if your server logic uses them to find the record.
        p256dh: arrayBufferToBase64(subscription.getKey('p256dh')),
        auth: arrayBufferToBase64(subscription.getKey('auth'))
    };
    console.log("Sending unsubscribe request to server:", subscriptionData);
    try {
        const response = await fetch('/api/push/unsubscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            },
            body: JSON.stringify(subscriptionData)
        });
        if (!response.ok) {
            throw new Error('Failed to unsubscribe on server');
        }
        console.log('Unsubscribed on server successfully');
        return true;
    } catch (error) {
        console.error('Error unsubscribing on server:', error);
        return false;
    }
}

// Unsubscribe from push notifications
async function unsubscribeFromPush() {
    try {
        if (!('serviceWorker' in navigator)) return false;

        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();

        if (!subscription) {
            console.log('No subscription to unsubscribe from.');
            return true;
        }

        // Send unsubscribe request to server first
        await sendUnsubscribeToServer(subscription);

        // Then remove the subscription from the browser
        const result = await subscription.unsubscribe();
        console.log('Unsubscribed from push notifications in browser:', result);
        return result;
    } catch (error) {
        console.error('Error unsubscribing from push:', error);
        alert('Could not unsubscribe. Please try again.');
        return false;
    }
}


// Function to test notifications (sends request to your server endpoint)
async function testNotification() {
    console.log("Attempting to send a test notification request...");
    try {
        const response = await fetch('/api/push/notify-me-test', { // Ensure this endpoint exists and is configured
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken()
            }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Failed to send test notification: ${response.status} ${errorText}`);
        }
        const result = await response.json();
        console.log('Test notification request successful:', result);
        alert('Test notification sent! Check your notifications.');
        return true;
    } catch (error) {
        console.error('Error during test notification:', error);
        alert(`Error sending test notification: ${error.message}`);
        return false;
    }
}


// --- Initialization and Service Worker Registration ---
async function initializePushManager() {
    if (!('serviceWorker' in navigator)) {
        console.log('Service Worker not supported. Push notifications disabled.');
        return;
    }
    if (!window.VAPID_PUBLIC_KEY || window.VAPID_PUBLIC_KEY.startsWith('@')) {
        console.error('VAPID Public Key is not configured on the window object. Ensure ViewBag.VapidPublicKey is set in your Razor view.');
        return;
    }

    try {
        // Register the service worker
        // Ensure 'sw.js' is in your wwwroot folder and the path is correct.
        const swRegistration = await navigator.serviceWorker.register('/service-worker.js', { scope: '/' });
        console.log('Service Worker registered successfully:', swRegistration);

        // Wait for the service worker to be ready/active
        await navigator.serviceWorker.ready;
        console.log('Service Worker is active and ready.');

        // Check current registration status and attempt to subscribe if not already
        const isRegistered = await checkPushRegistration();
        console.log('Current push registration status:', isRegistered);

        if (!isRegistered) {
            console.log("User not registered for push, attempting to register...");
            // This will trigger the permission prompt if not already granted
            const success = await registerPushSubscription();
            if (success) {
                console.log("Successfully registered for push notifications after initial check.");
                // You could show the test button now if it exists
                const testBtn = document.getElementById('test-push-btn');
                if (testBtn) testBtn.style.display = 'block';
                const unsubBtn = document.getElementById('unsubscribe-push-btn');
                if (unsubBtn) unsubBtn.style.display = 'block';

            } else {
                console.log("Failed to register for push notifications after initial check.");
            }
        } else {
            console.log("User already registered for push notifications.");
            // Show test/unsubscribe buttons if they exist
            const testBtn = document.getElementById('test-push-btn');
            if (testBtn) testBtn.style.display = 'block';
            const unsubBtn = document.getElementById('unsubscribe-push-btn');
            if (unsubBtn) unsubBtn.style.display = 'block';
        }

        // --- Optional: UI Button Event Listeners ---
        // (If you add these buttons to your HTML)

        const enableBtn = document.getElementById('enable-push-btn');
        if (enableBtn) {
            enableBtn.addEventListener('click', async () => {
                const success = await registerPushSubscription();
                if (success) {
                    enableBtn.style.display = 'none';
                    const testBtn = document.getElementById('test-push-btn');
                    if (testBtn) testBtn.style.display = 'block';
                    const unsubBtn = document.getElementById('unsubscribe-push-btn');
                    if (unsubBtn) unsubBtn.style.display = 'block';
                }
            });
        }

        const testBtn = document.getElementById('test-push-btn');
        if (testBtn) {
            testBtn.addEventListener('click', testNotification);
        }

        const unsubBtn = document.getElementById('unsubscribe-push-btn');
        if (unsubBtn) {
            unsubBtn.addEventListener('click', async () => {
                const success = await unsubscribeFromPush();
                if (success) {
                    alert('Successfully unsubscribed.');
                    if (enableBtn) enableBtn.style.display = 'block';
                    if (testBtn) testBtn.style.display = 'none';
                    unsubBtn.style.display = 'none';
                }
            });
        }

        // Initial button states based on registration
        if (isRegistered) {
            if (enableBtn) enableBtn.style.display = 'none';
        } else {
            if (enableBtn) enableBtn.style.display = 'block';
            if (testBtn) testBtn.style.display = 'none';
            if (unsubBtn) unsubBtn.style.display = 'none';
        }


    } catch (error) {
        console.error('Error during push manager initialization:', error);
        alert('Could not initialize push notifications. Please try refreshing the page.');
    }
}

// --- Initialization ---
async function initializeApp() {
    try {
        window.VAPID_PUBLIC_KEY = "BBF7sQPoubtDw1QK9Xu0GOpcpQBAGUj9L-sX_HBrRVqeZsR40PnPZV0mthVv-VWLsksV2LwDTnYCqVM7yJUaOQM";
        console.log("VAPID key loaded:", window.VAPID_PUBLIC_KEY);

        if (!window.VAPID_PUBLIC_KEY) {
            throw new Error('VAPID key not available');
        }

        await initializePushManager();
    } catch (error) {
        console.error("App initialization failed:", error);
        // Show error to user
    }
}

// Start the application
window.addEventListener('load', initializeApp);