
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Clu
{
    class Program // The main components to get the bot running
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();
        
        private IServiceProvider Services;
        private DiscordSocketClient Client;
        private CommandService Commands;

        private static DateTime? StartTime;
        // Make these static so we can access from, say, the command modules
        // (which is, in fact, the _only_ place we're accessing them from at this time)
        public static TimeSpan Uptime => DateTime.UtcNow.Subtract(StartTime ?? DateTime.UtcNow);


        public async Task MainAsync()
        {
            StartTime = DateTime.UtcNow;

            // Get Discord bot token
            Keychain.DiscordBotToken = Keychain.RetrieveFromKeychain("Token.txt");
            // Get Google API key
            Keychain.GoogleAPIKey = Keychain.RetrieveFromKeychain("SearchKey.txt");

            Client = new DiscordSocketClient();
            Client.Log += Log;

            var BehaviourInstance = new Behaviour(Client);
            // Voice channel rename events
            Client.JoinedGuild += BehaviourInstance.VCR_OnJoinedGuild;
            Client.Ready += BehaviourInstance.VCR_OnStartup;
            Client.GuildMemberUpdated += BehaviourInstance.VCR_HandleUserUpdated;
            Client.UserVoiceStateUpdated += BehaviourInstance.VCR_HandleVoiceState;
            // Settings events
            Client.JoinedGuild += BehaviourInstance.Settings_OnJoinedGuild;
            Client.Ready += BehaviourInstance.Settings_OnStartup;

            

            Services = new ServiceCollection().BuildServiceProvider();

            Commands = new CommandService();
            await InitCommands();

            await Client.LoginAsync(TokenType.Bot, Keychain.DiscordBotToken);
            await Client.StartAsync();

            // Prevent the program exiting
            await Task.Delay(-1);
        }

        public async Task InitCommands()
        {
            Client.MessageReceived += HandleCommand;
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task HandleCommand(SocketMessage _Message)
        {
            var Message = _Message as SocketUserMessage;
            if (Message == null) return;

            int ArgStart = 0;
            if (!Message.HasCharPrefix('?', ref ArgStart)) return;

            var Context = new CommandContext(Client, Message);
            var result = await Commands.ExecuteAsync(Context, ArgStart, Services);

            if (!result.IsSuccess)
                await Log(new LogMessage(LogSeverity.Warning, "HandleCommand", 
                $"Encountered an error while trying to parse potential command {Message.Content}: {result.ErrorReason} ({result.Error})"));
        }
        
        private Task Log(LogMessage Message)
        {
            Console.ForegroundColor = AuxillaryLogger.LogColors[Message.Severity];
            Console.Write($"[{AuxillaryLogger.LogPrefixes[Message.Severity]}][{Message.Source}] {Message.ToString()}");
            if (Message.Exception != null)
                Console.Write($" ({Message.Exception.ToString()}: {Message.Exception.Message})");
            
            Console.Write("\n");
            // Output e.g. [ERROR][source] Something went wrong
            Console.ResetColor();
            return Task.CompletedTask;
        }


        
    }

    static class Keychain
    {
        public static string DiscordBotToken { get; set; }
        public static string GoogleAPIKey { get; set; }

        public static string FileInKeychain(string Filename)
        {
            return System.IO.Path.Combine(Utils.BaseFilepath, "keychain", Filename);
        }

        public static string RetrieveFromKeychain(string Filename)
        {
            // Get the first line so we miss any formatting
            try {
                string RawKey = System.IO.File.ReadAllLines(
                    System.IO.Path.Combine(Utils.BaseFilepath, "keychain", Filename)
                )[0];
                // Clean token
                string Key = RawKey.Replace("\n", "");

                return Key;
            } catch(System.IO.FileNotFoundException) { 
                if (Filename == "Token.txt") {
                    // Sorry, but the script is totally useless without a token
                    AuxillaryLogger.Log(new LogMessage(LogSeverity.Critical, "Login", 
                    "Failed to retrieve bot token from token.txt in keychain folder. " +
                    "The script must now exit. Refer to the README to see how to set up " +
                    "a bot and get its user account token."));
                    Utils.Exit();
                } else {
                    // The other API keys are somewhat optional
                    AuxillaryLogger.Log(new LogMessage(LogSeverity.Warning, "Keychain retrieval",
                    $"Failed to retrieve token/API key '{Filename}'. " +
                    "Some functions of the bot may be unavailable as a result."));
                }
                return null; 
            } catch(Exception e) {
                // If don't know what happened, then let the user know.
                // Some exceptions which aren't covered here should be obvious to any user
                // e.g. UnauthorizedAccessException/SecurityException
                AuxillaryLogger.Log(new LogMessage(LogSeverity.Error, "Keychain retrieval",
                $"In attempting to retrieve data from the keychain, an unknown error occured.\n" +
                $"Error details are as follows: \"{e.ToString()} - {e.Message}\""));
                return null;
            }
        }
    }

    // A logger for me to use without messing with the one required by the bot.
    // (in keeping with the Open Closed principle if you want to spin it like that)
    static class AuxillaryLogger
    {
        // *** Logging
        public static Dictionary<LogSeverity, ConsoleColor> LogColors = new Dictionary<LogSeverity, ConsoleColor>() 
        {
            { LogSeverity.Critical, ConsoleColor.DarkRed },
            { LogSeverity.Error, ConsoleColor.Red},
            { LogSeverity.Debug, ConsoleColor.Gray },
            { LogSeverity.Info, ConsoleColor.Cyan },
            { LogSeverity.Warning, ConsoleColor.Yellow },
            { LogSeverity.Verbose, ConsoleColor.Gray },
        };

        public static Dictionary<LogSeverity, string> LogPrefixes = new Dictionary<LogSeverity, string>()
        {
            { LogSeverity.Critical, "CRITICAL"},
            { LogSeverity.Error, "ERROR"}, // Hopefully having those two in caps will make them easier to spot
            { LogSeverity.Debug, "Debug"},
            { LogSeverity.Info, "Info"},
            { LogSeverity.Warning, "Warn"},
            { LogSeverity.Verbose, "Info++"},
        };
        public static void Log(LogMessage Message)
        {
            Console.ForegroundColor = LogColors[Message.Severity];
            Console.Write($"[{LogPrefixes[Message.Severity]}][{Message.Source}] {Message.ToString()}");
            if (Message.Exception != null)
                Console.Write($" ({Message.Exception.ToString()}: {Message.Exception.Message})");
            
            Console.Write("\n");
            // Output e.g. [ERROR][source] Something went wrong
            Console.ResetColor();
        }

        // Construct the LogMessage autonomously, making function calls to log slightly less clunky
        // (by avoiding the new statements and constructors)
        public static void Log(LogSeverity Severity, string Source, string Message, Exception e = null)
        {
            Log(new LogMessage(Severity, Source, Message, e)); 
        }
    }

    public static partial class Utils
    {
        public static void Exit()
        {
            Console.WriteLine("Press Enter to exit...");
            while (Console.ReadKey().Key != ConsoleKey.Enter) {}
            Environment.Exit(0);
        }

        public static string FileInData(string Filename)
        {
            return System.IO.Path.Combine(Utils.BaseFilepath, "data", Filename);
        }

        // Some filepath auto-properties. These are here because the System.IO function calls aren't exactly concise,
        // but are used a lot in making things universal and relative. Saves us some typing
        public static string BaseFilepath => AppContext.BaseDirectory.Substring(0, AppContext.BaseDirectory.IndexOf("bin"));
    }
}