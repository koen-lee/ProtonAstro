// Shared SignalR connection for all pages
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/telescope")
    .withAutomaticReconnect()
    .build();

// Connection status indicator (in nav)
function updateConnectionIndicator(driveConnected, portName, isSimulated) {
    const el = document.getElementById("connection-status");
    if (!el) return;
    if (isSimulated) {
        el.textContent = "Simulated";
        el.className = "status simulated";
    } else if (driveConnected) {
        el.textContent = "Connected: " + portName;
        el.className = "status connected";
    } else {
        el.textContent = "No Drive";
        el.className = "status disconnected";
    }
}

connection.on("ConnectionStatus", updateConnectionIndicator);

connection.onreconnecting(() => {
    const el = document.getElementById("connection-status");
    if (el) { el.textContent = "Reconnecting…"; el.className = "status disconnected"; }
});

connection.onclose(() => {
    const el = document.getElementById("connection-status");
    if (el) { el.textContent = "No Backend"; el.className = "status disconnected"; }
});

// Geolocation: send observer location on connect
function sendGeolocation() {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
        pos => {
            connection.invoke("SetObserverLocation", pos.coords.latitude, pos.coords.longitude)
                .catch(err => console.warn("Failed to set geolocation:", err));
        },
        err => console.warn("Geolocation unavailable:", err.message)
    );
}

// Start connection
connection.start()
    .then(() => {
        console.log("SignalR connected");
        sendGeolocation();
    })
    .catch(err => console.error("SignalR connection error:", err));

connection.onreconnected(() => sendGeolocation());
