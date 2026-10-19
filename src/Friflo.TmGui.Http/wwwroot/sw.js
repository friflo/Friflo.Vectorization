

// Service Worker to intercept HTTP requests and bridge them to the main window
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);
    
    // Intercept requests matching the bridge path prefix
    /* if (!url.pathname.startsWith('/textures/')) {
        return;
    } */
    const appHeader = event.request.headers.get('X-App-Name');
    if (!appHeader) {
        return;
    }
    /* const requestUrl = new URL(event.request.url);
    const app = requestUrl.searchParams.get('app');
    if (!app) {
        return;
    }*/
    
    event.respondWith(new Promise(async (resolve) =>
    {
        const client = await self.clients.get(event.clientId);        

        if (!client || !appHeader) {
            return fetch(event.request);
        }

        // Create a MessageChannel port pair for asynchronous response handling
        const messageChannel = new MessageChannel();
        messageChannel.port1.onmessage = (e) => {
            resolve(new Response(e.data.body, {
                status: e.data.status,
                headers: e.data.headers
            }));
        };

        // Forward request details to the main browser window thread
        client.postMessage({
            type: 'HTTP_VIA_WEBRTC',
            url: event.request.url,
            method: event.request.method
        }, [messageChannel.port2]);
    }));
});

