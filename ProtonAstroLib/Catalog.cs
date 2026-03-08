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
        public static readonly EquatorialCoordinate Canopus = EquatorialCoordinate.FromRaDec("06:23:57.1", "-52d41m44s");
        public static readonly EquatorialCoordinate Achernar = EquatorialCoordinate.FromRaDec("01:37:42.8", "-57d14m12s");
        public static readonly EquatorialCoordinate AlphaCentauri = EquatorialCoordinate.FromRaDec("14:39:36.5", "-60d50m02s");
        public static readonly EquatorialCoordinate Acrux = EquatorialCoordinate.FromRaDec("12:26:35.9", "-63d05m57s");

        // Messier objects
        public static readonly EquatorialCoordinate CrabNebula = EquatorialCoordinate.FromRaDec("05:34:32", "22d00m52s");
        public static readonly EquatorialCoordinate AndromedaGalaxy = EquatorialCoordinate.FromRaDec("00:42:44", "41d16m09s");
        public static readonly EquatorialCoordinate OrionNebula = EquatorialCoordinate.FromRaDec("05:35:17", "-05d23m28s");
        public static readonly EquatorialCoordinate Pleiades = EquatorialCoordinate.FromRaDec("03:47:00", "24d07m00s");

        // Southern DSOs
        public static readonly EquatorialCoordinate LargeMagellanicCloud = EquatorialCoordinate.FromRaDec("05:23:34", "-69d45m22s");
        public static readonly EquatorialCoordinate SmallMagellanicCloud = EquatorialCoordinate.FromRaDec("00:52:45", "-72d49m43s");
        public static readonly EquatorialCoordinate OmegaCentauri = EquatorialCoordinate.FromRaDec("13:26:47", "-47d28m46s");
        public static readonly EquatorialCoordinate EtaCarinaeNebula = EquatorialCoordinate.FromRaDec("10:43:50", "-59d52m04s");
        public static readonly EquatorialCoordinate CentaurusA = EquatorialCoordinate.FromRaDec("13:25:28", "-43d01m09s");

    }

    public static class Constellations
    {
        // Southern Cross (Crux) — traced top-to-bottom then left-to-right
        // Gacrux → Acrux (vertical bar), Delta Crucis → Mimosa (horizontal bar)
        public static readonly EquatorialCoordinate[] SouthernCross =
        [
            EquatorialCoordinate.FromRaDec("12:31:09.9", "-57d06m48s"), // Gacrux  (γ Cru) — top
            EquatorialCoordinate.FromRaDec("12:26:35.9", "-63d05m57s"), // Acrux   (α Cru) — bottom
            EquatorialCoordinate.FromRaDec("12:15:08.7", "-58d44m56s"), // Imai    (δ Cru) — left
            EquatorialCoordinate.FromRaDec("12:47:43.3", "-59d41m20s"), // Mimosa  (β Cru) — right
        ];

        // Big Dipper (Ursa Major) — traced bowl then handle
        // Dubhe → Merak → Phecda → Megrez (bowl), Megrez → Alioth → Mizar → Alkaid (handle)
        public static readonly EquatorialCoordinate[] BigDipper =
        [
            EquatorialCoordinate.FromRaDec("11:03:43.7", "61d45m03s"),  // Dubhe   (α UMa)
            EquatorialCoordinate.FromRaDec("11:01:50.5", "56d22m57s"),  // Merak   (β UMa)
            EquatorialCoordinate.FromRaDec("11:53:49.8", "53d41m41s"),  // Phecda  (γ UMa)
            EquatorialCoordinate.FromRaDec("12:15:25.6", "57d01m57s"),  // Megrez  (δ UMa)
            EquatorialCoordinate.FromRaDec("12:54:01.7", "55d57m35s"),  // Alioth  (ε UMa)
            EquatorialCoordinate.FromRaDec("13:23:55.5", "54d55m31s"),  // Mizar   (ζ UMa)
            EquatorialCoordinate.FromRaDec("13:47:32.4", "49d18m48s"),  // Alkaid  (η UMa)
        ];
    }
}
