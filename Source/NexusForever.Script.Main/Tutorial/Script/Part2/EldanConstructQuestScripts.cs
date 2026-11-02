using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// Deconstruct the Construct (10520 Exile / 10523 Dominion): "Destroy the Virtual Eldan Defense Construct" has no creature to
    /// destroy, the construct (73499) is not placed in the world data and retail spawned it when the player reached the platform.
    /// Stand-in: once the platform objective is done, one hostile construct appears on the platform for ten minutes.
    /// SEMANTICS: "Eldan construct spawns on the platform" (2026-11-01, quest bot).
    /// </summary>
    public abstract class EldanConstructQuestScript : IQuestScript, IOwnedScript<IQuest>
    {
        private const uint VirtualEldanDefenseConstruct = 73499u;
        private const byte PlatformObjective = 2;
        private const byte DestroyObjective = 3;
        private const Faction VirtualBattleBeastFaction = (Faction)1441;
        private static readonly Vector3 PlatformPosition = new(429.3f, -388.9f, 253.8f); // WorldLocation2 51744

        private IQuest owner;
        private bool spawned;
        private double timer;

        #region Dependency Injection

        private readonly IEntityFactory entityFactory;
        private readonly ICreatureInfoManager creatureInfoManager;

        protected EldanConstructQuestScript(
            IEntityFactory entityFactory,
            ICreatureInfoManager creatureInfoManager)
        {
            this.entityFactory       = entityFactory;
            this.creatureInfoManager = creatureInfoManager;
        }

        #endregion

        public void OnLoad(IQuest owner)
        {
            this.owner = owner;
        }

        public void Update(double lastTick)
        {
            timer += lastTick;
            if (timer < 1d)
                return;
            timer = 0d;

            if (spawned || owner?.State != QuestState.Accepted)
                return;
            if (!(owner.GetQuestObjectiveByIndex(PlatformObjective)?.IsComplete() ?? false))
                return;
            if (owner.GetQuestObjectiveByIndex(DestroyObjective)?.IsComplete() ?? true)
                return;

            IPlayer player = owner.GetOwner();
            if (player?.Map == null || player.Map.Entry.Id != 3460u)
                return;

            ICreatureInfo creatureInfo = creatureInfoManager.GetCreatureInfo(VirtualEldanDefenseConstruct);
            if (creatureInfo == null)
                return;

            spawned = true;
            IWorldEntity construct = entityFactory.CreateWorldEntity(EntityType.NonPlayer);
            construct.Initialise(creatureInfo);
            construct.Faction1 = VirtualBattleBeastFaction;
            construct.Faction2 = VirtualBattleBeastFaction;
            construct.AddToMap(player.Map, PlatformPosition, (_, _, _) =>
            {
                if (construct is IUnitEntity unit)
                    unit.DespawnAfter(TimeSpan.FromMinutes(10d));
            });
        }
    }

    [ScriptFilterOwnerId(10520)]
    public class EldanConstructExileQuestScript : EldanConstructQuestScript
    {
        public EldanConstructExileQuestScript(IEntityFactory entityFactory, ICreatureInfoManager creatureInfoManager)
            : base(entityFactory, creatureInfoManager)
        {
        }
    }

    [ScriptFilterOwnerId(10523)]
    public class EldanConstructDominionQuestScript : EldanConstructQuestScript
    {
        public EldanConstructDominionQuestScript(IEntityFactory entityFactory, ICreatureInfoManager creatureInfoManager)
            : base(entityFactory, creatureInfoManager)
        {
        }
    }
}
