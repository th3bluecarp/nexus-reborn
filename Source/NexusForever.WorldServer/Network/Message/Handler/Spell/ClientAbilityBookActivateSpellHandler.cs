using System.Linq;
using NexusForever.Game.Prerequisite;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Abilities;

namespace NexusForever.WorldServer.Network.Message.Handler.Spell
{
    /// <summary>
    /// Learn button of the ability trainer (AbilityBook.ActivateSpell with a tier 1 Spell4 id).
    /// </summary>
    /// <remarks>
    /// Class abilities are granted for free when the level is reached (SpellManager.GrantSpells), so the trainer
    /// normally lists nothing. This learns any class ability the character qualifies for but does not know yet,
    /// for example one whose SpellLevel prerequisite was met after the level up. Training is free.
    /// </remarks>
    public class ClientAbilityBookActivateSpellHandler : IMessageHandler<IWorldSession, ClientAbilityBookActivateSpell>
    {
        public void HandleMessage(IWorldSession session, ClientAbilityBookActivateSpell activate)
        {
            if (!activate.Active || session.Player == null)
                return;

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(activate.Spell4Id);
            if (spell4Entry == null)
                return;

            SpellLevelEntry spellLevel = GameTableManager.Instance.SpellLevel.Entries
                .FirstOrDefault(s => s.Spell4Id == activate.Spell4Id && s.ClassId == (byte)session.Player.Class);
            if (spellLevel == null)
                return;

            if (spellLevel.CharacterLevel > session.Player.Level)
            {
                session.Player.SendSystemMessage($"You must be level {spellLevel.CharacterLevel} to learn this ability.");
                return;
            }

            if (spellLevel.PrerequisiteId > 0 && !PrerequisiteManager.Instance.Meets(session.Player, spellLevel.PrerequisiteId))
                return;

            if (session.Player.SpellManager.GetSpell(spell4Entry.Spell4BaseIdBaseSpell) != null)
                return;

            session.Player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell);
        }
    }
}
