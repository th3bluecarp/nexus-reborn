using System.Collections.Concurrent;
using NexusForever.Game.Abstract.Spell.Target;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Entity;

namespace NexusForever.WorldServer.Network.Message.Handler.Combat
{
    /// <summary>
    /// Stun breakout: the client sends the direction keys a stunned player presses, enough presses end the stun.
    /// </summary>
    /// <remarks>
    /// Retail filled a breakout gauge whose values are not in the client data. Here every press counts and the stun breaks after
    /// <see cref="PressesToBreak"/> presses (guessed).
    /// </remarks>
    public class ClientCCStateStunUpdateHandler : IMessageHandler<IWorldSession, ClientCCStateStunUpdate>
    {
        private const int PressesToBreak = 6;

        // stun effect being broken out of -> presses so far
        private static readonly ConcurrentDictionary<ISpellTargetEffectInfo, int> presses = new();

        public void HandleMessage(IWorldSession session, ClientCCStateStunUpdate packet)
        {
            if (packet.InputPressed == 0)
                return;

            ISpellTargetEffectInfo stun = session.Player.CrowdControlManager.GetCCEffect(CCState.Stun);
            if (stun == null)
                return;

            // stuns that ran out without being broken leave an entry, keep the table small
            if (presses.Count > 256)
                presses.Clear();

            int count = presses.AddOrUpdate(stun, 1, (_, c) => c + 1);
            if (count < PressesToBreak)
                return;

            presses.TryRemove(stun, out _);
            stun.Finish();
        }
    }
}
