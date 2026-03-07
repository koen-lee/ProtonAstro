const catalogSelect = document.getElementById("catalog-select");
const sunWarning = document.getElementById("sun-warning");
const btnGoto = document.getElementById("btn-goto");
const btnGotoCustom = document.getElementById("btn-goto-custom");
const btnTrack = document.getElementById("btn-track");
const btnStopTrack = document.getElementById("btn-stop-track");
const trackingDot = document.getElementById("tracking-dot");
const trackingLabel = document.getElementById("tracking-label");

catalogSelect.addEventListener("change", () => {
    sunWarning.style.display = catalogSelect.value === "Sun" ? "block" : "none";
});

btnGoto.addEventListener("click", () => {
    const target = catalogSelect.value;
    if (!target) return;
    connection.invoke("Goto", target).catch(err => console.error(err));
});

btnGotoCustom.addEventListener("click", () => {
    const ra = document.getElementById("custom-ra").value.trim();
    const dec = document.getElementById("custom-dec").value.trim();
    if (!ra || !dec) return;
    connection.invoke("GotoCustom", ra, dec).catch(err => console.error(err));
});

btnTrack.addEventListener("click", () => {
    connection.invoke("StartTracking").catch(err => console.error(err));
});

btnStopTrack.addEventListener("click", () => {
    connection.invoke("StopTracking").catch(err => console.error(err));
});

connection.on("PositionUpdate", (alt, az, targetName, isTracking, raDeg, decDeg) => {
    document.getElementById("pos-alt").textContent = alt.toFixed(4) + "\u00B0";
    document.getElementById("pos-az").textContent = az.toFixed(4) + "\u00B0";
    document.getElementById("pos-target").textContent = targetName || "--";
    document.getElementById("pos-ra").textContent = raDeg != null ? degreesToHMS(raDeg) : "--";
    document.getElementById("pos-dec").textContent = decDeg != null ? degreesToDMS(decDeg) : "--";
});

function degreesToHMS(deg) {
    const h = deg / 15;
    const hh = Math.floor(h);
    const mm = Math.floor((h - hh) * 60);
    const ss = ((h - hh) * 60 - mm) * 60;
    return `${String(hh).padStart(2, '0')}h${String(mm).padStart(2, '0')}m${ss.toFixed(1).padStart(4, '0')}s`;
}

function degreesToDMS(deg) {
    const sign = deg < 0 ? "-" : "+";
    const abs = Math.abs(deg);
    const dd = Math.floor(abs);
    const mm = Math.floor((abs - dd) * 60);
    const ss = ((abs - dd) * 60 - mm) * 60;
    return `${sign}${String(dd).padStart(2, '0')}\u00B0${String(mm).padStart(2, '0')}'${ss.toFixed(0).padStart(2, '0')}"`;
}

connection.on("TrackingStatus", (isTracking) => {
    btnTrack.style.display = isTracking ? "none" : "";
    btnStopTrack.style.display = isTracking ? "" : "none";
    trackingDot.className = "tracking-indicator" + (isTracking ? " active" : "");
    trackingLabel.textContent = isTracking ? "Tracking active" : "Not tracking";
});
