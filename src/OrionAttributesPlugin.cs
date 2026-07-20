using Orion.Gameplay;
using Orion.PluginContracts;
using Orion.PluginContracts.Services;
using System.Reflection;

namespace OrionAttributes;

/// <summary>
/// Opt-in vanilla vitals: health, hunger, food use. Exposes
/// <see cref="IAttributesApi"/> / <see cref="IEntityHealthService"/> /
/// <see cref="IPlayerHungerService"/> for other plugins.
/// </summary>
public sealed class OrionAttributesPlugin : IOrionPlugin
{
    public string Id => "orion:attributes";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context)
    {
        Assembly assembly = typeof(OrionAttributesPlugin).Assembly;
        context.Registries.EntityTraits.RegisterFromAssembly(assembly, Id);
        context.Registries.ItemTraits.RegisterFromAssembly(assembly, Id);
    }

    public void OnEnable(IPluginContext context)
    {
        AttributeGameplayServices services = new();
        context.Services.Register<IAttributesApi>(services, this);
        context.Services.Register<IEntityHealthService>(services, this);
        context.Services.Register<IPlayerHungerService>(services, this);
        context.Services.Register<IPlayerItemUseHandler>(services, this);
    }

    public void OnWorldInitialize(IWorldInitContext context) => _ = context;

    public void OnDisable(IPluginContext context) => _ = context;
}
