using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

namespace Clu 
{
    partial class Behaviour
    {
        // This event handler is called when our bot joins a new guild. In this case, what we want to do
        // is update that guild's voice channel naming as per our script.
        // This is a seperate method to UpdateAllVoiceChannels to save some work.
        // VCR = VoiceChannelRename to reduce ambiguity between event handlers
        public async Task VCR_OnJoinedGuild(SocketGuild g)
            => await VoiceChannelNaming.UpdateGuildVoiceChannels(g);
        

        // When the bot starts, we want to update all voice channels
        public async Task VCR_OnStartup()
            => await VoiceChannelNaming.UpdateAllVoiceChannels(await _Client.GetGuildsAsync());
        


        // This event handler is called whenever a user's state changes, which we use to detect a change
        // in their game which they are listed as playing.
        public async Task VCR_HandleUserUpdated(SocketGuildUser before, SocketGuildUser after)
        {
            // If it wasn't a game update, we don't care
            if (before.Game.Equals(after.Game)) { return; }
            // If the user isn't in a voice channel, we don't care
            if (after.VoiceChannel == null) { return; }
            // Now that we've ascertained it was a game change of a user within a voice channel:
            await VoiceChannelNaming.UpdateVoiceChannel(after.VoiceChannel);
        }

        // This event handler is used to determine when a user has left or joined a voice channel.
        // It also fires whenever they mute and unmute themselves.
        // TODO: exclude voice state changing (mute/deafen) 
        public async Task VCR_HandleVoiceState(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            // If the UserVoiceState was not updated due to a change in voice channel, we have nothing to do.
            if (before.VoiceChannel.Equals(after.VoiceChannel))
                return;

            if (before.VoiceChannel != null)
                await VoiceChannelNaming.UpdateVoiceChannel(before.VoiceChannel);
            if (after.VoiceChannel != null)
                await VoiceChannelNaming.UpdateVoiceChannel(after.VoiceChannel);
        }   

        /* This subclass names voice channels after what the majority of occupants have set
         * as their 'Playing' status - to inform users what's going on in the channel. */

        private static class VoiceChannelNaming
        {
            // I built these methods on each other; as you saw above different events call for a different
            // scale of refreshing. For conveniences sake, I could expose parts of these methods to avoid writing
            // foreach loops in the event handlers.
            public static async Task UpdateAllVoiceChannels(IReadOnlyCollection<IGuild> Guilds)
            {
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

            // The actual code which connects our name determinant function to renaming the channel.
            // I didn't want to make the function which determines the name async, as it's more of a utility,
            // so all of the async work is done here.
            public static async Task UpdateVoiceChannel(IVoiceChannel Channel)
            {
                // If the setting is off - don't bother
                if (!Settings.GetGuildSetting<bool>(Channel.Guild, "RenameVoiceChannels"))
                    return;
                // Don't mess with the AFK channel. Nobody plays games in it anyway...hence AFK
                if (Channel.Id == Channel.Guild.AFKChannelId)
                    return;
                string NewName = GetVoiceChannelName(await Channel.GetUsersAsync().Flatten());
                try {
                    await Channel.ModifyAsync(x=> x.Name = NewName);
                } 
                catch (Exception e) { 
                    // If my checks haven't caught someone trying to (most likely) change the voice channel name to something
                    // that it shouldn't be, then we can handle that to avoid crashing the bot
                    AuxillaryLogger.Log(LogSeverity.Error, "VoiceChannelRename", 
                    $"Failed to change the voice channel's name to: {NewName} ({e.ToString()}: {e.Message})");
                }
            }

            // Get the most popular game being played and return its name as a string
            // Requires users rather than a channel, to avoid making is async (getting users from a channel is an async op)
            private static string GetVoiceChannelName(IEnumerable<IGuildUser> Users)
            { 
                if (!Users.Any()) { return "General"; }

                var UsersPlayingGames = Users.Where(x => x.Game.HasValue);
                if (!UsersPlayingGames.Any()) { return "General"; }

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
                } else if (!MostPopularGames.Any()) {
                    return "General";
                } else {
                    return "Multple games";
                }
            }
        }
    }
}