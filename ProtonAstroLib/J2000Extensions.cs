using System;

namespace ProtonAstroLib
{
    /// <summary>
    /// DateTimeOffset extension methods using the J2000 epoch
    /// </summary>
    public static class J2000Extensions
    {

        /// <summary>
        /// Calculates the mean siderial time of moment, using the J2000 epoch
        /// </summary>
        /// <param name="moment"></param>
        /// <returns></returns>
        public static Angle GreenwichMeanSiderialTime(this DateTimeOffset moment)
        {
            // From http://en.wikipedia.org/wiki/Sidereal_time#Definition
            // Greenwich Mean Sidereal Time (GMST) and UT1 differ from each other in rate, with the second of sidereal time a little shorter than that of UT1, so that (as at 2000 January 1 noon) 1.002737909350795 second of mean sidereal time was equal to 1 second of Universal Time (UT1). The ratio varies slightly with time, reaching 1.002737909409795 after a century.[3]
            // To an accuracy within 0.1 second per century, Greenwich (Mean) Sidereal Time (in hours and decimal parts of an hour) can be calculated as
            //    GMST = 18.697374558 + 24.06570982441908 * D ,
            // where D is the interval, in UT1 days including any fraction of a day, since 2000 January 1, at 12h UT (interval counted positive if forwards to a later time than the 2000 reference instant), and the result is freed from any integer multiples of 24 hours to reduce it to a value in the range 0-24.[4]

            // UT1 matches UTC within a second http://en.wikipedia.org/wiki/DUT1
            // I guess that's good enough for me.

            var D = moment.Subtract(Constants.J2000Noon_UT).TotalDays;
            return Angle.FromTime(TimeSpan.FromHours(18.697374558 + 24.06570982441908 * D));
        }

        /// <summary>
        /// Equation of Time: the difference between mean and apparent solar time.
        /// Derived from the Sun's actual RA vs. the mean sun's uniform RA.
        /// Positive means the sundial is ahead of the clock.
        /// </summary>
        public static TimeSpan EquationOfTime(this DateTimeOffset moment)
        {
            var D = moment.Subtract(Constants.J2000Noon_UT).TotalDays;

            // Mean sun RA advances uniformly at ~360°/365.2422 days from the J2000 reference
            // Mean longitude of the Sun at J2000: 280.4600°
            var meanLongitude = Angle.FromDegrees(280.4600 + 0.9856474 * D);
            // Apparent sun RA from the solar position model
            var sunRA = SolarSystem.Sun(moment).RightAscension;
            // EoT = mean solar RA - apparent solar RA
            var diff = (meanLongitude - sunRA).SymmetricNormalized;

            return diff.Time;
        }
    }
}
