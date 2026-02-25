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
        new("M1 - Crab Nebula", Catalog.CrabNebula),
        new("M31 - Andromeda", Catalog.AndromedaGalaxy),
        new("M42 - Orion Nebula", Catalog.OrionNebula),
        new("M45 - Pleiades", Catalog.Pleiades),
    ];

    public static CatalogEntry? FindByName(string name) =>
        All.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
