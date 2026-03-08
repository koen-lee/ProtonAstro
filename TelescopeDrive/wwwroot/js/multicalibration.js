// ── State ──────────────────────────────────────────────────────────────────
let surveyGrid        = [];
let currentPointIndex = -1;
let pendingSolveAlt   = null;
let pendingSolveAz    = null;
let solvedRa          = null;
let solvedDec         = null;
let solvedEpoch       = null;
let recordedCount     = 0;

// ── DOM refs ───────────────────────────────────────────────────────────────
const btnStartSurvey  = document.getElementById("btn-start-survey");
const btnClearModel   = document.getElementById("btn-clear-model");
const btnSolve        = document.getElementById("btn-solve");
const btnSkip         = document.getElementById("btn-skip");
const btnApplyPoint   = document.getElementById("btn-apply-point");
const btnFinish       = document.getElementById("btn-finish-survey");
const fileInput       = document.getElementById("solve-file");
const surveyGridCard  = document.getElementById("survey-grid-card");
const surveyTbody     = document.getElementById("survey-tbody");
const solveCard       = document.getElementById("solve-card");
const solveStatus     = document.getElementById("solve-status");
const solveResult     = document.getElementById("solve-result");
const modelSummary    = document.getElementById("model-summary");
const surveyStatusEl  = document.getElementById("survey-status");

// ── Restore persisted model on connect ────────────────────────────────────
connection.on("AlignmentModelStatus", count => {
    if (count > 0) {
        recordedCount = count;
        updateModelSummary(count);
        setStatus(`Alignment model loaded: ${count} point(s) from previous session.`);
    }
});

// ── Survey start ───────────────────────────────────────────────────────────
btnStartSurvey.addEventListener("click", () => {
    setStatus("Loading survey grid...");
    connection.invoke("GetSurveyGrid").catch(err => setStatus("Error: " + err, true));
});

connection.on("SurveyGrid", grid => {
    surveyGrid        = grid;
    currentPointIndex = -1;
    renderSurveyTable();
    surveyGridCard.style.display = "block";
    setStatus(`${grid.length} survey points loaded. Click Slew on any row to begin.`);
    advanceToNextPoint();
});

function renderSurveyTable() {
    surveyTbody.innerHTML = "";
    surveyGrid.forEach((pt, i) => {
        const tr = document.createElement("tr");
        tr.id = `row-${i}`;
        tr.innerHTML =
            `<td>${i + 1}</td>` +
            `<td>${pt.altDeg.toFixed(1)}</td>` +
            `<td>${pt.azDeg.toFixed(1)}</td>` +
            `<td id="status-${i}">Pending</td>` +
            `<td id="dalt-${i}">—</td>` +
            `<td id="daz-${i}">—</td>` +
            `<td><button onclick="slewToPoint(${i})">Slew</button></td>`;
        surveyTbody.appendChild(tr);
    });
}

function advanceToNextPoint() {
    // Find next pending point
    const next = surveyGrid.findIndex(
        (_, i) => i > currentPointIndex &&
            document.getElementById(`status-${i}`)?.textContent === "Pending");
    if (next === -1) {
        setStatus("All points visited.");
        solveCard.style.display = "none";
        return;
    }
    slewToPoint(next);
}

function slewToPoint(index) {
    currentPointIndex = index;
    const pt = surveyGrid[index];
    pendingSolveAlt   = pt.altDeg;
    pendingSolveAz    = pt.azDeg;

    setRowStatus(index, "Slewing...");
    setStatus(`Slewing to point ${index + 1}: Alt ${pt.altDeg.toFixed(1)}°, Az ${pt.azDeg.toFixed(1)}°`);

    solveCard.style.display = "block";
    solveResult.style.display = "none";
    setSolveStatus("");
    fileInput.value = "";
    btnSolve.disabled = true;
    solvedRa = solvedDec = solvedEpoch = null;

    document.getElementById("solve-point-index").textContent = index + 1;
    document.getElementById("solve-alt").textContent = pt.altDeg.toFixed(1);
    document.getElementById("solve-az").textContent  = pt.azDeg.toFixed(1);

    connection.invoke("GotoSurveyPoint", pt.altDeg, pt.azDeg)
        .catch(err => setStatus("Slew error: " + err, true));
}

connection.on("SurveyPointReached", (alt, az) => {
    setRowStatus(currentPointIndex, "At position");
    setStatus(`At Alt ${alt.toFixed(1)}°, Az ${az.toFixed(1)}°. Upload a photo to solve.`);
});

// ── File input ─────────────────────────────────────────────────────────────
fileInput.addEventListener("change", () => {
    btnSolve.disabled = !fileInput.files.length;
    solveResult.style.display = "none";
    solvedRa = solvedDec = solvedEpoch = null;
});

