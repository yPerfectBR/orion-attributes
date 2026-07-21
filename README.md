# Orion Attributes

Opt-in vanilla vitals: **health**, **hunger**, and food consumption.

- **Manifest id:** `orion:attributes`
- **Provides:** `orion:attributes`, `orion:health`, `orion:hunger`
- **Soft depend:** `orion:inventory` (food use goes through player inventory when loaded)

## Build

Requires the [Orion SDK](https://www.nuget.org/packages/Orion.Api) (`0.1.*`) from NuGet:

```bash
dotnet build OrionAttributes.csproj -c Release
```

Deploy `plugin.json` and `orion.attributes.dll` under `plugins/orion:attributes/` on the server.

## API

```csharp
if (context.Services.TryGet(out IAttributesApi? api) && api is not null)
{
    _ = api.Health.TryHeal(player, 4f);
    _ = api.Hunger.TryAddExhaustion(player, 0.1f);
}
```

Registered services: `IAttributesApi`, `IEntityHealthService`, `IPlayerHungerService`, `IPlayerItemUseHandler`.

## CI

GitHub Actions builds the plugin, runs `PackageReferenceTests`, checks out [OrionServerBE](https://github.com/OrionBedrock/OrionServerBE), and smoke-boots the server with this plugin loaded.
