using System.Text.Json;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

/// <summary>
/// Demonstrates the one integration this framework's own controlling Work
/// Package names explicitly: a <see cref="Quantity{TDimension}"/> stored as
/// <see cref="IEngineeringDocumentStore"/> content. No other Platform
/// Service integration is demonstrated, or required — see this Work
/// Package's own Implementation Report for why (`Tempest.Core.UnitsAndQuantities`
/// depends on no Platform Service, and none depends on it).
/// </summary>
public class PlatformIntegrationTests
{
    [Fact]
    public async Task Quantity_StoredAsEngineeringDocumentContent_RoundTripsThroughRevision()
    {
        var store = new EngineeringDocumentStore(new InMemoryPersistenceStore(), new CurrentPrincipalAccessor());
        var density = new Quantity<Mass>(7850.0, MassUnits.Kilogram); // kg per cubic metre, expressed as a bare Mass quantity for this demonstration

        var document = await store.CreateAsync("MaterialProperty", JsonSerializer.Serialize(density));
        var storedHistory = await store.GetRevisionHistoryAsync(document.Id);
        var storedQuantity = JsonSerializer.Deserialize<Quantity<Mass>>(storedHistory[0].Content);

        Assert.Equal(density, storedQuantity);

        var revisedDensity = density.ConvertTo(MassUnits.Pound);
        await store.ReviseAsync(document.Id, JsonSerializer.Serialize(revisedDensity), "Converted to Imperial units");
        var latestRevision = (await store.GetRevisionHistoryAsync(document.Id))[^1];
        var latestQuantity = JsonSerializer.Deserialize<Quantity<Mass>>(latestRevision.Content);

        Assert.Equal(revisedDensity, latestQuantity);
    }
}
