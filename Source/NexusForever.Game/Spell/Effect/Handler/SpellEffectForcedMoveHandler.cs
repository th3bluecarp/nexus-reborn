using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement.Force;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Entity.Movement.Command.State;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.Shared;

namespace NexusForever.Game.Spell.Effect.Handler
{
    [SpellEffectHandler(SpellEffectType.ForcedMove)]
    public class SpellEffectForcedMoveHandler : ISpellEffectApplyHandler<ISpellEffectForcedMoveData>
    {
        #region Dependency Injection

        private readonly IForcedMovementGenerator forcedMovementGenerator;

        public SpellEffectForcedMoveHandler(
            IForcedMovementGenerator forcedMovementGenerator)
        {
            this.forcedMovementGenerator = forcedMovementGenerator;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectForcedMoveData data)
        {
            float distance = data.MinDistance + ((float)Random.Shared.NextDouble() * (data.MaxDistance - data.MinDistance));
            float gravity  = data.Gravity;
            float data06   = data.Unknown6;

            var v20 = (float)data.FlightTime.TotalSeconds;
            if (data06 == 0)
                data06 = ((gravity * 0.125f) * v20) * v20;
            else
                gravity = (data06 * 8.0f) / (v20 * v20);

            IUnitEntity mover;
            if ((data.Flags & SpellEffectForcedMoveFlags.Target) != 0)
                mover = target;
            else
                mover = executionContext.Spell.Caster;

            Vector3 position = Vector3.Zero;
            float angle = 0f;

            switch (data.MoveType)
            {
                case SpellEffectForcedMoveType.PositionForward:
                case SpellEffectForcedMoveType.KeyForward:
                case SpellEffectForcedMoveType.Unknown11:
                {
                    if (mover == target)
                    {
                        // TODO
                    }

                    angle = -target.Rotation.X - MathF.PI / 2;
                    if (data.Angle != 0f)
                        angle -= data.Angle.ToRadians();

                    position = target.Position.GetPoint2D(angle, distance);
                    break;
                }
                case SpellEffectForcedMoveType.PositionBackward:
                case SpellEffectForcedMoveType.KeyBackward:
                case SpellEffectForcedMoveType.Unknown12:
                {
                    angle = -target.Rotation.X + MathF.PI / 2;
                    if (data.Angle != 0f)
                        angle += data.Angle.ToRadians();

                    position = target.Position.GetPoint2D(angle, distance);
                    break;
                }
                case SpellEffectForcedMoveType.PositionRandom:
                case SpellEffectForcedMoveType.KeyRandom:
                case SpellEffectForcedMoveType.Unknown13:
                {
                    angle = (float)Random.Shared.NextDouble() * (MathF.PI * 2f);
                    position = target.Position.GetPoint2D(angle, distance);
                    break;
                }
                // velocity knock
                case SpellEffectForcedMoveType.KeyVelocity:
                case SpellEffectForcedMoveType.PositionVelocity:
                case SpellEffectForcedMoveType.Unknown14:
                {
                    // TODO
                    StateFlags stateFlags = target.MovementManager.GetState();
                    break;
                }
                case SpellEffectForcedMoveType.Unknown8:  // 60331 - Teleport Target to Caster
                case SpellEffectForcedMoveType.Unknown9:  // 2039 - Get Over Here
                case SpellEffectForcedMoveType.Unknown10: // 4206 - Grapple
                case SpellEffectForcedMoveType.Unknown15:
                {
                    position = mover.Position;
                    break;
                }
            }

            if (position == Vector3.Zero)
                return SpellEffectExecutionResult.PreventEffect;

            // TODO
            if ((data.Flags & SpellEffectForcedMoveFlags.Unknown2) != 0)
            {
            }

            // TODO
            if ((data.Flags & SpellEffectForcedMoveFlags.Unknown4) != 0)
            {
            }

            // TODO
            if ((data.Flags & SpellEffectForcedMoveFlags.Facing) != 0)
            {
            }

            // TODO: this needs to be smarter and take other geometry into account
            // once LoS maps are implemented might be able to use a ray instead
            float? terrainHeight = mover.Map.GetTerrainHeight(position.X, position.Z);
            if (position.Y < terrainHeight)
                position.Y = terrainHeight.Value;

            switch (data.MoveType)
            {
                case SpellEffectForcedMoveType.PositionForward:
                case SpellEffectForcedMoveType.PositionBackward:
                case SpellEffectForcedMoveType.PositionRandom:
                case SpellEffectForcedMoveType.PositionVelocity:
                case SpellEffectForcedMoveType.Unknown8:
                {
                    if (mover is IPlayer player)
                        player.TeleportToLocal(position, false);
                    else
                        mover.MovementManager.SetPosition(position, false);
                    break;
                }
                case SpellEffectForcedMoveType.KeyForward:
                case SpellEffectForcedMoveType.KeyBackward:
                case SpellEffectForcedMoveType.KeyRandom:
                case SpellEffectForcedMoveType.KeyVelocity:
                case SpellEffectForcedMoveType.Unknown9:
                case SpellEffectForcedMoveType.Unknown10:
                // trampolines and jump pads (Jumpshroom 40649, Airboom 41263): a short move with a high arc, Unknown6 is the apex height
                case SpellEffectForcedMoveType.Unknown11:
                case SpellEffectForcedMoveType.Unknown12:
                {
                    //float speed = distance / (float)data.FlightTime.TotalSeconds;
                    float spin = (data.Spin * MathF.PI * 2) / v20;
                    forcedMovementGenerator.ForceMove(mover, position, new Vector3(angle, 0f, 0f), data.FlightTime, gravity, spin);
                    break;
                }
                case SpellEffectForcedMoveType.Unknown13:
                case SpellEffectForcedMoveType.Unknown14:
                case SpellEffectForcedMoveType.Unknown15:
                {
                    break;
                }
            }

            return SpellEffectExecutionResult.Ok;
        }
    }
}