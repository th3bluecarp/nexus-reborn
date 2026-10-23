using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Abstract.Entity.Stat;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Model;

namespace NexusForever.Game.Entity
{
    public class SimpleEntity : UnitEntity, ISimpleEntity
    {
        public override EntityType Type => EntityType.Simple;

        #region Dependency Injection

        public SimpleEntity(
            IMovementManager movementManager,
            IEntitySummonFactory entitySummonFactory,
            IStatUpdateManager<IUnitEntity> statUpdateManager,
            ISpellFactory spellFactory,
            ICreatureInfoManager creatureInfoManager)
            : base(movementManager, entitySummonFactory, statUpdateManager, spellFactory)
        {
            statUpdateManager.Initialise(this);
        }

        #endregion

        protected override IEntityModel BuildEntityModel()
        {
            return new SimpleEntityModel
            {
                CreatureId        = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        public override void OnActivate(IPlayer activator)
        {
            if (CreatureInfo.Entry.DatacubeId != 0u)
                activator.DatacubeManager.AddDatacube((ushort)CreatureInfo.Entry.DatacubeId, int.MaxValue);

            // the client sends a plain activate for soldier holdout control points (no activate cast, though they list
            // spell 1817), so the cast success path that starts path missions never ran and the beacon did nothing
            if (CreatureInfo.Entry.PathMissionIdSoldier != 0u)
                activator.PathMissionManager.OnActivate(CreatureInfo.Entry.Id, AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureInfo.Entry.Id));
        }

        public override void OnActivateSuccess(IPlayer activator)
        {
            base.OnActivateSuccess(activator);

            uint progress = (uint)(1 << QuestChecklistIdx);

            if (CreatureInfo.Entry.DatacubeId != 0u)
            {
                IDatacube datacube = activator.DatacubeManager.GetDatacube((ushort)CreatureInfo.Entry.DatacubeId, DatacubeType.Datacube);
                if (datacube == null)
                    activator.DatacubeManager.AddDatacube((ushort)CreatureInfo.Entry.DatacubeId, progress);
                else
                {
                    datacube.Progress |= progress;
                    activator.DatacubeManager.SendDatacube(datacube);
                }
            }

            if (CreatureInfo.Entry.DatacubeVolumeId != 0u)
            {
                IDatacube datacube = activator.DatacubeManager.GetDatacube((ushort)CreatureInfo.Entry.DatacubeVolumeId, DatacubeType.Journal);
                if (datacube == null)
                    activator.DatacubeManager.AddDatacubeVolume((ushort)CreatureInfo.Entry.DatacubeVolumeId, progress);
                else
                {
                    datacube.Progress |= progress;
                    activator.DatacubeManager.SendDatacubeVolume(datacube);
                }
            }
        }
    }
}
