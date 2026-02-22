using System;
using Xunit;

namespace ProtonAstroLib.Tests
{
    public class AngleTests
    {
        [Fact]
        public void FromDegrees_Zero_IsZeroRadians()
        {
            Assert.Equal(0.0, (double)Angle.FromDegrees(0.0), 12);
        }

        [Fact]
        public void FromDegrees_180_IsPi()
        {
            Assert.Equal(Math.PI, (double)Angle.FromDegrees(180.0), 12);
        }

        [Fact]
        public void FromDegrees_90_IsHalfPi()
        {
            Assert.Equal(Math.PI / 2, (double)Angle.FromDegrees(90), 12);
        }

        [Fact]
        public void ToDegreesZero()
        {
            Assert.Equal(0.0, ((Angle)0.0).Degrees, 12);
        }

        [Fact]
        public void ToDegrees_Pi_Is180()
        {
            Assert.Equal(180, ((Angle)Math.PI).Degrees, 12);
        }

        [Fact]
        public void ToDegrees_HalfPi_Is90()
        {
            Assert.Equal(90, ((Angle)(Math.PI / 2)).Degrees, 12);
        }

        [Fact]
        public void FromTime_3Hours_Is45Degrees()
        {
            Assert.Equal(45, Angle.FromTime(TimeSpan.FromHours(3)).Degrees, 12);
        }

        [Fact]
        public void FromTime_0Hours_Is0Degrees()
        {
            Assert.Equal(0, Angle.FromTime(TimeSpan.FromHours(0)).Degrees, 12);
        }

        [Fact]
        public void FromDegrees_12_30_0_Is12Point5()
        {
            Assert.Equal(12.5, Angle.FromDegrees(12, 30, 0).Degrees, 12);
        }
    }
}
