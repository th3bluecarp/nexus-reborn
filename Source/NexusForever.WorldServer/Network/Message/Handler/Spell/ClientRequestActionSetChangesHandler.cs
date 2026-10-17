using System.Collections.Generic;
using System.Linq;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Abilities;

namespace NexusForever.WorldServer.Network.Message.Handler.Spell
{
    public class ClientRequestActionSetChangesHandler : IMessageHandler<IWorldSession, ClientRequestActionSetChanges>
    {
        public void HandleMessage(IWorldSession session, ClientRequestActionSetChanges requestActionSetChanges)
        {
            // TODO: check for client validity, e.g. Level & Spell4TierRequirements

            IActionSet actionSet = session.Player.SpellManager.GetActionSet(requestActionSetChanges.ActionSetIndex);

            List<IActionSetShortcut> shortcuts = actionSet.Actions.ToList();
            for (UILocation i = 0; i < (UILocation)requestActionSetChanges.Actions.Count; i++)
            {
                IActionSetShortcut shortcut = actionSet.GetShortcut(i);
                if (shortcut != null)
                    actionSet.RemoveShortcut(i);

                uint spell4BaseId = requestActionSetChanges.Actions[(int)i];
                if (spell4BaseId != 0u && session.Player.SpellManager.IsSpellUsable(spell4BaseId))
                {
                    IActionSetShortcut existingShortcut = shortcuts.SingleOrDefault(s => s.ObjectId == spell4BaseId);
                    byte tier = existingShortcut?.Tier ?? 1;
                    actionSet.AddShortcut(i, ShortcutType.SpellbookItem, spell4BaseId, tier);
                }
            }

            foreach (ClientRequestActionSetChanges.ActionTier actionTier in requestActionSetChanges.ActionTiers)
                session.Player.SpellManager.UpdateSpell(actionTier.Action, actionTier.Tier, requestActionSetChanges.ActionSetIndex);

            session.EnqueueMessageEncrypted(actionSet.BuildServerActionSet());
            if (requestActionSetChanges.ActionTiers.Count > 0)
                session.Player.SpellManager.SendServerAbilityPoints();

            // only new AMP can be added with this packet, filter out existing ones
            List<ushort> newAmps = requestActionSetChanges.Amps
                .Except(actionSet.Amps
                    .Select(a => (ushort)a.Entry.Id))
                .ToList();

            if (newAmps.Count > 0)
            {
                foreach (ushort id in newAmps)
                {
                    // class, point cost and the required AMP are checked, AMPs locked behind an unlock item are not given
                    EldanAugmentationEntry entry = GameTableManager.Instance.EldanAugmentation.GetEntry(id);
                    if (entry == null
                        || entry.ClassId != (uint)session.Player.Class
                        || entry.PowerCost > actionSet.AmpPoints
                        || entry.Item2IdUnlock != 0u
                        || (entry.EldanAugmentationIdRequired != 0u && actionSet.GetAmp((ushort)entry.EldanAugmentationIdRequired) == null))
                        continue;

                    actionSet.AddAmp(id);
                }

                session.EnqueueMessageEncrypted(actionSet.BuildServerAmpList());
                if (requestActionSetChanges.ActionSetIndex == session.Player.SpellManager.ActiveActionSet)
                    session.Player.SpellManager.SyncAmpPassives();
            }
        }
    }
}
