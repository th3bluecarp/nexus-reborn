using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Challenges;

namespace NexusForever.WorldServer.Network.Message.Handler.Challenge
{
    public class ClientChallengeChoiceHandler : IMessageHandler<IWorldSession, ClientChallengeChoice>
    {
        public void HandleMessage(IWorldSession session, ClientChallengeChoice challengeChoice)
        {
            session.Player.ChallengeManager.HandleChoice(challengeChoice.ChallengeId, challengeChoice.Choice);
        }
    }
}
