using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Threading.Tasks;


public class CoreCommandModule : ModuleBase
{
    [Command("uptime"), Summary("Find out how long the bot's current session has been running for")]
    {
        void RemoveSubstring(ref string Original, string Substring) {
            Original = Original.Replace(Substring, "");
        } // Internal function to strip 0hrs, 0days, etc.

        // It's internal because I have no need for it elsewhere. Only accessible from this
        // func. New in C# 7.0 :)
        
        TimeSpan Uptime = Clu.Program.Uptime;
        // Bit of spaghetti here. Could be better, but it's a simple command so not much point making it very fancy
        // In future I might spice it up a bit by including an embed (info card, basically) that also details recent
        // uptime percentages/other statistics.
        string UptimeString = String.Empty;
        if (Uptime.TotalSeconds < 1) { UptimeString = "not very long at all"; }
        else { UptimeString = $"{Uptime.Days} days, {Uptime.Hours} hours, {Uptime.Minutes} minutes and {Uptime.Seconds} seconds"; }
        // No weeks method :(

        RemoveSubstring(ref UptimeString, "0 days, ");
        RemoveSubstring(ref UptimeString, "0 hours, ");

        await ReplyAsync($"I've been running for {UptimeString}.");

        
    }
}