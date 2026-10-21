using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NexusForever.Database;
using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Character;
using NexusForever.Game.Abstract.Housing;
using NexusForever.GameTable.Text.Filter;
using NexusForever.GameTable.Text.Static;
using NexusForever.Network;
using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model.Pregame;
using NexusForever.Network.World.Message.Static;
using NexusForever.Shared.Game.Events;

namespace NexusForever.WorldServer.Network.Message.Handler.Character
{
    /// <summary>
    /// Rename a character from the character select screen. Retail needed a rename service token, here renaming is free.
    /// </summary>
    /// <remarks>
    /// Characters that own a house are refused: residences are looked up by owner name.
    /// </remarks>
    public class ClientCharacterRenameHandler : IMessageHandler<IWorldSession, ClientCharacterRename>
    {
        #region Dependency Injection

        private readonly ITextFilterManager textFilterManager;
        private readonly IDatabaseManager databaseManager;
        private readonly ICharacterManager characterManager;
        private readonly IGlobalResidenceManager globalResidenceManager;

        public ClientCharacterRenameHandler(
            ITextFilterManager textFilterManager,
            IDatabaseManager databaseManager,
            ICharacterManager characterManager,
            IGlobalResidenceManager globalResidenceManager)
        {
            this.textFilterManager      = textFilterManager;
            this.databaseManager        = databaseManager;
            this.characterManager       = characterManager;
            this.globalResidenceManager = globalResidenceManager;
        }

        #endregion

        public void HandleMessage(IWorldSession session, ClientCharacterRename rename)
        {
            if (session.IsQueued == true)
                throw new InvalidPacketValueException();

            CharacterModel character = session.Characters.FirstOrDefault(c => c.Id == rename.CharacterId);
            string name = rename.Name?.Trim() ?? "";

            CharacterModifyResult GetResult()
            {
                if (character == null)
                    return CharacterModifyResult.RenameFailed;

                if (!textFilterManager.IsTextValid(name)
                    || !textFilterManager.IsTextValid(name, UserText.CharacterName))
                    return CharacterModifyResult.RenameFailed_InvalidName;

                foreach (string part in name.Split(' '))
                    if (!textFilterManager.IsTextValid(part, UserText.CharacterNamePart))
                        return CharacterModifyResult.RenameFailed_InvalidName;

                if (databaseManager.GetDatabase<CharacterDatabase>().CharacterNameExists(name))
                    return CharacterModifyResult.RenameFailed_UniqueName;

                if (globalResidenceManager.GetResidenceByOwner(character.Name) != null)
                    return CharacterModifyResult.RenameFailed;

                return CharacterModifyResult.RenameOk;
            }

            CharacterModifyResult result = GetResult();
            if (result != CharacterModifyResult.RenameOk)
            {
                session.EnqueueMessageEncrypted(new ServerCharacterRenameResult
                {
                    Result      = result,
                    CharacterId = rename.CharacterId,
                    Name        = name
                });
                return;
            }

            session.CanProcessIncomingPackets = false;
            string oldName = character.Name;

            void Save(CharacterContext context)
            {
                var model = new CharacterModel
                {
                    Id = character.Id
                };

                EntityEntry<CharacterModel> entity = context.Attach(model);
                model.Name = name;
                entity.Property(e => e.Name).IsModified = true;
            }

            session.Events.EnqueueEvent(new TaskEvent(databaseManager.GetDatabase<CharacterDatabase>().Save(Save),
                () =>
            {
                session.CanProcessIncomingPackets = true;

                character.Name = name;
                characterManager.DeleteCharacter(character.Id, oldName);
                try
                {
                    characterManager.AddCharacter(character);
                }
                catch (ArgumentException)
                {
                    // another character took the name at the same moment, the database already has it, the cache keeps the id only
                }

                session.EnqueueMessageEncrypted(new ServerCharacterRenameResult
                {
                    Result      = CharacterModifyResult.RenameOk,
                    CharacterId = character.Id,
                    Name        = name
                });
            }));
        }
    }
}
