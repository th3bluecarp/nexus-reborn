using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Pet;

namespace NexusForever.WorldServer.Network.Message.Handler.Pet
{
    /// <summary>
    /// Pet bar stance buttons. A pet unit id of 0 sets the stance of every combat pet the player has.
    /// </summary>
    public class ClientPetSetStanceHandler : IMessageHandler<IWorldSession, ClientPetSetStance>
    {
        public void HandleMessage(IWorldSession session, ClientPetSetStance setStance)
        {
            IPlayer player = session.Player;
            foreach (uint guid in player.GetCombatPets().ToList())
            {
                if (setStance.PetUnitId != 0u && setStance.PetUnitId != guid)
                    continue;

                IUnitEntity pet = player.GetVisible<IUnitEntity>(guid);
                if (pet == null)
                    continue;

                pet.PetStance = setStance.Stance;
                session.EnqueueMessageEncrypted(new ServerPetStanceChanged
                {
                    PetUnitId = guid,
                    Stance    = setStance.Stance
                });
            }
        }
    }
}
