if (!window.__APP) {
    (async function () {
        'use strict';

        /** crypto.randomUUID polyfill */
        try {
            if (typeof crypto === 'undefined') {
                crypto = {
                    randomUUID: function () {
                        return "10000000-1000-4000-8000-100000000000".replace(/[018]/g, c =>
                            (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
                        );
                    }
                };
            } else {

                if (!('randomUUID' in crypto))
                    // https://stackoverflow.com/a/2117523/2800218
                    // LICENSE: https://creativecommons.org/licenses/by-sa/4.0/legalcode
                    crypto.randomUUID = function randomUUID() {
                        return (
                            [1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g,
                                c => (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
                            );
                    };
            }
        } catch (e) { alert(e); }
        /** crypto.randomUUID polyfill */

        /**
         * @type WebSocket
         */
        let socket = null;

        let globalData = {};

        const CONTROL_XBOX_TOUCH = 0;
        const CONTROL_JOYSTICK_GYRO = 1;
        const CONTROL_MODE = CONTROL_JOYSTICK_GYRO;

        const sendData = () => {
            if (socket != null && socket.readyState === WebSocket.OPEN)
                socket.send(JSON.stringify(globalData));
        };

        const loadable = () => {
            const joyl = new JoyStick('joyl', {}, (data) => {
                if (CONTROL_MODE === CONTROL_XBOX_TOUCH) {
                    const x = Math.max(-1, Math.min(1, data.x / 100.0));
                    const y = Math.max(-1, Math.min(1, data.y / 100.0));
                    globalData.lx = Math.floor(x * 32767);
                    globalData.ly = Math.floor(y * 32767);
                    sendData();
                }
            });

            const joyr = new JoyStick('joyr', {}, (data) => {
                const x = Math.max(-1, Math.min(1, data.x / 100.0));
                const y = Math.max(-1, Math.min(1, data.y / 100.0));
                globalData.rx = Math.floor(x * 32767);
                globalData.ry = Math.floor(y * 32767);
                sendData();
            });

            const joytl = new JoyStick('tl', {}, (data) => {
                if (CONTROL_MODE === CONTROL_XBOX_TOUCH) {
                    const x = Math.max(-1, Math.min(1, data.x / 100.0));
                    const y = Math.max(-1, Math.min(1, data.y / 100.0));
                    globalData.tl = Math.floor(Math.abs(y) * 255);
                    sendData();
                }
            });

            const joytr = new JoyStick('tr', {}, (data) => {
                if (CONTROL_MODE === CONTROL_XBOX_TOUCH) {
                    const x = Math.max(-1, Math.min(1, data.x / 100.0));
                    const y = Math.max(-1, Math.min(1, data.y / 100.0));
                    globalData.tr = Math.floor(Math.abs(y) * 255);
                    sendData();
                }
            });

            const btnY = new ControllerButton('by', "Y", BTN_YELLOW, (data) => {
                globalData.by = data.pressed;
                sendData();
            });
            const btnB = new ControllerButton('bb', "B", BTN_RED, (data) => {
                globalData.bb = data.pressed;
                sendData();
            });
            const btnA = new ControllerButton('ba', "A", BTN_GREEN, (data) => {
                globalData.ba = data.pressed;
                sendData();
            });
            const btnX = new ControllerButton('bx', "X", BTN_BLUE, (data) => {
                globalData.bx = data.pressed;
                sendData();
            });
        };


        window.Controls = {
            receiveGyro: (rot) => {
                if (CONTROL_MODE === CONTROL_JOYSTICK_GYRO) {
                    rot[0] = rot[0] - 130;
                    rot[1] = -(rot[1] - 90);
                    rot[2] = -rot[2];
                    console.log(rot);

                    const x = Math.max(-1, Math.min(1, rot[1] / 40.0));
                    const y = Math.max(-1, Math.min(1, rot[2] / 40.0));
                    const tl = Math.max(0, Math.min(1, -rot[0] / 20.0));
                    const tr = Math.max(0, Math.min(1, rot[0] / 20.0));

                    globalData.lx = Math.floor(x * 32767);
                    globalData.ly = Math.floor(y * 32767);
                    globalData.tl = Math.floor(tl * 255);
                    globalData.tr = Math.floor(tr * 255);

                    sendData();
                }
            }
        };

        window.addEventListener('load', loadable);

        window.__ONLOAD = (window.__ONLOAD || []);
        window.__ONLOAD.push(loadable);

        window.__APP = [() => {
            console.log('a')
            document.getElementById("joyl").innerHTML = "";
            document.getElementById("joyr").innerHTML = "";
            document.getElementById("tl").innerHTML = "";
            document.getElementById("tr").innerHTML = "";
            document.getElementById("by").innerHTML = "";
            document.getElementById("bb").innerHTML = "";
            document.getElementById("ba").innerHTML = "";
            document.getElementById("bx").innerHTML = "";
            if (socket)
                socket.close();
        }, loadable];

        const config = await (await fetch("/app/api/config")).json();

        const getCid = () => {

            let cid = localStorage.getItem('cid');

            if (!cid) {
                cid = crypto.randomUUID();
                localStorage.setItem('cid', cid);
            }

            return cid;
        };

        // Create WebSocket connection.
        if (location.hostname.length > 0) {
            const connectSocket = () => {
                try {
                    socket = new WebSocket("wss://" + location.hostname + ":" + config.Port.toFixed());

                    // Connection opened
                    socket.addEventListener("open", () => {
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
                    socket.addEventListener("message", (e) => {
                        const data = JSON.parse(e.data);
                        console.log("[SERVER]", data);
                    });

                    socket.addEventListener('close', () => {
                        setTimeout(connectSocket, 1000);
                    });
                } catch {
                    console.log("[INFO]: Error connecting to server");
                    setTimeout(connectSocket, 1000);
                }
            };
            connectSocket();
        }
    })();
}