using Orion.Api;
using Orion.Api.Events;
using Orion.Api.Network;
using Orion.Api.Traits;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using ApiVec3f = Orion.Api.Math.Vec3f;
using ProtoVec3f = Orion.Protocol.Types.Vec3f;

namespace OrionAttributes;

/// <summary>
/// Player health via Orion.Api attributes (no Orion.dll).
/// </summary>
public sealed class EntityHealthTrait : EntityTraitBase
{
    public new static string Identifier => "health";
    public static readonly string[] Types = ["minecraft:player"];
    public static readonly string[] Components = ["minecraft:health"];

    private const string HealthAttribute = "minecraft:health";
    private const float DefaultHealth = 20f;
    private const float KnockbackHorizontalForce = 0.28f;
    private const float KnockbackVerticalForce = 0.38f;
    private const float KnockbackVerticalLimit = 0.4f;
    private const ulong KnockbackCooldownTicks = 10;

    private ulong _lastKnockbackTick;

    public IEntity Entity { get; }

    public EntityHealthTrait(IEntity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public float MinimumValue =>
        Entity.TryGetAttribute(HealthAttribute, out float min, out _, out _, out _) ? min : 0f;

    public float MaximumValue =>
        Entity.TryGetAttribute(HealthAttribute, out _, out float max, out _, out _) ? max : DefaultHealth;

    public float DefaultValue =>
        Entity.TryGetAttribute(HealthAttribute, out _, out _, out _, out float def) ? def : DefaultHealth;

    public float CurrentValue
    {
        get => Entity.TryGetAttribute(HealthAttribute, out _, out _, out float current, out _)
            ? current
            : DefaultHealth;
        set
        {
            float min = MinimumValue;
            float max = MaximumValue;
            float def = DefaultValue;
            Entity.SetAttribute(HealthAttribute, min, max, Math.Clamp(value, min, max), def);
            Entity.SyncAttributes();
        }
    }

    public override void OnAdd()
    {
        if (Entity.TryGetAttribute(HealthAttribute, out _, out _, out _, out _))
        {
            return;
        }

        Entity.SetAttribute(HealthAttribute, 0f, DefaultHealth, DefaultHealth, DefaultHealth);
        Entity.SyncAttributes();
    }

    public void ApplyDamage(float amount, IEntity? damager = null, int? damageCause = null)
    {
        EntityHurtSignal signal = new(Entity, amount, damageCause, damager);
        AttributeGameplayServices.Instance?.Emit(signal);
        if (signal.Cancelled)
        {
            return;
        }

        CurrentValue -= signal.Amount;

        if (signal.DamageCause == (int)ActorDamageCause.EntityAttack
            && damager is not null
            && Entity.Dimension is not null
            && damager.Dimension == Entity.Dimension)
        {
            ulong currentTick = Entity.Dimension.CurrentTick;
            if (currentTick >= _lastKnockbackTick
                && currentTick - _lastKnockbackTick >= KnockbackCooldownTicks)
            {
                float x = Entity.Position.X - damager.Position.X;
                float z = Entity.Position.Z - damager.Position.Z;
                float length = MathF.Sqrt((x * x) + (z * z));
                if (length > 0.0001f)
                {
                    float invLength = 1f / length;
                    ApiVec3f velocity = Entity.Velocity;
                    float velocityX = velocity.X * 0.5f;
                    float velocityY = velocity.Y * 0.5f;
                    float velocityZ = velocity.Z * 0.5f;
                    velocityX += x * invLength * KnockbackHorizontalForce;
                    velocityY += KnockbackVerticalForce;
                    velocityZ += z * invLength * KnockbackHorizontalForce;
                    if (velocityY > KnockbackVerticalLimit)
                    {
                        velocityY = KnockbackVerticalLimit;
                    }

                    Entity.Velocity = new ApiVec3f(velocityX, velocityY, velocityZ);
                    _lastKnockbackTick = currentTick;
                }
            }
        }

        if (Entity.Dimension is not null)
        {
            ActorEventPacket packet = new()
            {
                ActorRuntimeId = Entity.RuntimeId,
                Event = ActorEvent.Hurt,
                Data = signal.DamageCause ?? (int)ActorDamageCause.None,
                FiredAt = new Optional<ProtoVec3f>
                {
                    HasValue = true,
                    Value = new ProtoVec3f(Entity.Position.X, Entity.Position.Y, Entity.Position.Z)
                }
            };
            Entity.Dimension.Broadcast(new OpaqueOutboundPacket(packet));
        }

        if (Entity is IPlayer)
        {
            Entity.GetTrait<PlayerHungerTrait>()?.AddExhaustion(0.1f);
        }

        if (CurrentValue <= 0f)
        {
            Entity.Kill(damager, signal.DamageCause);
        }
    }
}
