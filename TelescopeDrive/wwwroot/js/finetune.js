let stepSize = 0.1;

document.querySelectorAll(".step-btn").forEach(btn => {
    btn.addEventListener("click", () => {
        document.querySelectorAll(".step-btn").forEach(b => b.classList.remove("active"));
        btn.classList.add("active");
        stepSize = parseFloat(btn.dataset.step);
    });
});

document.querySelectorAll(".dpad button").forEach(btn => {
    btn.addEventListener("click", () => {
        const dir = btn.dataset.dir;
        connection.invoke("Jog", dir, stepSize).catch(err => console.error(err));
    });
});

document.getElementById("adoptBtn").addEventListener("click", () => {
    connection.invoke("AdoptPosition")
        .then(() => {
            document.getElementById("adoptStatus").textContent = "Position adopted";
            setTimeout(() => document.getElementById("adoptStatus").textContent = "", 3000);
        })
        .catch(err => console.error(err));
});

document.addEventListener("keydown", e => {
    const stepKeys = { "1": 0, "2": 1, "3": 2, "4": 3, "5": 4 };
    if (e.key in stepKeys) {
        const btns = document.querySelectorAll(".step-btn");
        const idx = stepKeys[e.key];
        if (btns[idx]) btns[idx].click();
        return;
    }

    if (e.key === "Enter") {
        document.getElementById("adoptBtn").click();
        return;
    }

    const map = { ArrowUp: "up", ArrowDown: "down", ArrowLeft: "left", ArrowRight: "right" };
    const dir = map[e.key];
    if (dir && !e.repeat) {
        e.preventDefault();
        connection.invoke("Jog", dir, stepSize).catch(err => console.error(err));
    }
});
