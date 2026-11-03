using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Spell;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Spell;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Spell;

namespace NexusForever.WorldServer.Network.Message.Handler.Spell
{
    /// <summary>
    /// A quest ability: the client casts the spell a SpellSuccess objective names from the quest tracker (Rebuild Helix,
    /// Use Augmented Remains) without the spell being in the ability book (SEMANTICS: quest abilities).
    /// </summary>
    public class ClientCastObjectiveAbilityHandler : IMessageHandler<IWorldSession, ClientCastObjectiveAbility>
    {
        public void HandleMessage(IWorldSession session, ClientCastObjectiveAbility cast)
        {
            IPlayer player = session.Player;
            bool granted = player.QuestManager.GetActiveQuests()
                .Where(quest => quest.State == QuestState.Accepted)
                .SelectMany(quest => quest)
                .Any(objective => !objective.IsComplete()
                    && objective.ObjectiveInfo.Entry.Data == cast.Spell4Id
                    && (QuestObjectiveType)objective.ObjectiveInfo.Entry.Type is QuestObjectiveType.SpellSuccess
                        or QuestObjectiveType.SpellSuccess2 or QuestObjectiveType.SpellSuccess3 or QuestObjectiveType.SpellSuccess4);
            if (!granted)
            {
                // the tracker only shows the ability while such an objective is open; anything else is refused
                session.EnqueueMessageEncrypted(new ServerSpellCastResult
                {
                    ClientSpellCastUniqueId = cast.ClientSpellCastUniqueId,
                    Spell4Id                = cast.Spell4Id,
                    CastResult              = CastResult.PrereqCasterCast
                });
                return;
            }

            player.CastSpell(cast.Spell4Id, new SpellParameters
            {
                PrimaryTargetId        = cast.TargetUnitId,
                TargetPosition         = cast.Position,
                UserInitiatedSpellCast = true
            });
        }
    }
}
