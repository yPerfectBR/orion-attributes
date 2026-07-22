using Orion.Gameplay;
using Orion.PluginContracts;

namespace OrionAttributes;

public sealed class OrionAttributesPlugin : IOrionPlugin
{
    public string Id => "orion:attributes";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context)
    {
        var assembly = typeof(OrionAttributesPlugin).Assembly;
        context.Registries.EntityTraits.RegisterFromAssembly(assembly, Id);
    }

    public void OnEnable(IPluginContext context)
    {
        AttributeGameplayServices services = new(context.Server, context.Services);
        context.Services.Register<IAttributesApi>(services, this);
        context.Services.Register<IEntityHealthService>(services, this);
        context.Services.Register<IPlayerHungerService>(services, this);
        context.Services.Register<IPlayerItemUseHandler>(services, this);
    }

    public void OnWorldInitialize(IWorldInitContext context) => _ = context;

    public void OnDisable(IPluginContext context) => _ = context;
}
