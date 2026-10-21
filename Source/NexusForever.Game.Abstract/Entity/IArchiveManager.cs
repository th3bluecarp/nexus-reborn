using NexusForever.Database.Character;

namespace NexusForever.Game.Abstract.Entity
{
    public interface IArchiveManager : IDatabaseCharacter
    {
        void SendInitialPackets();

        /// <summary>
        /// The client unlocked an article itself (interacting with a creature that unlocks it, an archive link).
        /// </summary>
        void ClientUnlock(uint articleId);

        /// <summary>
        /// The player read an article.
        /// </summary>
        void Viewed(uint articleId);

        /// <summary>
        /// Unlock the article entries whose unlock rule is the completion of the supplied quest.
        /// </summary>
        void OnQuestComplete(uint questId);
    }
}
