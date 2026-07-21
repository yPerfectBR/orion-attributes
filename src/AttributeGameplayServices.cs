using Orion.Api;
using Orion.Api.Items;
using Orion.Gameplay;

namespace OrionAttributes;

/// <summary>
/// S7 Api-only vitals façade. Host trait-backed health/hunger returns once EntityAttributeTrait
/// surfaces move behind Orion.Api.
/// </summary>
public sealed class AttributeGameplayServices :
    IAttributesApi,
    IEntityHealthService,
    IPlayerHungerService,
    IPlayerItemUseHandler
{
    public IEntityHealthService Health => this;
    public IPlayerHungerService Hunger => this;

    public void EnableHud(IPlayer player) => _ = player;

    public bool TryApplyDamage(IEntity entity, float amount, IEntity? damager = null, int? damageCause = null)
    {
        _ = (entity, amount, damager, damageCause);
        return false;
    }

    public bool TryHeal(IEntity entity, float amount)
    {
        _ = (entity, amount);
        return false;
    }

    public bool TryGet(IEntity entity, out float current, out float maximum)
    {
        _ = entity;
        current = 0;
        maximum = 0;
        return false;
    }

    public bool TrySet(IEntity entity, float current)
    {
        _ = (entity, current);
        return false;
    }

    public bool TryEat(IPlayer player, int nutrition, float saturationModifier, bool canAlwaysEat)
    {
        _ = (player, nutrition, saturationModifier, canAlwaysEat);
        return false;
    }

    public bool TryAddExhaustion(IPlayer player, float amount)
    {
        _ = (player, amount);
        return false;
    }

    public bool TryGet(IPlayer player, out float hunger, out float saturation, out float exhaustion)
    {
        _ = player;
        hunger = 0;
        saturation = 0;
        exhaustion = 0;
        return false;
    }

    public bool TrySetHunger(IPlayer player, float hunger, float? saturation = null)
    {
        _ = (player, hunger, saturation);
        return false;
    }

    public bool TryBeginUse(IPlayer player, IItemStack heldItem, out ulong durationTicks)
    {
        _ = (player, heldItem);
        durationTicks = 0;
        return false;
    }

    public bool TryCompleteUse(IPlayer player, IItemStack heldItem, int slot)
    {
        _ = (player, heldItem, slot);
        return false;
    }
}
