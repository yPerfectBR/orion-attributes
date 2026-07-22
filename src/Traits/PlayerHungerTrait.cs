using Orion.Api;
using Orion.Api.Traits;
using Orion.Protocol.Enums;
using ApiGamemode = Orion.Api.Gamemode;

namespace OrionAttributes;

/// <summary>
/// Player hunger / saturation / exhaustion via Orion.Api attributes.
/// </summary>
public sealed class PlayerHungerTrait : EntityTraitBase
{
    public new static string Identifier => "hunger";
    public static readonly string[] Types = ["minecraft:player"];

    private const string HungerAttribute = "minecraft:player.hunger";
    private const string SaturationAttribute = "minecraft:player.saturation";
    private const string ExhaustionAttribute = "minecraft:player.exhaustion";

    private const float SprintCost = 0.1f;
    private const float SwimCost = 0.01f;
    private const float DrainAt = 4f;
    private const float SatDrain = 1f;
    private const float HungerDrain = 1f;
    private const ulong RegenTicks = 30UL;
    private const float RegenAt = 17f;
    private const float StarveDamage = 1f;
    private const float DefaultSat = 10f;
    private const float MaxHunger = 20f;

    public IEntity Entity { get; }

    public float Saturation { get; private set; } = DefaultSat;
    public float Exhaustion { get; private set; }

    public PlayerHungerTrait(IEntity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public float MinimumValue => 0f;
    public float MaximumValue => MaxHunger;
    public float DefaultValue => MaxHunger;

    public float CurrentValue
    {
        get => Entity.TryGetAttribute(HungerAttribute, out _, out _, out float current, out _)
            ? current
            : MaxHunger;
        set
        {
            float next = Math.Clamp(value, MinimumValue, MaximumValue);
            Entity.SetAttribute(HungerAttribute, MinimumValue, MaximumValue, next, DefaultValue);
            SyncClientAttrs();
        }
    }

    public override void OnAdd()
    {
        if (!Entity.TryGetAttribute(HungerAttribute, out _, out _, out _, out _))
        {
            Entity.SetAttribute(HungerAttribute, 0f, MaxHunger, MaxHunger, MaxHunger);
        }

        SyncClientAttrs();
    }

    public override void OnTick(TraitOnTickDetails details)
    {
        if (Entity is not IPlayer player || !CanLoseHunger(player))
        {
            return;
        }

        EntityHealthTrait? health = player.GetTrait<EntityHealthTrait>();
        if (health is null)
        {
            return;
        }

        AddMovementExhaustion(player);
        TryDrainExhaustion();
        TickHungerEffects(health, player, details.CurrentTick);
    }

    public void AddExhaustion(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Exhaustion += amount;
        SyncClientAttrs();
    }

    public bool Eat(int nutrition, float saturationModifier, bool canAlwaysEat)
    {
        if (!canAlwaysEat && CurrentValue >= MaximumValue)
        {
            return false;
        }

        CurrentValue = Math.Clamp(CurrentValue + nutrition, MinimumValue, MaximumValue);
        Saturation = Math.Clamp(Saturation + nutrition * saturationModifier * 2f, 0f, CurrentValue);
        SyncClientAttrs();
        return true;
    }

    public void SetHunger(float hunger, float? saturation = null)
    {
        CurrentValue = hunger;
        if (saturation is float sat)
        {
            Saturation = Math.Clamp(sat, 0f, CurrentValue);
        }

        SyncClientAttrs();
    }

    private static bool CanLoseHunger(IPlayer player)
    {
        int difficulty = player.Dimension?.Difficulty ?? (int)Difficulty.Normal;
        if (difficulty == (int)Difficulty.Peaceful)
        {
            return false;
        }

        if (!player.IsAlive || player.IsFlying)
        {
            return false;
        }

        return player.Gamemode is not (ApiGamemode.Spectator or ApiGamemode.Creative);
    }

    private void AddMovementExhaustion(IPlayer player)
    {
        if (player.IsSprinting)
        {
            Exhaustion += SprintCost;
        }

        if (player.IsSwimming)
        {
            Exhaustion += SwimCost;
        }
    }

    private void TryDrainExhaustion()
    {
        if (Exhaustion < DrainAt)
        {
            return;
        }

        Exhaustion -= DrainAt;

        if (Saturation > 0f)
        {
            Saturation = MathF.Max(0f, Saturation - SatDrain);
        }
        else
        {
            CurrentValue = MathF.Max(0f, CurrentValue - HungerDrain);
        }

        SyncClientAttrs();
    }

    private void TickHungerEffects(EntityHealthTrait health, IPlayer player, ulong tick)
    {
        if (tick % RegenTicks != 0UL)
        {
            return;
        }

        if (CurrentValue > RegenAt)
        {
            health.CurrentValue = MathF.Min(health.MaximumValue, health.CurrentValue + 1f);
        }
        else if (CurrentValue <= 0f)
        {
            health.ApplyDamage(StarveDamage, player, (int)ActorDamageCause.Starve);
        }
    }

    private void SyncClientAttrs()
    {
        Entity.SetAttribute(SaturationAttribute, 0f, MaxHunger, Math.Clamp(Saturation, 0f, MaxHunger), DefaultSat);
        Entity.SetAttribute(ExhaustionAttribute, 0f, MaxHunger, Math.Clamp(Exhaustion, 0f, MaxHunger), 0f);
        if (Entity.TryGetAttribute(HungerAttribute, out float min, out float max, out float current, out float def))
        {
            Entity.SetAttribute(HungerAttribute, min, max, current, def);
        }

        Entity.SyncAttributes();
    }
}
