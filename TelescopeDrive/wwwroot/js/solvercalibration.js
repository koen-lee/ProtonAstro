const fileInput    = document.getElementById("solve-file");
const btnSolve     = document.getElementById("btn-solve");
const btnApply     = document.getElementById("btn-apply");
const btnUseGps    = document.getElementById("btn-use-gps");
const preview      = document.getElementById("image-preview");
const previewWrap  = document.getElementById("image-preview-wrap");
const solveStatus  = document.getElementById("solve-status");
const solveResult  = document.getElementById("solve-result");
const gpsMismatch  = document.getElementById("gps-mismatch");
const calResult    = document.getElementById("cal-result");

let solvedRa          = null;
let solvedDec         = null;
let solvedEpoch       = null;   // DateTimeOffset ISO string from server, or null
let solvedEpochSource = null;   // "gps" | "offsetoriginal" | "datetimeoriginal" | "fallback"
let solvedGpsLat      = null;
let solvedGpsLon      = null;

// Track observer location so we can compare with photo GPS
let observerLat = null;
let observerLon = null;
connection.on("ObserverLocationSet", (lat, lon) => {
    observerLat = lat;
    observerLon = lon;
});

// ── File selected ──────────────────────────────────────────────────────────
fileInput.addEventListener("change", () => {
    const file = fileInput.files[0];
    if (!file) return;

    btnSolve.disabled = false;
    solveResult.style.display  = "none";
    gpsMismatch.style.display  = "none";
    calResult.style.display    = "none";
    solvedRa = solvedDec = solvedEpoch = solvedEpochSource = null;
    solvedGpsLat = solvedGpsLon = null;
    document.getElementById("sol-altaz-row").style.display = "none";
    setStatus("");

    if (file.type.startsWith("image/")) {
        preview.src = URL.createObjectURL(file);
        previewWrap.style.display = "block";
    } else {
        previewWrap.style.display = "none";
    }
});

// ── Plate solve ────────────────────────────────────────────────────────────
btnSolve.addEventListener("click", async () => {
    const file = fileInput.files[0];
    if (!file) return;

    btnSolve.disabled = true;
    solveResult.style.display  = "none";
    gpsMismatch.style.display  = "none";
    calResult.style.display    = "none";
    solvedRa = solvedDec = solvedEpoch = solvedEpochSource = null;
    solvedGpsLat = solvedGpsLon = null;
    setStatus("Uploading and solving \u2014 this may take up to 30 seconds\u2026");

    try {
        const form = new FormData();
        form.append("image", file);

        const resp = await fetch("?handler=Solve", { method: "POST", body: form });
        const data = await resp.json();

        if (!data.success) {
            setStatus("Error: " + data.error, true);
            return;
        }

        solvedRa          = data.ra;
        solvedDec         = data.dec;
        solvedEpoch       = data.imageEpoch ?? null;
        solvedEpochSource = data.epochSource ?? null;
        solvedGpsLat      = data.gpsLat ?? null;
        solvedGpsLon      = data.gpsLon ?? null;

        document.getElementById("sol-ra").textContent          = formatRA(data.ra);
        document.getElementById("sol-dec").textContent         = formatDec(data.dec);
        document.getElementById("sol-radius").textContent      = data.fieldRadius.toFixed(3) + "\u00b0";
        document.getElementById("sol-scale").textContent       = data.pixelScale.toFixed(2)  + "\u2033/px";
        document.getElementById("sol-orientation").textContent = data.orientation.toFixed(1)  + "\u00b0";
        document.getElementById("sol-time").textContent        = data.timeSpent.toFixed(1)    + " s";

        const epochRow = document.getElementById("sol-epoch-row");
        if (solvedEpoch) {
            document.getElementById("sol-epoch").textContent =
                solvedEpoch.replace("T", " ").replace(/\.\d+/, "");
            document.getElementById("sol-epoch-source").textContent =
                solvedEpochSource ? `(${solvedEpochSource})` : "";
            epochRow.style.display = "block";
        } else {
            epochRow.style.display = "none";
        }
        document.getElementById("sol-altaz-row").style.display = "none";

        solveResult.style.display = "block";
        setStatus("");

        checkGpsMismatch();
    } catch (err) {
        setStatus("Request failed: " + err, true);
    } finally {
        btnSolve.disabled = false;
    }
});

// ── Apply calibration ──────────────────────────────────────────────────────
btnApply.addEventListener("click", () => {
    if (solvedRa === null || solvedDec === null) return;
    connection.invoke("CalibrateRaDec", solvedRa, solvedDec, solvedEpoch)
        .catch(err => console.error(err));
});

// ── Use photo GPS as observer location ─────────────────────────────────────
btnUseGps.addEventListener("click", () => {
    if (solvedGpsLat === null || solvedGpsLon === null) return;
    connection.invoke("SetObserverLocation", solvedGpsLat, solvedGpsLon)
        .catch(err => console.error(err));
    gpsMismatch.style.display = "none";
});

connection.on("CalibrationComplete", (_label, alt, az) => {
    calResult.style.display = "block";
    document.getElementById("cal-alt").textContent = alt.toFixed(4) + "\u00b0";
    document.getElementById("cal-az").textContent  = az.toFixed(4)  + "\u00b0";

    document.getElementById("sol-az").textContent  = az.toFixed(4)  + "\u00b0";
    document.getElementById("sol-alt").textContent = alt.toFixed(4) + "\u00b0";
    document.getElementById("sol-altaz-row").style.display = "block";
});

// ── Helpers ────────────────────────────────────────────────────────────────
function checkGpsMismatch() {
    if (solvedGpsLat === null || solvedGpsLon === null) return;
    if (observerLat === null || observerLon === null) return;

    const latDiff = Math.abs(solvedGpsLat - observerLat);
    const lonDiff = Math.abs(solvedGpsLon - observerLon);
    if (latDiff <= 0.5 && lonDiff <= 0.5) return;

    document.getElementById("gps-photo-pos").textContent =
        `${solvedGpsLat.toFixed(4)}\u00b0, ${solvedGpsLon.toFixed(4)}\u00b0`;
    document.getElementById("gps-current-pos").textContent =
        `${observerLat.toFixed(4)}\u00b0, ${observerLon.toFixed(4)}\u00b0`;
    gpsMismatch.style.display = "block";
}

function setStatus(msg, isError = false) {
    solveStatus.textContent   = msg;
    solveStatus.style.display = msg ? "block" : "none";
    solveStatus.style.color   = isError ? "#e55" : "";
}

// Degrees → HH MM SS.s
function formatRA(deg) {
    const h  = deg / 15;
    const hh = Math.floor(h);
    const mm = Math.floor((h - hh) * 60);
    const ss = ((h - hh) * 60 - mm) * 60;
    return `${hh}h ${pad(mm)}m ${ss.toFixed(1).padStart(4, "0")}s`;
}

// Degrees → ±DD MM SS.s
function formatDec(deg) {
    const sign = deg < 0 ? "\u2212" : "+";
    const abs  = Math.abs(deg);
    const dd   = Math.floor(abs);
    const mm   = Math.floor((abs - dd) * 60);
    const ss   = ((abs - dd) * 60 - mm) * 60;
    return `${sign}${dd}\u00b0 ${pad(mm)}\u2032 ${ss.toFixed(1).padStart(4, "0")}\u2033`;
}

function pad(n) { return n.toString().padStart(2, "0"); }
