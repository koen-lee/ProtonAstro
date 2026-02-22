using System;
using Xunit;

namespace ProtonAstroLib.Tests
{

    public class EquationOfTimeTests
    {
        [Fact]
        public void EquationOfTime_NeverExceeds20Minutes()
        {
            var date = Constants.J2000Epoch;
            for (int i = 0; i < 365; i++)
            {
                var eot = date.EquationOfTime().TotalMinutes;
                Assert.True(Math.Abs(eot) < 20,
                    $"Equation of time {eot:F2} min exceeded 20 min on day {i}");
                date = date.AddDays(1);
            }
        }

        [Fact]
        public void EquationOfTime_CancelsOutOverYear()
        {
            var date = Constants.J2000Epoch + TimeSpan.FromDays(-40);
            var sum = 0.0;
            var steps = (int)(365.25 * 4); // 6-hour intervals over one orbital period
            for (int i = 0; i < steps; i++)
            {
                sum += date.EquationOfTime().TotalMinutes;
                date = date.AddHours(6);
            }

            Assert.True(Math.Abs(sum) < 1,
                $"Equation of time sum over year was {sum:F2}, expected near zero");
        }
    }
}
