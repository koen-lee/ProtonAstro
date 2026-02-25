// Shared SignalR connection for all pages
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/telescope")
    .withAutomaticReconnect()
    .build();

// Connection status indicator (in nav)
function updateConnectionIndicator(connected, portName) {
    const el = document.getElementById("connection-status");
    if (!el) return;
    if (connected) {
        el.textContent = "Connected: " + portName;
        el.className = "status connected";
    } else {
        el.textContent = "Disconnected";
        el.className = "status disconnected";
    }
}

connection.on("ConnectionStatus", updateConnectionIndicator);

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
