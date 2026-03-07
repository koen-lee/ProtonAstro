namespace TelescopeDrive.Models;

/// <summary>
/// One calibration datum: where the telescope was commanded to point (expected)
/// versus what a plate solve revealed it was actually pointing at (actual).
/// Both positions are horizontal coordinates at the moment of the solve.
///
/// Sign convention:
///   DeltaAltDeg = actual_alt - expected_alt  (positive → scope points higher than commanded)
///   DeltaAzDeg  = actual_az  - expected_az   (normalised to [-180, 180])
///
/// Correction to apply when commanding the motor:
///   commandedAlt = desiredSkyAlt - DeltaAltDeg
/// </summary>
public record AlignmentPoint(
    double ExpectedAltDeg,
    double ExpectedAzDeg,
    double DeltaAltDeg,
    double DeltaAzDeg,
    DateTimeOffset CapturedAt);
