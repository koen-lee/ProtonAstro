using System;

namespace ProtonAstroLib
{
    public static class Catalog
    {
        // Stars
        public static readonly EquatorialCoordinate Betelgeuse = EquatorialCoordinate.FromRaDec("05:55:10.3", "07d24m25s");
        public static readonly EquatorialCoordinate Rigel = EquatorialCoordinate.FromRaDec("05:14:32.3", "-08d12m06s");
        public static readonly EquatorialCoordinate Aldebaran = EquatorialCoordinate.FromRaDec("04:35:55.2", "16d30m33s");
        public static readonly EquatorialCoordinate Polaris = EquatorialCoordinate.FromRaDec("02:31:49.1", "89d15m51s");
        public static readonly EquatorialCoordinate Sirius = EquatorialCoordinate.FromRaDec("06:45:08.9", "-16d42m58s");
        public static readonly EquatorialCoordinate Vega = EquatorialCoordinate.FromRaDec("18:36:56.3", "38d47m01s");

        // Messier objects
        public static readonly EquatorialCoordinate CrabNebula = EquatorialCoordinate.FromRaDec("05:34:32", "22d00m52s");
        public static readonly EquatorialCoordinate AndromedaGalaxy = EquatorialCoordinate.FromRaDec("00:42:44", "41d16m09s");
        public static readonly EquatorialCoordinate OrionNebula = EquatorialCoordinate.FromRaDec("05:35:17", "-05d23m28s");
        public static readonly EquatorialCoordinate Pleiades = EquatorialCoordinate.FromRaDec("03:47:00", "24d07m00s");
    }
}
