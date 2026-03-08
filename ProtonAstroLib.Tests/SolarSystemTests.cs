using System;
using Xunit;

namespace ProtonAstroLib.Tests
{
    public class SolarSystemTests
    {
        // ── Sun (moved from Catalog) ──────────────────────────────────────────

        [Fact]
        public void Sun_OnSummerSolstice_DeclinationIsPlus23()
        {
            var solstice = new DateTimeOffset(2024, 6, 20, 20, 51, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(solstice);

            Assert.Equal(23.44, sun.Declination.Degrees, 0.5);
        }

        [Fact]
        public void Sun_OnWinterSolstice_DeclinationIsMinus23()
        {
            var solstice = new DateTimeOffset(2024, 12, 21, 9, 20, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(solstice);

            Assert.Equal(-23.44, sun.Declination.Degrees, 0.5);
        }

        [Fact]
        public void Sun_OnVernalEquinox_DeclinationIsZero()
        {
            var equinox = new DateTimeOffset(2024, 3, 20, 3, 6, 0, TimeSpan.Zero);
            var sun = SolarSystem.Sun(equinox);

            Assert.Equal(0, sun.Declination.Degrees, 0.5);
        }

        // ── Planets: epoch must be moment (skips precession) ─────────────────

        [Theory]
        [InlineData("Mercury")]
        [InlineData("Venus")]
        [InlineData("Mars")]
        [InlineData("Jupiter")]
        [InlineData("Saturn")]
        [InlineData("Uranus")]
        public void Planet_EpochIsSetToMoment(string name)
        {
            var moment = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
            SolarSystem.TryGetBody(name, out var func);

            var coord = func!(moment);

            Assert.Equal(moment, coord.Epoch);
        }

        // ── Planets: declination stays within the ecliptic band ──────────────

        [Theory]
        [InlineData("Mercury")]
        [InlineData("Venus")]
        [InlineData("Mars")]
        [InlineData("Jupiter")]
        [InlineData("Saturn")]
        [InlineData("Uranus")]
        public void Planet_DeclinationWithinEclipticBand(string name)
        {
            var moment = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
            SolarSystem.TryGetBody(name, out var func);

            var coord = func!(moment);

            Assert.InRange(coord.Declination.Degrees, -35.0, 35.0);
        }

        // ── Spot-checks against known ephemeris positions (~2° tolerance) ────

        [Fact]
        public void Mars_AtOpposition2022_IsNearTaurus()
        {
            // Mars opposition Dec 8 2022. Sun at RA ≈ 252°, so Mars opposite at ≈ 72°, Dec ≈ +25°.
            var opposition = new DateTimeOffset(2022, 12, 8, 5, 42, 0, TimeSpan.Zero);
            var mars = SolarSystem.Mars(opposition);

            Assert.Equal(72.0, mars.RightAscension.Degrees, 6.0);
            Assert.Equal(25.0, mars.Declination.Degrees, 4.0);
        }

        [Fact]
        public void Jupiter_AtOpposition2022_IsNearPisces()
        {
            // Jupiter opposition Sep 26 2022. Sun just past autumnal equinox at RA ≈ 183°,
            // so Jupiter is opposite at ≈ 3°. Dec ≈ -1° (near celestial equator).
            var opposition = new DateTimeOffset(2022, 9, 26, 21, 25, 0, TimeSpan.Zero);
            var jupiter = SolarSystem.Jupiter(opposition);

            Assert.Equal(3.0, jupiter.RightAscension.Degrees, 6.0);
            Assert.Equal(-1.0, jupiter.Declination.Degrees, 4.0);
        }

        [Fact]
        public void Saturn_AtOpposition2022_IsNearCapricornus()
        {
            // Saturn opposition Aug 14 2022. Sun at RA ≈ 144°, Saturn opposite at ≈ 324°, Dec ≈ -16°.
            var opposition = new DateTimeOffset(2022, 8, 14, 19, 0, 0, TimeSpan.Zero);
            var saturn = SolarSystem.Saturn(opposition);

            Assert.Equal(324.0, saturn.RightAscension.Degrees, 6.0);
            Assert.Equal(-16.0, saturn.Declination.Degrees, 4.0);
        }

        // ── TryGetBody ────────────────────────────────────────────────────────

        [Fact]
        public void TryGetBody_UnknownName_ReturnsFalse()
        {
            Assert.False(SolarSystem.TryGetBody("Neptune", out _));
        }

        [Fact]
        public void TryGetBody_Sun_ReturnsTrue()
        {
            Assert.True(SolarSystem.TryGetBody("Sun", out var func));
            Assert.NotNull(func);
        }
    }
}
