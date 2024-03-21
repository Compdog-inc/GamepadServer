(function () {
    'use strict';

    /** crypto.randomUUID polyfill */
    try {
        if (typeof crypto === 'undefined')
            var crypto = require('crypto');

        if (!('randomUUID' in crypto))
            // https://stackoverflow.com/a/2117523/2800218
            // LICENSE: https://creativecommons.org/licenses/by-sa/4.0/legalcode
            crypto.randomUUID = function randomUUID() {
                return (
                    [1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g,
                        c => (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
                    );
            };
    } catch (e) { alert(e); }
    /** crypto.randomUUID polyfill */

    const getCid = () => {

        let cid = localStorage.getItem('cid');

        if (!cid) {
            cid = crypto.randomUUID();
            localStorage.setItem('cid', cid);
        }

        return cid;
    };

    // Create WebSocket connection.
    const socket = new WebSocket("ws://" + location.hostname + ":3000");

    // Connection opened
    socket.addEventListener("open", (event) => {
        console.log("[INFO]: Socket OPEN");

        try {
            const cid = getCid();
            console.log("[INFO]: Sending CID '" + cid + "'");
            socket.send("CID" + cid);
        } catch (e) {
            alert("Error connecting!\n" + e);
        }
    });

    // Listen for messages
    socket.addEventListener("message", (event) => {
        const data = JSON.parse(event.data);
        console.log("[SERVER]", data);
    });

    let globalData = {};

    const sendData = () => {
        socket.send(JSON.stringify(globalData));
    };

    window.addEventListener('load', () => {
        const joyl = new JoyStick('joyl', {}, (data) => {
            const x = Math.max(-1, Math.min(1, data.x / 100.0));
            const y = Math.max(-1, Math.min(1, data.y / 100.0));
            globalData.lx = Math.floor(x * 32767);
            globalData.ly = Math.floor(y * 32767);
            sendData();
        });

        const joyr = new JoyStick('joyr', {}, (data) => {
            const x = Math.max(-1, Math.min(1, data.x / 100.0));
            const y = Math.max(-1, Math.min(1, data.y / 100.0));
            globalData.rx = Math.floor(x * 32767);
            globalData.ry = Math.floor(y * 32767);
            sendData();
        });

        const joytl = new JoyStick('tl', {}, (data) => {
            const x = Math.max(-1, Math.min(1, data.x / 100.0));
            const y = Math.max(-1, Math.min(1, data.y / 100.0));
            globalData.tl = Math.floor(Math.abs(y) * 255);
            sendData();
        });

        const joytr = new JoyStick('tr', {}, (data) => {
            const x = Math.max(-1, Math.min(1, data.x / 100.0));
            const y = Math.max(-1, Math.min(1, data.y / 100.0));
            globalData.tr = Math.floor(Math.abs(y) * 255);
            sendData();
        });
    });
})();