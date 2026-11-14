using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Creature;
using NexusForever.Game.Abstract.Prerequisite;
using NexusForever.Game.Static.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Prerequisite
{
    public class PrerequisiteManager : Singleton<PrerequisiteManager>, IPrerequisiteManager
    {
        #region Dependency Injection

        private readonly ILogger<PrerequisiteManager> log;
        private readonly IServiceProvider serviceProvider;
        private readonly IGameTableManager gameTableManager;
        private readonly IFactory<IPrerequisiteParameters> prerequisiteParametersFactory;

        public PrerequisiteManager(
            ILogger<PrerequisiteManager> log,
            IServiceProvider serviceProvider,
            IGameTableManager gameTableManager,
            IFactory<IPrerequisiteParameters> prerequisiteParametersFactory)
        {
            this.log                           = log;
            this.serviceProvider               = serviceProvider;
            this.gameTableManager              = gameTableManager;
            this.prerequisiteParametersFactory = prerequisiteParametersFactory;
        }

        #endregion

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        public bool Meets(IPlayer player, uint prerequisiteId)
        {
            IPrerequisiteParameters parameters = prerequisiteParametersFactory.Resolve();
            return Meets(player, prerequisiteId, parameters);
        }

        /// <summary>
        /// Checks if a creature caster meets a spell effect's caster prerequisite.
        /// </summary>
        /// <remarks>
        /// Creature auto attacks carry two damage effects gated by <see cref="PrerequisiteType.CreatureDifficulty"/> (one coefficient
        /// for the low ranks, a bigger one for the high ranks). Applying both doubled every creature hit. Checks of other types
        /// are not known for creatures and pass.
        /// </remarks>
        public bool MeetsCreature(IWorldEntity caster, uint prerequisiteId)
        {
            PrerequisiteEntry entry = gameTableManager.Prerequisite.GetEntry(prerequisiteId);
            if (entry == null)
                return true;

            // a holdout boss raised to Prime picks its effects at its own rank, like its damage (PathMissionManager.Soldier.cs)
            uint difficulty = (caster?.CreatureInfo as ICreatureInfoOverride)?.DamageDifficultyEntry?.Id
                ?? caster?.CreatureInfo?.DifficultyEntry?.Id ?? 0u;
            bool any = false, all = true, evaluated = false;
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                if (entry.PrerequisiteTypeId[i] != PrerequisiteType.CreatureDifficulty)
                    continue;

                evaluated = true;
                bool met = entry.PrerequisiteComparisonId[i] switch
                {
                    PrerequisiteComparison.Equal              => difficulty == entry.ObjectId[i],
                    PrerequisiteComparison.NotEqual           => difficulty != entry.ObjectId[i],
                    PrerequisiteComparison.GreaterThanOrEqual => difficulty >= entry.ObjectId[i],
                    PrerequisiteComparison.GreaterThan        => difficulty > entry.ObjectId[i],
                    PrerequisiteComparison.LessThanOrEqual    => difficulty <= entry.ObjectId[i],
                    PrerequisiteComparison.LessThan           => difficulty < entry.ObjectId[i],
                    _                                         => true
                };
                any |= met;
                all &= met;
            }

            if (!evaluated)
                return true;
            return entry.Flags == EvaluationMode.EvaluateOR ? any : all;
        }

        /// <summary>
        /// Checks if <see cref="IPlayer"/> meets supplied prerequisite.
        /// </summary>
        public bool Meets(IPlayer player, uint prerequisiteId, IPrerequisiteParameters parameters)
        {
            PrerequisiteEntry entry = gameTableManager.Prerequisite.GetEntry(prerequisiteId);
            if (entry == null)
                throw new ArgumentException();

            switch (entry.Flags)
            {
                case EvaluationMode.EvaluateAND:
                    return MeetsEvaluateAnd(player, prerequisiteId, entry, parameters);
                case EvaluationMode.EvaluateOR:
                    return MeetsEvaluateOr(player, prerequisiteId, entry, parameters);
                default:
                    log.LogTrace($"Unhandled EvaluationMode {entry.Flags}");
                    return false;
            }
        }

        private bool MeetsEvaluateAnd(IPlayer player, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                PrerequisiteComparison comparison = entry.PrerequisiteComparisonId[i];
                if (!Meets(player, type, comparison, entry.Value[i], entry.ObjectId[i], parameters))
                {
                    log.LogTrace($"Player {player.Name} failed prerequisite AND check ({prerequisiteId}) {type}, {comparison}, {entry.Value[i]}, {entry.ObjectId[i]}");
                    return false;
                }
            }

            return true;
        }

        private bool MeetsEvaluateOr(IPlayer player, uint prerequisiteId, PrerequisiteEntry entry, IPrerequisiteParameters parameters)
        {
            for (int i = 0; i < entry.PrerequisiteTypeId.Length; i++)
            {
                PrerequisiteType type = entry.PrerequisiteTypeId[i];
                if (type == PrerequisiteType.None)
                    continue;

                if (Meets(player, type, entry.PrerequisiteComparisonId[i], entry.Value[i], entry.ObjectId[i], parameters))
                    return true;
            }

            log.LogTrace($"Player {player.Name} failed prerequisite OR check ({prerequisiteId})");
            return false;
        }

        private bool Meets(IPlayer player, PrerequisiteType type, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
        {
            IPrerequisiteCheck handler = serviceProvider.GetKeyedService<IPrerequisiteCheck>(type);
            if (handler == null)
            {
                log.LogWarning($"Unhandled PrerequisiteType {type}!");
                return parameters?.UnhandledResult ?? false;
            }

            return handler.Meets(player, comparison, value, objectId, parameters);
        }
    }
}
