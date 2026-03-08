using ProtonAstroLib;

namespace TelescopeDrive.Models;

public record CatalogEntry(string Name, EquatorialCoordinate Coordinate);

public static class CatalogEntries
{
    public static readonly IReadOnlyList<CatalogEntry> All =
    [
        new("Polaris", Catalog.Polaris),
        new("Vega", Catalog.Vega),
        new("Sirius", Catalog.Sirius),
        new("Betelgeuse", Catalog.Betelgeuse),
        new("Rigel", Catalog.Rigel),
        new("Aldebaran", Catalog.Aldebaran),
        new("Canopus", Catalog.Canopus),
        new("Achernar", Catalog.Achernar),
        new("Alpha Centauri", Catalog.AlphaCentauri),
        new("Acrux", Catalog.Acrux),
        new("M1 - Crab Nebula", Catalog.CrabNebula),
        new("M31 - Andromeda", Catalog.AndromedaGalaxy),
        new("M42 - Orion Nebula", Catalog.OrionNebula),
        new("M45 - Pleiades", Catalog.Pleiades),
        new("LMC - Large Magellanic Cloud", Catalog.LargeMagellanicCloud),
        new("SMC - Small Magellanic Cloud", Catalog.SmallMagellanicCloud),
        new("NGC 5139 - Omega Centauri", Catalog.OmegaCentauri),
        new("NGC 3372 - Eta Carinae Nebula", Catalog.EtaCarinaeNebula),
        new("NGC 5128 - Centaurus A", Catalog.CentaurusA),
    ];

    public static CatalogEntry? FindByName(string name) =>
        All.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
