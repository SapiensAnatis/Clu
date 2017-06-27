using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Clu 
{
    public class CoreCommandModule : ModuleBase
    {
        public Regex RemoveBadPlurals = new Regex(@"(?<!\d)1 \w*?(?=s)"); 
        // Remove plurals from time expressions where the preceding quantity is equal to one 
        // Matches on '1 days/1 minutes', etc, but only actually matches the '1 day' part so what
        // the substitution is will be immediately obvious. Thanks lookaheads!  
        // The beginning lookbehind is to ensure the tail end of 11 days or 31 days doesn't get caught
        public Regex NoDoubleSpace = new Regex(@"\s{2,}");

        [Command("uptime"), Summary("Find out how long the bot's current session has been running for")]
        public async Task GetUptime()
        {
            void RemoveSubstrings(ref string Original, params string[] Substrings) {
                foreach (string Substring in Substrings) {
                    Original = Original.Replace(Substring, "");
                }
            } // Internal function to strip 0hrs, 0days, etc.
            
            TimeSpan Uptime = Clu.Program.Uptime;
            // Bit of spaghetti here. Could be better, but it's a simple command so not much point making it very fancy
            // In future I might spice it up a bit by including an embed (info card, basically) that also details recent
            // uptime percentages/other statistics.
            string UptimeString = String.Empty;

            // Extra timespan things
            int TotalMonths = (int)(Uptime.TotalDays/30.44); // Bit crap
            int TotalWeeks = (int)(Uptime.TotalDays/7); // Always truncate
            int Months = TotalMonths;
            int Weeks = TotalWeeks - (int)(Uptime.TotalDays/7);

            if (Uptime.TotalSeconds < 1) { UptimeString = "not very long at all"; } 
            // To avoid printing an empty string, in case a particularly fast-fingered user manages this
            else { UptimeString = $"{Months} months, " +
                                $"{Weeks} weeks, " +
                                $"{Uptime.Days - TotalWeeks*7} days, " +
                                $"{Uptime.Hours} hours, " +
                                $"{Uptime.Minutes} minutes " +
                                $"and {Uptime.Seconds} seconds"; }

            // I am informed that concats are better for performance if, and only if, the number of concats is known
            // at compile time. Here, it's known to be 5.
            
            RemoveSubstrings(ref UptimeString,
                            "0 months,", "0 weeks,", 
                            "0 days,", "0 hours,", "0 minutes and");
            
            int i = 0; // Index change tracker
            foreach (Match m in RemoveBadPlurals.Matches(UptimeString))
            {
                string BadPlural = UptimeString.Substring(m.Index-i, m.Length+1);
                UptimeString = UptimeString.Replace(BadPlural, m.Value);
                // It actually matches the correct version of the plural using lookaheads.
                // So the hardest part is finding the original part it took issue with
                i++; // For every s removed, the string shifts left.
            }

            // Also just generally clean up
            UptimeString = NoDoubleSpace.Replace(UptimeString, " ");
            if (UptimeString[0] == ' ') // Remove leading spaces
                UptimeString = UptimeString.Remove(0, 1);
            
            await ReplyAsync($"I've been running for {UptimeString}.");

        }
        
        [Command("avatar"), Summary("Get a given user's avatar")]
        public async Task GetAvatar([Remainder, Summary("The user whose avatar you wish to view")] IUser Target)
        {
            await ReplyAsync(Target.GetAvatarUrl(size: 512));
        }
    }
}