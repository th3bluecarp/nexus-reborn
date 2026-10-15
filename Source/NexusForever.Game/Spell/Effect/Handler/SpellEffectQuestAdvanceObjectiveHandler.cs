using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Spell.Effect;
using NexusForever.Game.Abstract.Spell.Effect.Data;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Static.Spell.Effect;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Spell.Effect.Handler
{
    /// <summary>
    /// Advances an objective of an active quest, used by quest items and quest abilities.
    /// </summary>
    /// <remarks>
    /// DataBits00 is the quest id, DataBits01 the objective index and DataBits02 the amount.
    /// When the effect lands on a creature, the casting player gets the credit.
    /// </remarks>
    [SpellEffectHandler(SpellEffectType.QuestAdvanceObjective)]
    public class SpellEffectQuestAdvanceObjectiveHandler : ISpellEffectApplyHandler<ISpellEffectDefaultData>
    {
        #region Dependency Injection

        private readonly IGameTableManager gameTableManager;

        public SpellEffectQuestAdvanceObjectiveHandler(
            IGameTableManager gameTableManager)
        {
            this.gameTableManager = gameTableManager;
        }

        #endregion

        public SpellEffectExecutionResult Apply(ISpellExecutionContext executionContext, IUnitEntity target, ISpellTargetEffectInfo info, ISpellEffectDefaultData data)
        {
            IPlayer player = target as IPlayer ?? executionContext.Spell.Caster as IPlayer;
            if (player == null)
                return SpellEffectExecutionResult.Ok;

            Quest2Entry questEntry = gameTableManager.Quest2.GetEntry(data.DataBits00);
            if (questEntry == null || data.DataBits01 >= questEntry.Objectives.Length)
                return SpellEffectExecutionResult.Ok;

            uint objectiveId = questEntry.Objectives[data.DataBits01];
            if (objectiveId == 0u)
                return SpellEffectExecutionResult.Ok;

            player.QuestManager.ObjectiveUpdate(objectiveId, data.DataBits02 > 0u ? data.DataBits02 : 1u);
            return SpellEffectExecutionResult.Ok;
        }
    }
}
