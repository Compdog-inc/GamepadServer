if (!window.__BUTTON) {
    window.__BUTTON = true;

    window.BTN_YELLOW = 0;
    window.BTN_RED = 1;
    window.BTN_GREEN = 2;
    window.BTN_BLUE = 3;

    window.ControllerButton = function (id, label, color, evt) {
        this.render = function () {
            this.ctx.clearRect(0, 0, this.ctx.canvas.width, this.ctx.canvas.height);
            this.ctx.strokeStyle = "#000";
            this.ctx.lineWidth = 2;
            this.ctx.beginPath();
            this.ctx.ellipse(this.ctx.canvas.width / 2, this.ctx.canvas.height / 2, this.ctx.canvas.width / 2 - 2, this.ctx.canvas.height / 2 - 2, 0, 0, Math.PI * 2);

            this.ctx.fillStyle = this.isPressed ? this.gradientPressed : this.gradient;
            this.ctx.fill();
            this.ctx.stroke();

            this.ctx.fillStyle = "#fff";
            this.ctx.font = "35px Helvetica";
            const measure = this.ctx.measureText(this.label);
            const halfwidth = (measure.actualBoundingBoxRight - measure.actualBoundingBoxLeft) / 2;
            const halfheight = (measure.actualBoundingBoxDescent - measure.actualBoundingBoxAscent) / 2;
            this.ctx.fillText(this.label, this.ctx.canvas.width / 2 - halfwidth, this.ctx.canvas.height / 2 - halfheight);
        };

        const elem = document.getElementById(id);
        const canvas = document.createElement("canvas");

        canvas.width = elem.offsetWidth;
        canvas.height = elem.offsetHeight;

        const ctx = canvas.getContext('2d');
        this.ctx = ctx;
        this.label = label;
        this.evt = evt;
        this.isPressed = false;

        const down = (e) => {
            e.preventDefault();
            this.isPressed = true;
            this.evt({
                pressed: true
            });
            this.render();
        };

        const up = (e) => {
            if (this.isPressed)
                e.preventDefault();
            this.isPressed = false;
            this.evt({
                pressed: false
            });
            this.render();
        };

        canvas.addEventListener('pointerdown', down);
        canvas.addEventListener('touchstart', down);
        window.addEventListener('pointerup', up);
        window.addEventListener('touchend', up);

        elem.appendChild(canvas);

        const gradient = this.ctx.createRadialGradient(this.ctx.canvas.width / 2, this.ctx.canvas.height / 2, 0, this.ctx.canvas.width / 2, this.ctx.canvas.height / 2, this.ctx.canvas.height / 2);

        switch (color) {
            case BTN_YELLOW:
                gradient.addColorStop(0, "rgba(239,236,49,1)");
                gradient.addColorStop(0.35, "rgba(192,191,89,1)");
                gradient.addColorStop(1, "rgba(125,125,31,1)");
                break;
            case BTN_RED:
                gradient.addColorStop(0, "rgba(239,49,49,1)");
                gradient.addColorStop(0.35, "rgba(192,89,89,1)");
                gradient.addColorStop(1, "rgba(125,31,31,1)");
                break;
            case BTN_GREEN:
                gradient.addColorStop(0, "rgba(131,220,117,1)");
                gradient.addColorStop(0.35, "rgba(107,192,89,1)");
                gradient.addColorStop(1, "rgba(31,125,33,1)");
                break;
            case BTN_BLUE:
                gradient.addColorStop(0, "rgba(49,59,239,1)");
                gradient.addColorStop(0.35, "rgba(89,93,192,1)");
                gradient.addColorStop(1, "rgba(31,39,125,1)");
                break;
        }

        this.gradient = gradient;

        const gradientPressed = this.ctx.createRadialGradient(this.ctx.canvas.width / 2, this.ctx.canvas.height / 2, 0, this.ctx.canvas.width / 2, this.ctx.canvas.height / 2, this.ctx.canvas.height / 2);

        switch (color) {
            case BTN_YELLOW:
                gradientPressed.addColorStop(0, "rgba(120,119,49,1)");
                gradientPressed.addColorStop(0.35, "rgba(91,96,40,1)");
                gradientPressed.addColorStop(1, "rgba(56,57,41,1)");
                break;
            case BTN_RED:
                gradientPressed.addColorStop(0, "rgba(120,49,49,1)");
                gradientPressed.addColorStop(0.35, "rgba(96,40,40,1)");
                gradientPressed.addColorStop(1, "rgba(57,41,41,1)");
                break;
            case BTN_GREEN:
                gradientPressed.addColorStop(0, "rgba(59,120,49,1)");
                gradientPressed.addColorStop(0.35, "rgba(49,96,40,1)");
                gradientPressed.addColorStop(1, "rgba(41,57,41,1)");
                break;
            case BTN_BLUE:
                gradientPressed.addColorStop(0, "rgba(49,49,120,1)");
                gradientPressed.addColorStop(0.35, "rgba(40,43,96,1)");
                gradientPressed.addColorStop(1, "rgba(41,42,57,1)");
                break;
        }

        this.gradientPressed = gradientPressed;

        requestAnimationFrame(this.render.bind(this));
    };
}