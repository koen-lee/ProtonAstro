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

        [Theory]
        [InlineData("12d30m00s", 12.5)]
        [InlineData("90d00m00s", 90.0)]
        [InlineData("38d47m03s", 38.784166666666664)]
        [InlineData("12°30'00\"", 12.5)]
        [InlineData("180d", 180.0)]
        [InlineData("45d30m", 45.5)]
        [InlineData("12.5", 12.5)]
        [InlineData("-08d12m06s", -8.201666666666666)]
        [InlineData("-16d42m58s", -16.716111111111111)]
        public void FromDegrees_String_ParsesCorrectly(string input, double expectedDegrees)
        {
            Assert.Equal(expectedDegrees, Angle.FromDegrees(input).Degrees, 6);
        }

        [Theory]
        [InlineData("not an angle")]
        [InlineData("")]
        public void FromDegrees_String_InvalidInput_Throws(string input)
        {
            Assert.Throws<FormatException>(() => Angle.FromDegrees(input));
        }

        [Theory]
        [InlineData("18h36m56s", 18, 36, 56)]
        [InlineData("0h0m0s", 0, 0, 0)]
        [InlineData("6h45m", 6, 45, 0)]
        [InlineData("12h", 12, 0, 0)]
        public void FromHMS_ParsesCorrectly(string input, int h, int m, int s)
        {
            var expected = Angle.FromTime(new TimeSpan(h, m, s));
            Assert.Equal((double)expected, (double)Angle.FromHMS(input), 12);
        }

        [Theory]
        [InlineData("18:36:56")]
        public void FromHMS_TimeSpanFormat_ParsesCorrectly(string input)
        {
            var expected = Angle.FromTime(new TimeSpan(18, 36, 56));
            Assert.Equal((double)expected, (double)Angle.FromHMS(input), 12);
        }

        [Theory]
        [InlineData("not a time")]
        [InlineData("")]
        public void FromHMS_InvalidInput_Throws(string input)
        {
            Assert.Throws<FormatException>(() => Angle.FromHMS(input));
        }
    }
}
