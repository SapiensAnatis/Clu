using System;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace Clu
{
    public class ExtraCommandsModule : ModuleBase
    {
        [Command("google"), Summary("Retrieve a handful of the top results from Google for a given search query")]
        public async Task Google(string query)
        {
            throw new NotImplementedException();
        }
    }
}