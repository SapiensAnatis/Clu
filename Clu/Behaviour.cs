using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

namespace Clu 
{
    class Behaviour
    {
        public static IDiscordClient Client { get; set; }
        
        public Behaviour(IDiscordClient _Client) 
        {
            Client = _Client;            
        }

        public async Task OnJoinedGuild(SocketGuild g)
        {
            await VoiceChannelNaming.UpdateGuildVoiceChannels(g);
        }

        public async Task OnStartup()
        {
            await VoiceChannelNaming.UpdateAllVoiceChannels();
        }

        public async Task HandleUserUpdated(SocketGuildUser Before, SocketGuildUser After)
        {
            // If it wasn't a game update, we don't care
            if (Before.Game.Equals(After.Game)) { return; }
            // If the user isn't in a voice channel, we don't care
            if (After.VoiceChannel == null) { return; }
            // Now that we've ascertained it was a game change of a user within a voice channel:
            await VoiceChannelNaming.UpdateVoiceChannel(After.VoiceChannel);
        }

        public async Task HandleUserVoiceStateUpdated(SocketUser User, SocketVoiceState Before, SocketVoiceState After)
        {
            if (Before.VoiceChannel != null)
                await VoiceChannelNaming.UpdateVoiceChannel(Before.VoiceChannel);
            if (After.VoiceChannel != null)
                await VoiceChannelNaming.UpdateVoiceChannel(After.VoiceChannel);
        }   

        /* This subclass names voice channels after what the majority of occupants have set
         * as their 'Playing' status - to inform users what's going on in the channel. */
         
        static class VoiceChannelNaming
        {
            public static async Task UpdateAllVoiceChannels()
            {
                var Guilds = await Client.GetGuildsAsync();
                foreach (IGuild g in Guilds) {
                    await UpdateGuildVoiceChannels(g);
                }
            }

            public static async Task UpdateGuildVoiceChannels(IGuild Guild)
            {
                var VoiceChannels = await Guild.GetVoiceChannelsAsync();
                foreach (IVoiceChannel v in VoiceChannels) {
                    await UpdateVoiceChannel(v);
                }
            }

            public static async Task UpdateVoiceChannel(IVoiceChannel Channel)
            {
                if (Channel.Id == Channel.Guild.AFKChannelId)
                    return;
                string NewName = GetVoiceChannelName(await Channel.GetUsersAsync().Flatten());
                try {
                    await Channel.ModifyAsync(x=> x.Name = NewName);
                } 
                catch (Exception e) { 
                    AuxillaryLogger.Log(LogSeverity.Error, "VoiceChannelRename", 
                    $"Failed to change the voice channel's name to: {NewName} ({e.ToString()}: {e.Message})");
                }
            }

            // Get the most popular game being played and return its name as a string
            // Requires users rather than a channel, to avoid making is async
            private static string GetVoiceChannelName(IEnumerable<IGuildUser> Users)
            { 
                if (Users.Count() == 0) { return "General"; }

                var UsersPlayingGames = Users.Where(x => x.Game.HasValue);
                if (UsersPlayingGames.Count() == 0 ) { return "General"; }

                // Get a list of the games whcih share the top spot for most popular
                var GamesBeingPlayed = UsersPlayingGames.Select(x => (Game)x.Game);

                // Order them by their number of occurences in GamesBeingPlayed
                var MostPopularGamesSorted = GamesBeingPlayed.OrderByDescending(
                    x => GamesBeingPlayed.Count(y => (y.Name == x.Name))
                ) // Filter out games that aren't being played by more than one person
                .Where(x => GamesBeingPlayed.Count(y => (y.Name == x.Name)) > 1)
                .Distinct(); // If there is a popular game, mention it once
                
                // Convert them to a list of non-nullable types
                List<Game> MostPopularGames = MostPopularGamesSorted.ToList();

                if (MostPopularGames.Count() == 1) {
                    string Name = MostPopularGames.First().Name;
                    if (Name.Length >= 100)
                        Name = Name.Substring(0, 99); // 100 char limit
                        // whoever sets their game to this is a dumbass
                    if (Name.Length <= 2)
                        Name += new string(' ', 2); // 2 char min
                        // see above comment about dumbassery
                    
                    return Name;
                } else if (UsersPlayingGames.Count() == 1) { 
                    // Only one user playing a game (everyone else is null or not present)
                    return UsersPlayingGames.First().Game?.Name;
                } else if (MostPopularGames.Count() == 0) {
                    return "General";
                } else {
                    return "Multple games";
                }
            }
        }
    }
}