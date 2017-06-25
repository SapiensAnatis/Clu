using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

public class CoreCommandModule : ModuleBase
{
    public static Regex RemoveBadPlurals = new Regex(@"(?<!\d)1 \w*?(?=s)"); 
    // Remove plurals from time expressions where the preceding quantity is equal to one 
    // Matches on '1 days/1 minutes', etc, but only actually matches the '1 day' part so what
    // the substitution is will be immediately obvious. Thanks lookaheads!  
    // The beginning lookbehind is to ensure the tail end of 11 days or 31 days doesn't get caught
    public static Regex NoDoubleSpace = new Regex(@"\s{2,}");

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
        int TotalDecades = (int)(Uptime.TotalDays/3652.5);
        int TotalYears = (int)(Uptime.TotalDays/365.25); // Leap years mann
        int TotalMonths = (int)(Uptime.TotalDays/30.44); // Bit crap
        int TotalWeeks = (int)(Uptime.TotalDays/7); // Always truncate
        // The years and decades thing is a joke btw
        // Now get to actual years/decades/etc
        int Decades = TotalDecades;
        int Years = TotalYears - Decades*10;
        int Months = TotalMonths - TotalYears*12;
        // According to Google
        int Weeks = TotalWeeks - (int)(TotalYears*52);

        // It should be noted that this all breaks down with very large timespan values due to the natural innacuracy in
        // for example stating that there are 4.34524 weeks per month. Because the values are nonsensical with some
        // months having more days than others, we approximate.
        // When using the DateTime.MinValue as the startup time, this happens: 
        // "I've been running for 201 decades, 6 years, 3 months, 81 weeks, 5 days, 23 hours, 35 minutes and 1 seconds."
        // Sadly, weeks is most affected because it's the smallest time period without an official method, and I'm bad at this
        // If I use 52 weeks per year I get 382 weeks
        //
        // On the bright side, this is totally immaterial unless you're spoofing the times - uptimes should never ever reach
        // over a year...I will therefore keep 52 weeks a year as it's more readable

        if (Uptime.TotalSeconds < 1) { UptimeString = "not very long at all"; } 
        // To avoid printing an empty string, in case a particularly fast-fingered user manages this
        else { UptimeString = $"{Decades} decades, " +
                              $"{Years} years, " +
                              $"{Months} months, " +
                              $"{Weeks} weeks, " +
                              $"{Uptime.Days - TotalWeeks*7} days, " +
                              $"{Uptime.Hours} hours, " +
                              $"{Uptime.Minutes} minutes " +
                              $"and {Uptime.Seconds} seconds"; }

        /* Okay, look, I know this code is really, really ugly. However, I was looking at replacing various
           string concatenations with StringBuilders, and many people say that the concat approach is faster
           if, and only if, the number of strings is known. In every case I've done so far (there's similar
           code in ExtraCommands.cs for making an API request) there have been a known number of concatenations.
           
           This is something to do with the fact that it gets optimized into a Concat call at runtime, which I think
           is uglier than the plus ops, so I won't do it myself. Either way, the code is faster than a builder. */
        
        RemoveSubstrings(ref UptimeString,
                        "0 decades,", "0 years,", "0 months,", "0 weeks,", 
                        "0 days,", "0 hours,", "0 minutes ");
        
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
}