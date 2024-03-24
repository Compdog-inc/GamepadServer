if (!window.__GYRO) {
    window.__GYRO = () => { };
    (async function () {
        'use strict';

        const quaternion_matrix = (quat) => {
            const x = quat[0];
            const y = quat[1];
            const z = quat[2];
            const w = quat[3];

            return [
                - 2 * y * y - 2 * z * z, 2 * x * y - 2 * z * w, 2 * x * z + 2 * y * w, 0,
                2 * x * y + 2 * z * w, 1 - 2 * x * x - 2 * z * z, 2 * y * z - 2 * x * w, 0,
                2 * x * z - 2 * y * w, 2 * y * z + 2 * x * w, 1 - 2 * x * x - 2 * y * y, 0,
                0, 0, 0, 1
            ];
        };

        const rotation_angles = (mat) => {
            const R31 = mat[8];
            const R32 = mat[9];
            const R33 = mat[10];
            const R21 = mat[4];
            const R11 = mat[0];
            const R12 = mat[1];
            const R13 = mat[2];

            if (R31 != -1 && R31 != 1) {
                const z_1 = -Math.asin(R31);
                const z_2 = Math.PI - z_1;
                const cos1 = Math.cos(z_1);
                const cos2 = Math.cos(z_2);
                const y_1 = Math.atan2(R32 / cos1, R33 / cos1);
                const y_2 = Math.atan2(R32 / cos2, R33 / cos2);
                const x_1 = Math.atan2(R21 / cos1, R11 / cos1);
                const x_2 = Math.atan2(R21 / cos2, R11 / cos2);

                return [x_1 * 180 / Math.PI, y_1 * 180 / Math.PI, z_1 * 180 / Math.PI];
            }
            else {
                const x = 0;
                if (R31 == -1) {
                    const z = Math.PI / 2;
                    const y = x + Math.atan2(R12, R13);
                    return [x * 180 / Math.PI, y * 180 / Math.PI, z * 180 / Math.PI];
                } else {
                    const z = -Math.PI / 2;
                    const y = -x + Math.atan2(-R12, -R13);
                    return [x * 180 / Math.PI, y * 180 / Math.PI, z * 180 / Math.PI];
                }
            }
        }

        const options = { frequency: 60, referenceFrame: "device" };
        const sensor = new RelativeOrientationSensor(options);

        await Promise.all([
            navigator.permissions.query({ name: "accelerometer" }),
            navigator.permissions.query({ name: "gyroscope" }),
        ]).then((results) => {
            if (results.every((result) => result.state === "granted")) {
                sensor.start();
                sensor.addEventListener("reading", () => {
                    const rot = rotation_angles(quaternion_matrix(sensor.quaternion));
                    window.Controls.receiveGyro(rot);
                });
                sensor.addEventListener("error", (e) => {
                    if (e.error.name === "NotReadableError") {
                        console.log("Sensor is not available.");
                    }
                });
            } else {
                console.log("No permissions to use RelativeOrientationSensor.");
            }
        });
    })();
}