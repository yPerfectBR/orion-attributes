using Orion.Api;
using Orion.Api.Events;
using Orion.Api.Items;
using Orion.Gameplay;
using Orion.PluginContracts.Services;

namespace OrionAttributes;

/// <summary>
/// Implements host gameplay services for health, hunger, and food item use (Api-only).
/// </summary>
public sealed class AttributeGameplayServices :
    IAttributesApi,
    IEntityHealthService,
    IPlayerHungerService,
    IPlayerItemUseHandler
{
    const ulong DefaultFoodUseTicks = 32UL;

    readonly IServer _server;
    readonly IServiceRegistry _services;

    internal static AttributeGameplayServices? Instance { get; private set; }

    public AttributeGameplayServices(IServer server, IServiceRegistry services)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Instance = this;
    }

    public IEntityHealthService Health => this;
    public IPlayerHungerService Hunger => this;

    internal void Emit(ISignal signal) => _server.Emit(signal);

    public void EnableHud(IPlayer player)
    {
        if (player.GetTrait<EntityHealthTrait>() is null
            && player.GetTrait<PlayerHungerTrait>() is null)
        {
            return;
        }

        player.SetHud(HudVisibility.Reset, HudElement.Health, HudElement.Hunger);
    }

    public bool TryApplyDamage(IEntity entity, float amount, IEntity? damager = null, int? damageCause = null)
    {
        EntityHealthTrait? health = entity.GetTrait<EntityHealthTrait>();
        if (health is null || !entity.IsAlive)
        {
            return false;
        }

        health.ApplyDamage(amount, damager, damageCause);
        return true;
    }

    public bool TryHeal(IEntity entity, float amount)
    {
        EntityHealthTrait? health = entity.GetTrait<EntityHealthTrait>();
        if (health is null || amount <= 0f)
        {
            return false;
        }

        float before = health.CurrentValue;
        health.CurrentValue = MathF.Min(health.MaximumValue, health.CurrentValue + amount);
        return health.CurrentValue > before;
    }

    public bool TryGet(IEntity entity, out float current, out float maximum)
    {
        EntityHealthTrait? health = entity.GetTrait<EntityHealthTrait>();
        if (health is null)
        {
            current = 0f;
            maximum = 0f;
            return false;
        }

        current = health.CurrentValue;
        maximum = health.MaximumValue;
        return true;
    }

    public bool TrySet(IEntity entity, float current)
    {
        EntityHealthTrait? health = entity.GetTrait<EntityHealthTrait>();
        if (health is null)
        {
            return false;
        }

        health.CurrentValue = Math.Clamp(current, health.MinimumValue, health.MaximumValue);
        return true;
    }

    public bool TryEat(IPlayer player, int nutrition, float saturationModifier, bool canAlwaysEat)
    {
        PlayerHungerTrait? hunger = player.GetTrait<PlayerHungerTrait>();
        return hunger is not null && hunger.Eat(nutrition, saturationModifier, canAlwaysEat);
    }

    public bool TryAddExhaustion(IPlayer player, float amount)
    {
        PlayerHungerTrait? hunger = player.GetTrait<PlayerHungerTrait>();
        if (hunger is null)
        {
            return false;
        }

        hunger.AddExhaustion(amount);
        return true;
    }

    public bool TryGet(IPlayer player, out float hunger, out float saturation, out float exhaustion)
    {
        PlayerHungerTrait? trait = player.GetTrait<PlayerHungerTrait>();
        if (trait is null)
        {
            hunger = 0f;
            saturation = 0f;
            exhaustion = 0f;
            return false;
        }

        hunger = trait.CurrentValue;
        saturation = trait.Saturation;
        exhaustion = trait.Exhaustion;
        return true;
    }

    public bool TrySetHunger(IPlayer player, float hunger, float? saturation = null)
    {
        PlayerHungerTrait? trait = player.GetTrait<PlayerHungerTrait>();
        if (trait is null)
        {
            return false;
        }

        trait.SetHunger(hunger, saturation);
        return true;
    }

    public bool TryBeginUse(IPlayer player, IItemStack heldItem, out ulong durationTicks)
    {
        durationTicks = 0UL;
        PlayerHungerTrait? hunger = player.GetTrait<PlayerHungerTrait>();
        if (hunger is null
            || !heldItem.Type.TryGetFood(out _, out _, out bool canAlwaysEat, out _))
        {
            return false;
        }

        if (!canAlwaysEat && hunger.CurrentValue >= hunger.MaximumValue)
        {
            return false;
        }

        durationTicks = heldItem.Type.TryGetUseDurationTicks(out ulong ticks)
            ? Math.Max(1UL, ticks)
            : DefaultFoodUseTicks;
        return true;
    }

    public bool TryCompleteUse(IPlayer player, IItemStack heldItem, int slot)
    {
        if (!heldItem.Type.TryGetFood(
                out int nutrition,
                out float saturationModifier,
                out bool canAlwaysEat,
                out string? usingConvertsTo))
        {
            return false;
        }

        PlayerHungerTrait? hunger = player.GetTrait<PlayerHungerTrait>();
        if (hunger is null || !hunger.Eat(nutrition, saturationModifier, canAlwaysEat))
        {
            return false;
        }

        if (_services.TryGet(out IPlayerInventoryService? inventory)
            && inventory is not null
            && inventory.TryGetAccess(player, out IPlayerInventoryAccess? access)
            && access is not null)
        {
            heldItem.Decrement();
            if (heldItem.Count <= 0)
            {
                access.Container.ClearSlot(slot);
            }
            else
            {
                access.Container.UpdateSlot(slot);
            }

            if (!string.IsNullOrWhiteSpace(usingConvertsTo)
                && Items.TryCreate(usingConvertsTo) is IItemStack converted
                && !access.Container.AddItem(converted))
            {
                _ = player.DropItem(converted);
            }
        }

        player.SyncAttributes();
        return true;
    }
}
