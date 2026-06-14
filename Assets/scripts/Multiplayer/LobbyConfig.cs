using System;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// Configuration for creating a lobby with game-specific settings.
    /// Used during creation and displayed in waiting room.
    /// </summary>
    public class LobbyConfig
    {
        public string GameName { get; set; }
        public int MaxPlayers { get; set; } = 4;
        public string GameVariant { get; set; } = "Classic";
        public bool IsPrivate { get; set; } = false;
        public string Password { get; set; } = "";

        public LobbyConfig()
        {
        }

        public LobbyConfig(string gameName, int maxPlayers = 4, string gameVariant = "Classic", bool isPrivate = false, string password = "")
        {
            GameName = gameName;
            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4);
            GameVariant = gameVariant;
            IsPrivate = isPrivate;
            Password = password;
        }

        public bool IsValid()
        {
            // Keep lobby creation constraints aligned with the UI controls.
            return !string.IsNullOrWhiteSpace(GameName) && 
                   GameName.Length >= 1 && GameName.Length <= 32 &&
                   MaxPlayers >= 2 && MaxPlayers <= 4 &&
                   (!IsPrivate || !string.IsNullOrEmpty(Password));
        }

        public override string ToString()
        {
            return $"[{GameName}] {MaxPlayers}p | Variant: {GameVariant} | Private: {IsPrivate}";
        }
    }
}
