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

        RemoveSubstring(ref UptimeString, "0 decades, ");
        RemoveSubstring(ref UptimeString, "0 years, ");
        RemoveSubstring(ref UptimeString, "0 months, ");
        RemoveSubstring(ref UptimeString, "0 weeks, ");
        RemoveSubstring(ref UptimeString, "0 days, ");
        RemoveSubstring(ref UptimeString, "0 hours, ");
        RemoveSubstring(ref UptimeString, "0 minutes and "); // Remove redundant time periods

        await ReplyAsync($"I've been running for {UptimeString}.");
        

        
    }
}