// ── Plate solve ────────────────────────────────────────────────────────────
btnSolve.addEventListener("click", async () => {
    const file = fileInput.files[0];
    if (!file) return;

    btnSolve.disabled = true;
    setSolveStatus("Solving — may take up to 30 s...");

    try {
        const form = new FormData();
        form.append("image", file);
        if (pendingSolveAlt !== null) form.append("hintAltDeg", pendingSolveAlt);
        if (pendingSolveAz  !== null) form.append("hintAzDeg",  pendingSolveAz);

        const resp = await fetch("/Calibration?handler=Solve",
            { method: "POST", body: form });
        const data = await resp.json();

        if (!data.success) {
            setSolveStatus("Solve failed: " + (data.error ?? "unknown error"), true);
            return;
        }

        solvedRa    = data.ra;
        solvedDec   = data.dec;
        solvedEpoch = data.imageEpoch ?? null;

        document.getElementById("sol-ra").textContent = formatRA(data.ra);
        document.getElementById("sol-dec").textContent = formatDec(data.dec);
        document.getElementById("sol-delta-alt").textContent = "—";
        document.getElementById("sol-delta-az").textContent  = "—";
        solveResult.style.display = "block";
        setSolveStatus("");
    } catch (err) {
        setSolveStatus("Request failed: " + err, true);
    } finally {
        btnSolve.disabled = false;
    }
});

// ── Record point ───────────────────────────────────────────────────────────
btnApplyPoint.addEventListener("click", () => {
    if (solvedRa === null || pendingSolveAlt === null) return;
    connection.invoke(
        "AddAlignmentPoint",
        solvedRa, solvedDec,
        pendingSolveAlt, pendingSolveAz,
        solvedEpoch
    ).catch(err => setStatus("Error recording point: " + err, true));
});

connection.on("AlignmentPointAdded", (expAlt, expAz, dAlt, dAz, totalPoints) => {
    recordedCount = totalPoints;

    setRowStatus(currentPointIndex, "Done");
    document.getElementById(`dalt-${currentPointIndex}`).textContent = dAlt.toFixed(3);
    document.getElementById(`daz-${currentPointIndex}`).textContent  = dAz.toFixed(3);

    document.getElementById("sol-delta-alt").textContent = dAlt.toFixed(3);
    document.getElementById("sol-delta-az").textContent  = dAz.toFixed(3);

    updateModelSummary(totalPoints);
    setStatus(`Point ${currentPointIndex + 1} recorded — ΔAlt ${dAlt.toFixed(3)}°, ΔAz ${dAz.toFixed(3)}°.`);
    advanceToNextPoint();
});

// ── Skip point ─────────────────────────────────────────────────────────────
btnSkip.addEventListener("click", () => {
    if (currentPointIndex < 0) return;
    setRowStatus(currentPointIndex, "Skipped");
    setStatus(`Point ${currentPointIndex + 1} skipped.`);
    advanceToNextPoint();
});

// ── Finish survey ──────────────────────────────────────────────────────────
btnFinish.addEventListener("click", () => {
    solveCard.style.display = "none";
    setStatus("Survey complete. The alignment model is active.");
});

// ── Clear model ────────────────────────────────────────────────────────────
btnClearModel.addEventListener("click", () => {
    connection.invoke("ClearAlignmentModel")
        .catch(err => setStatus("Error: " + err, true));
});

connection.on("AlignmentModelCleared", () => {
    recordedCount = 0;
    updateModelSummary(0);
    surveyGrid.forEach((_, i) => {
        const status = document.getElementById(`status-${i}`);
        if (status) status.textContent = "Pending";
        const dalt = document.getElementById(`dalt-${i}`);
        if (dalt) dalt.textContent = "—";
        const daz = document.getElementById(`daz-${i}`);
        if (daz) daz.textContent = "—";
    });
    setStatus("Alignment model cleared.");
});

// ── Helpers ────────────────────────────────────────────────────────────────
function updateModelSummary(count) {
    modelSummary.style.display = "block";
    document.getElementById("model-point-count").textContent = count;
    const active = document.getElementById("model-active-label");
    active.style.display = count >= 3 ? "inline" : "none";
    btnFinish.style.display = count >= 3 ? "" : "none";
}

function setRowStatus(index, text) {
    const el = document.getElementById(`status-${index}`);
    if (el) el.textContent = text;
}

function setStatus(msg, isError = false) {
    surveyStatusEl.textContent   = msg;
    surveyStatusEl.style.display = msg ? "block" : "none";
    surveyStatusEl.style.color   = isError ? "var(--danger, #e55)" : "";
}

function setSolveStatus(msg, isError = false) {
    solveStatus.textContent   = msg;
    solveStatus.style.display = msg ? "block" : "none";
    solveStatus.style.color   = isError ? "var(--danger, #e55)" : "";
}

function formatRA(deg) {
    const h  = deg / 15;
    const hh = Math.floor(h);
    const mm = Math.floor((h - hh) * 60);
    const ss = ((h - hh) * 60 - mm) * 60;
    return `${hh}h ${pad(mm)}m ${ss.toFixed(1).padStart(4, "0")}s`;
}

function formatDec(deg) {
    const sign = deg < 0 ? "\u2212" : "+";
    const abs  = Math.abs(deg);
    const dd   = Math.floor(abs);
    const mm   = Math.floor((abs - dd) * 60);
    const ss   = ((abs - dd) * 60 - mm) * 60;
    return `${sign}${dd}\u00b0 ${pad(mm)}' ${ss.toFixed(1).padStart(4, "0")}"`;
}

function pad(n) { return n.toString().padStart(2, "0"); }
