using NexusForever.Game.Spell;
using NexusForever.Game.Static.Support;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Support;

namespace NexusForever.WorldServer.Network.Message.Handler.Misc
{
    /// <summary>
    /// The stuck menu: recall to the bind point, recall to the house or a free suicide, each a 10 second cast.
    /// </summary>
    public class ClientStuckHandler : IMessageHandler<IWorldSession, ClientStuck>
    {
        public void HandleMessage(IWorldSession session, ClientStuck stuck)
        {
            uint spell4Id = stuck.UnstickingType switch
            {
                UnstickType.RecallTransmat => 49881u,
                UnstickType.RecallHouse    => 49882u,
                UnstickType.FreeSuicide    => 52577u,
                _                          => 0u
            };

            if (spell4Id == 0u)
                return;

            session.Player.CastSpell(spell4Id, new SpellParameters
            {
                PrimaryTargetId        = session.Player.Guid,
                UserInitiatedSpellCast = true
            });
        }
    }
}
