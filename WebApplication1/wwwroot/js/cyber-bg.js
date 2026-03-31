// === CrimeCode Cyber Background ===
(function () {
    const canvas = document.getElementById('cyberBg');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');

    let W, H;
    function resize() {
        W = canvas.width = window.innerWidth;
        H = canvas.height = window.innerHeight;
    }
    resize();
    window.addEventListener('resize', resize);

    // --- Matrix rain columns ---
    const FONT_SIZE = 14;
    const chars = 'アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン0123456789ABCDEF<>/{}[];:'.split('');
    let columns = [];

    function initColumns() {
        const count = Math.floor(W / FONT_SIZE);
        columns = [];
        for (let i = 0; i < count; i++) {
            columns.push({
                x: i * FONT_SIZE,
                y: Math.random() * H,
                speed: 0.3 + Math.random() * 0.8,
                chars: [],
                len: 8 + Math.floor(Math.random() * 16),
                opacity: 0.03 + Math.random() * 0.06
            });
        }
    }
    initColumns();
    window.addEventListener('resize', initColumns);

    // --- Network nodes ---
    const NODE_COUNT = 40;
    const nodes = [];
    for (let i = 0; i < NODE_COUNT; i++) {
        nodes.push({
            x: Math.random() * 2000,
            y: Math.random() * 2000,
            vx: (Math.random() - 0.5) * 0.3,
            vy: (Math.random() - 0.5) * 0.3,
            r: 1 + Math.random() * 2,
            pulse: Math.random() * Math.PI * 2
        });
    }

    // --- Hex grid dots ---
    function drawHexGrid() {
        const spacing = 80;
        const dotR = 0.6;
        ctx.fillStyle = 'rgba(0, 229, 160, 0.04)';
        for (let y = 0; y < H + spacing; y += spacing) {
            const offset = (Math.floor(y / spacing) % 2) * (spacing / 2);
            for (let x = 0; x < W + spacing; x += spacing) {
                ctx.beginPath();
                ctx.arc(x + offset, y, dotR, 0, Math.PI * 2);
                ctx.fill();
            }
        }
    }

    // --- Data stream (horizontal glitch lines) ---
    function drawDataStreams(time) {
        const streamCount = 3;
        for (let i = 0; i < streamCount; i++) {
            const y = (time * (0.5 + i * 0.3) * 50) % (H + 200) - 100;
            const w = 100 + Math.random() * 300;
            const x = Math.random() * W;
            ctx.fillStyle = `rgba(0, 229, 160, ${0.02 + Math.random() * 0.03})`;
            ctx.fillRect(x, y, w, 1);
        }
    }

    // --- Main render ---
    let t = 0;

    function render() {
        t += 0.016;

        // Fade previous frame
        ctx.fillStyle = 'rgba(5, 8, 14, 0.12)';
        ctx.fillRect(0, 0, W, H);

        // Hex grid (static)
        if (Math.floor(t * 60) % 120 === 0) {
            drawHexGrid();
        }

        // Matrix rain
        ctx.font = `${FONT_SIZE}px 'JetBrains Mono', monospace`;
        for (const col of columns) {
            col.y += col.speed;
            if (col.y > H + col.len * FONT_SIZE) {
                col.y = -col.len * FONT_SIZE;
                col.speed = 0.3 + Math.random() * 0.8;
                col.opacity = 0.03 + Math.random() * 0.06;
            }

            for (let j = 0; j < col.len; j++) {
                const cy = col.y - j * FONT_SIZE;
                if (cy < -FONT_SIZE || cy > H + FONT_SIZE) continue;

                const fade = 1 - j / col.len;
                const alpha = col.opacity * fade;

                if (j === 0) {
                    ctx.fillStyle = `rgba(0, 229, 160, ${Math.min(alpha * 4, 0.4)})`;
                } else {
                    ctx.fillStyle = `rgba(0, 229, 160, ${alpha})`;
                }

                const ch = chars[Math.floor(Math.random() * chars.length)];
                ctx.fillText(ch, col.x, cy);
            }
        }

        // Network nodes
        for (const n of nodes) {
            n.x += n.vx;
            n.y += n.vy;
            n.pulse += 0.02;
            if (n.x < 0 || n.x > W) n.vx *= -1;
            if (n.y < 0 || n.y > H) n.vy *= -1;
            n.x = Math.max(0, Math.min(W, n.x));
            n.y = Math.max(0, Math.min(H, n.y));
        }

        // Draw connections
        for (let i = 0; i < nodes.length; i++) {
            for (let j = i + 1; j < nodes.length; j++) {
                const dx = nodes[i].x - nodes[j].x;
                const dy = nodes[i].y - nodes[j].y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                if (dist < 200) {
                    const alpha = (1 - dist / 200) * 0.06;
                    ctx.strokeStyle = `rgba(0, 212, 255, ${alpha})`;
                    ctx.lineWidth = 0.5;
                    ctx.beginPath();
                    ctx.moveTo(nodes[i].x, nodes[i].y);
                    ctx.lineTo(nodes[j].x, nodes[j].y);
                    ctx.stroke();
                }
            }
        }

        // Draw node dots
        for (const n of nodes) {
            const glow = 0.5 + Math.sin(n.pulse) * 0.5;
            const r = n.r * (0.8 + glow * 0.4);

            // Outer glow
            ctx.beginPath();
            ctx.arc(n.x, n.y, r * 3, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(0, 212, 255, ${0.02 * glow})`;
            ctx.fill();

            // Core
            ctx.beginPath();
            ctx.arc(n.x, n.y, r, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(0, 212, 255, ${0.15 + glow * 0.1})`;
            ctx.fill();
        }

        // Data streams
        drawDataStreams(t);

        // Occasional red pulse
        if (Math.random() < 0.003) {
            const px = Math.random() * W;
            const py = Math.random() * H;
            const grad = ctx.createRadialGradient(px, py, 0, px, py, 80);
            grad.addColorStop(0, 'rgba(255, 34, 68, 0.08)');
            grad.addColorStop(1, 'transparent');
            ctx.fillStyle = grad;
            ctx.fillRect(px - 80, py - 80, 160, 160);
        }

        requestAnimationFrame(render);
    }

    // Initial clear
    ctx.fillStyle = '#05080e';
    ctx.fillRect(0, 0, W, H);
    drawHexGrid();
    render();
})();
