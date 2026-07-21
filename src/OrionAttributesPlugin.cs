using Orion.Gameplay;
using Orion.PluginContracts;

namespace OrionAttributes;

public sealed class OrionAttributesPlugin : IOrionPlugin
{
    public string Id => "orion:attributes";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context) => _ = context;

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
