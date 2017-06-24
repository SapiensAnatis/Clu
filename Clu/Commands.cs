using Discord;
using Discord.Commands;
using Discord.WebSocket;

using System;
using System.Threading.Tasks;


public class CoreCommandModule : ModuleBase
{
    [Command("uptime"), Summary("Find out how long the bot's current session has been running for")]
    public async Task GetUptime()
    {
        void RemoveSubstrings(ref string Original, params string[] Substrings) {
            foreach (string Substring in Substrings) {
                Original = Original.Replace(Substring, "");
            }
        } // Internal function to strip 0hrs, 0days, etc.

        // It's internal because I have no need for it elsewhere. Only accessible from this
        // func. New in C# 7.0 :)
        
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
        // We can only go back to 1970 in theory so decades is all we need
        // The years and decades thing is a joke btw
        // Now get to actual years/decades/etc
        int Decades = TotalDecades;
        int Years = TotalYears - Decades*10;
        int Months = TotalMonths - TotalYears*12;
        // According to Google
        int Weeks = TotalWeeks - (int)(TotalDecades*4.34524);

        if (Uptime.TotalSeconds < 1) { UptimeString = "not very long at all"; }
        else { UptimeString = $"{Decades} decades, " +
                              $"{Years} years, " +
                              $"{Months} months, " +
                              $"{Weeks} weeks, " +
                              $"{Uptime.Days - TotalWeeks*7} days, " +
                              $"{Uptime.Hours} hours, " +
                              $"{Uptime.Minutes} minutes " +
                              $"and {Uptime.Seconds} seconds"; }

        RemoveSubstrings(ref UptimeString,
                        "0 decades", "0 years", "0 months", "0 weeks", 
                        "0 days,", "0 hours", "0 minutes and");

        /* Okay, look, I know this code is really, really ugly. However, I was looking at replacing various
           string concatenations with StringBuilders, and many people say that the concat approach is faster
           if, and only if, the number of strings is known. In every case I've done so far (there's similar
           code in ExtraCommands.cs for making an API request) there have been a known number of concatenations.
           
           This is something to do with the fact that it gets optimized into a Concat call at runtime, which I think
           is uglier than the plus ops, so I won't do it myself, but is much faster than a StringBuilder. */
           
        await ReplyAsync($"I've been running for {UptimeString}.");

    }
}