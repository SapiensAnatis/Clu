using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace Clu
{
    public partial class ExtraCommandsModule : ModuleBase
    {
        [Command("wolfram"), Summary("Query Wolfram Alpha and display the result as an embed")]
        public async Task Wolfram([Remainder] string Query)
        {
            if (!Settings.GetGuildSetting<bool>(Context.Guild, "AllowExtraCommands"))
                return;

            var ResponseMessage = await ReplyAsync("Calculating...");
            string Result;

            using (var HC = new HttpClient())
            {
                HC.BaseAddress = new Uri("http://api.wolframalpha.com");
                HttpResponseMessage Response = await HC.GetAsync($"/v1/result?appid={Keychain.WolframClientID}&i={Query}");

                Result = await Response.Content.ReadAsStringAsync();
            }

            await ResponseMessage.ModifyAsync(m => m.Content = Result);
        }
    }
}