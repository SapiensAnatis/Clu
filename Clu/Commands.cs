using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System.Threading.Tasks;

public class CoreCommandModule : ModuleBase
{
    [Command("ping"), Summary("pong???")]
    public async Task Ping()
    {
        await ReplyAsync("Pong!");
    }
}