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
});
