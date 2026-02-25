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

connection.on("PositionUpdate", (alt, az, targetName, isTracking) => {
    document.getElementById("pos-alt").textContent = alt.toFixed(4) + "\u00B0";
    document.getElementById("pos-az").textContent = az.toFixed(4) + "\u00B0";
    document.getElementById("pos-target").textContent = targetName || "--";
});

connection.on("TrackingStatus", (isTracking) => {
    btnTrack.style.display = isTracking ? "none" : "";
    btnStopTrack.style.display = isTracking ? "" : "none";
    trackingDot.className = "tracking-indicator" + (isTracking ? " active" : "");
    trackingLabel.textContent = isTracking ? "Tracking active" : "Not tracking";
});
