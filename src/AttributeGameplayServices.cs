using Orion.Api;
using Orion.Api.Events;
using Orion.Api.Items;
using Orion.Entity.Traits;
using Orion.Gameplay;
using Orion.Item;
using Orion.Item.Traits;
using Orion.Player;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Nbt;
using Entity = Orion.Entity.Entity;

namespace OrionAttributes;

/// <summary>
/// Implements host gameplay services for health, hunger, and food item use.
/// </summary>
public sealed class AttributeGameplayServices :
    IAttributesApi,
    IEntityHealthService,
    IPlayerHungerService,
    IPlayerItemUseHandler
{
    const ulong DefaultFoodUseTicks = 32UL;

    public IEntityHealthService Health => this;
    public IPlayerHungerService Hunger => this;

    public void EnableHud(IPlayer player)
    {
        Player concrete = RequirePlayer(player);
        List<Orion.Protocol.Enums.HudElement> elements = [];
        if (concrete.GetTrait<EntityHealthTrait>() is not null)
        {
            elements.Add(Orion.Protocol.Enums.HudElement.Health);
        }

        if (concrete.GetTrait<PlayerHungerTrait>() is not null)
        {
            elements.Add(Orion.Protocol.Enums.HudElement.Hunger);
        }

        if (elements.Count == 0)
        {
            return;
        }

        concrete.SetHud(Orion.Protocol.Enums.HudVisibility.Reset, elements.ToArray());
    }

    public bool TryApplyDamage(
        IEntity entity,
        float amount,
        IEntity? damager = null,
        int? damageCause = null)
    {
        Entity concrete = RequireEntity(entity);
        Entity? concreteDamager = damager is null ? null : RequireEntity(damager);
        ActorDamageCause? cause = damageCause.HasValue ? (ActorDamageCause)damageCause.Value : null;

        EntityHealthTrait? health = concrete.GetTrait<EntityHealthTrait>();
        if (health is null || !concrete.IsAlive)
        {
            return false;
        }

        health.ApplyDamage(amount, concreteDamager, cause);
        return true;
    }

    public bool TryHeal(IEntity entity, float amount)
    {
        Entity concrete = RequireEntity(entity);
        EntityHealthTrait? health = concrete.GetTrait<EntityHealthTrait>();
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
        EntityHealthTrait? health = RequireEntity(entity).GetTrait<EntityHealthTrait>();
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
        EntityHealthTrait? health = RequireEntity(entity).GetTrait<EntityHealthTrait>();
        if (health is null)
        {
            return false;
        }

        health.CurrentValue = Math.Clamp(current, health.MinimumValue, health.MaximumValue);
        return true;
    }

    public bool TryEat(IPlayer player, int nutrition, float saturationModifier, bool canAlwaysEat)
    {
        PlayerHungerTrait? hunger = RequirePlayer(player).GetTrait<PlayerHungerTrait>();
        return hunger is not null && hunger.Eat(nutrition, saturationModifier, canAlwaysEat);
    }

    public bool TryAddExhaustion(IPlayer player, float amount)
    {
        PlayerHungerTrait? hunger = RequirePlayer(player).GetTrait<PlayerHungerTrait>();
        if (hunger is null)
        {
            return false;
        }

        hunger.Exhaustion += amount;
        return true;
    }

    public bool TryGet(IPlayer player, out float hunger, out float saturation, out float exhaustion)
    {
        PlayerHungerTrait? trait = RequirePlayer(player).GetTrait<PlayerHungerTrait>();
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
        PlayerHungerTrait? trait = RequirePlayer(player).GetTrait<PlayerHungerTrait>();
        if (trait is null)
        {
            return false;
        }

        trait.CurrentValue = Math.Clamp(hunger, trait.MinimumValue, trait.MaximumValue);
        if (saturation is float sat)
        {
            trait.Saturation = Math.Clamp(sat, 0f, trait.CurrentValue);
        }

        return true;
    }

    public bool TryBeginUse(IPlayer player, IItemStack heldItem, out ulong durationTicks)
    {
        durationTicks = 0UL;
        Player concrete = RequirePlayer(player);
        ItemStack stack = RequireStack(heldItem);
        ItemStackFoodTrait? food = stack.GetTrait<ItemStackFoodTrait>();
        PlayerHungerTrait? hunger = concrete.GetTrait<PlayerHungerTrait>();
        if (food is null || hunger is null)
        {
            return false;
        }

        if (!food.CanAlwaysEat && hunger.CurrentValue >= hunger.MaximumValue)
        {
            return false;
        }

        durationTicks = GetUseDurationTicks(stack);
        return true;
    }

    public bool TryCompleteUse(IPlayer player, IItemStack heldItem, int slot)
    {
        Player concrete = RequirePlayer(player);
        ItemStack stack = RequireStack(heldItem);
        ItemStackFoodTrait? food = stack.GetTrait<ItemStackFoodTrait>();
        PlayerHungerTrait? hunger = concrete.GetTrait<PlayerHungerTrait>();
        if (food is null || hunger is null)
        {
            return false;
        }

        if (concrete.Dimension?.World?.Server is Orion.Server server)
        {
            PlayerFoodEatSignal eatSignal = new(concrete, stack);
            server.Emit(eatSignal);
            if (!eatSignal.Emit())
            {
                return false;
            }
        }

        if (!hunger.Eat(food.Nutrition, food.SaturationModifier, food.CanAlwaysEat))
        {
            return false;
        }

        if (!PluginHost.Services.TryGet(out IPlayerInventoryService? inventory)
            || inventory is null
            || !inventory.TryGetAccess(player, out IPlayerInventoryAccess? access)
            || access is null)
        {
            return false;
        }

        stack.DecrementStack();
        if (stack.StackSize == 0)
        {
            access.Container.ClearSlot(slot);
        }
        else
        {
            access.Container.UpdateSlot(slot);
        }

        if (!string.IsNullOrWhiteSpace(food.UsingConvertsTo) && ItemType.Get(food.UsingConvertsTo) is ItemType convertedType)
        {
            ItemStack converted = new(convertedType);
            if (!access.Container.AddItem(converted))
            {
                _ = concrete.DropItem(converted);
            }
        }

        concrete.SendAttributes();
        return true;
    }

    static ulong GetUseDurationTicks(ItemStack item)
    {
        if (item.Type.TryGetComponentProperties("minecraft:use_duration", out CompoundTag tag))
        {
            return (ulong)Math.Max(1, tag.Get<IntTag>("value")?.Value ?? (int)DefaultFoodUseTicks);
        }

        return DefaultFoodUseTicks;
    }

    static Player RequirePlayer(IPlayer player) =>
        player as Player ?? throw new ArgumentException("Player must be an Orion.Player.Player.", nameof(player));

    static Entity RequireEntity(IEntity entity) =>
        entity as Entity ?? throw new ArgumentException("Entity must be an Orion.Entity.Entity.", nameof(entity));

    static ItemStack RequireStack(IItemStack stack) =>
        stack as ItemStack ?? throw new ArgumentException("Stack must be an Orion.Item.ItemStack.", nameof(stack));
}
