const log = document.getElementById("gcode-log");
const input = document.getElementById("gcode-input");
const autoScroll = document.getElementById("auto-scroll");

function appendLog(cls, text) {
    const line = document.createElement("div");
    line.className = cls;
    line.textContent = text;
    log.appendChild(line);
    if (autoScroll.checked) {
        log.scrollTop = log.scrollHeight;
    }
}

connection.on("GCodeSent", (ts, cmd) => appendLog("sent", `[${ts}] >> ${cmd}`));
connection.on("GCodeReceived", (ts, resp) => appendLog("received", `[${ts}] << ${resp}`));

document.getElementById("btn-clear").addEventListener("click", () => {
    log.innerHTML = "";
});

document.getElementById("btn-send").addEventListener("click", sendGCode);
input.addEventListener("keydown", e => { if (e.key === "Enter") sendGCode(); });

function sendGCode() {
    const cmd = input.value.trim();
    if (!cmd) return;
    connection.invoke("SendGCode", cmd).catch(err => appendLog("error", "Error: " + err));
    input.value = "";
}
