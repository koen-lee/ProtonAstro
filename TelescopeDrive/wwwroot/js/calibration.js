fetch(location.href, { method: "HEAD" }).then(r => {
    const serverDate = r.headers.get("Date");
    if (!serverDate) return;
    const serverTime = new Date(serverDate);
    const browserTime = new Date();
    const skewMs = Math.abs(browserTime - serverTime);
    const warn = document.getElementById("clock-skew-warning");
    warn.style.display = skewMs > 30_000 ? "block" : "none";
    document.getElementById("clock-server-time").textContent = serverTime.toUTCString();
    document.getElementById("clock-browser-time").textContent = browserTime.toUTCString();
});

document.getElementById("btn-apply-clock-offset").addEventListener("click", () => {
    connection.invoke("SetClockOffset", Date.now()).catch(err => console.error(err));
});

connection.on("ClockOffsetApplied", serverUtcMs => {
    const newServerTime = new Date(serverUtcMs);
    document.getElementById("clock-server-time").textContent = newServerTime.toUTCString();
    const skewMs = Math.abs(Date.now() - serverUtcMs);
    document.getElementById("clock-skew-warning").style.display = skewMs > 30_000 ? "block" : "none";
});

document.getElementById("btn-calibrate").addEventListener("click", () => {
    const star = document.getElementById("cal-star").value;
    if (!star) return;
    connection.invoke("Calibrate", star).catch(err => console.error(err));
});

connection.on("CalibrationComplete", (starName, alt, az) => {
    const result = document.getElementById("cal-result");
    result.style.display = "block";
    document.getElementById("cal-star-name").textContent = starName;
    document.getElementById("cal-alt").textContent = alt.toFixed(4) + "\u00B0";
    document.getElementById("cal-az").textContent = az.toFixed(4) + "\u00B0";

    const warn = document.getElementById("cal-zenith-warning");
    warn.style.display = alt > 80 ? "block" : "none";
});
