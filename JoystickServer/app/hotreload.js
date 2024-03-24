if (!window.__HOTRELOAD) {
    window.__HOTRELOAD = () => { };

    (async function () {
        'use strict';

        const config = await (await fetch("/app/api/config")).json();

        /**
         * @param {string} path 
         */
        const updatePath = async (path) => {
            if (path.endsWith(".html")) {
                if (path === location.pathname) {
                    const doc = document.open();
                    const html = await fetch(path, {
                        cache: 'reload'
                    });
                    doc.write(await html.text());
                    doc.close();
                }
            } else if (path.endsWith(".js")) {
                const file = await fetch(path, {
                    cache: 'reload'
                });

                const parts = path.split('/');
                const filename = parts[parts.length - 1];
                const ind = filename.indexOf('.');
                const filenameNoExt = filename.substring(0, ind === -1 ? undefined : ind)
                const unload = window["__" + filenameNoExt.toUpperCase()];
                if (typeof unload === 'function')
                    unload();
                else if (typeof unload === 'object' && unload instanceof Array && unload.length >= 2 && typeof (unload[0]) === 'function' && typeof (unload[1]) === 'function')
                    unload[0]();
                window["__" + filenameNoExt.toUpperCase()] = undefined;
                const ret = new Function(await file.text())();
                if (ret instanceof Promise && (unload !== 'object' || unload[2] === true)) {
                    await ret;
                }
                const reload = window["__" + filenameNoExt.toUpperCase()];
                if (typeof reload === 'object' && reload instanceof Array && reload.length >= 2 && typeof (reload[0]) === 'function' && typeof (reload[1]) === 'function')
                    reload[1]();
            } else {
                location.reload();
            }
        };

        // Create WebSocket connection.
        if (location.hostname.length > 0) {
            // Create hot reload tunnel
            if (config.HotReload) {
                /** @type WebSocket */
                let hotreload_socket = null;
                const connectHotreload = (desynced) => () => {
                    try {
                        hotreload_socket = new WebSocket("wss://" + location.hostname + ":" + config.HotReload.toFixed());

                        // Connection opened
                        hotreload_socket.addEventListener("open", () => {
                            console.log("[HOTRELOAD]: Socket OPEN");
                            if (desynced) {
                                // reload page because of potential desync between server and client states
                                location.reload();
                            }
                        });

                        // Listen for messages
                        hotreload_socket.addEventListener("message", async (e) => {
                            if (e.data instanceof Blob) {
                                const bytes = new Uint8Array(await e.data.arrayBuffer());
                                if (bytes[0] === 0x69 && bytes[1] === 0x42 && bytes[2] === 0x13 && bytes[3] === 0x37) {
                                    // perform full reload (after deep copy)
                                    location.reload();
                                }
                            } else if (typeof (e.data) === 'string') {
                                updatePath(e.data);
                            }
                        });

                        hotreload_socket.addEventListener('close', () => {
                            setTimeout(connectHotreload(true), 1000);
                        });
                    } catch {
                        console.log("[HOTRELOAD]: Error connecting to server");
                        setTimeout(connectHotreload(true), 1000);
                    }
                };
                connectHotreload(false)();
            }
        }
    })();
} else {
    // run html-dependent code
    document.addEventListener('DOMContentLoaded', () => {
        (window.__ONLOAD || []).forEach(v => v());
    });
}