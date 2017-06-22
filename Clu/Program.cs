﻿
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Clu
{
    class Program // The main components to get the bot running
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();
        
        private IServiceProvider Services;
        private DiscordSocketClient Client;
        private CommandService Commands;


        public async Task MainAsync()
        {
            string Token = RetrieveToken();
            
            Client = new DiscordSocketClient();
            Client.Log += Log;

            Services = new ServiceCollection().BuildServiceProvider();

            Commands = new CommandService();
            await InitCommands();

            await Client.LoginAsync(TokenType.Bot, Token);
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
                await Context.Channel.SendMessageAsync($"Oops, something went wrong! {result.ErrorReason}");
        }

        // *** Logging
        public Dictionary<LogSeverity, ConsoleColor> LogColors = new Dictionary<LogSeverity, ConsoleColor>() 
        {
            { LogSeverity.Critical, ConsoleColor.DarkRed },
            { LogSeverity.Error, ConsoleColor.Red},
            { LogSeverity.Debug, ConsoleColor.Magenta },
            { LogSeverity.Info, ConsoleColor.Cyan },
            { LogSeverity.Warning, ConsoleColor.Yellow },
            { LogSeverity.Verbose, ConsoleColor.Gray },
        };

        public Dictionary<LogSeverity, string> LogPrefixes = new Dictionary<LogSeverity, string>()
        {
            { LogSeverity.Critical, "CRITICAL"},
            { LogSeverity.Error, "ERROR"}, // Hopefully having those two in caps will make them easier to spot
            { LogSeverity.Debug, "Debug"},
            { LogSeverity.Info, "Info"},
            { LogSeverity.Warning, "Warn"},
            { LogSeverity.Verbose, "Info++"},
        };


        private Task Log(LogMessage Message)
        {
            Console.ForegroundColor = LogColors[Message.Severity];
            Console.Write($"[{LogPrefixes[Message.Severity]}][{Message.Source}] {Message.ToString()}");
            if (Message.Exception != null)
                Console.Write($" ({Message.Exception.ToString()}: {Message.Exception.Message})");
            
            Console.Write("\n");
            // Output e.g. [ERROR][source] Something went wrong
            Console.ResetColor();
            return Task.CompletedTask;
        }

        // *** Token retrieval 

        // Some filepath auto-properties. These are here because the System.IO function calls aren't exactly concise,
        // but are used a lot in making things universal and relative. Saves us some typing
        public string BaseFilepath
        {
            get { return AppContext.BaseDirectory.Substring(0, AppContext.BaseDirectory.IndexOf("bin")); }
            // Get the app directory and trim it up to before bin/ so we get the root dir which this file is in
        }

        public string KeychainPath
        {
            get { return System.IO.Path.Combine(BaseFilepath, "keychain"); }
            // Add on the Keychain folder
        }

        public string FileInKeychain(string filename)
        {
            return System.IO.Path.Combine(KeychainPath, filename);
        }

        private string RetrieveToken()
        {
            string RawToken = System.IO.File.ReadAllLines(FileInKeychain("token.txt"))[0];
            // Clean token
            string Token = RawToken.Replace("\n", "");
            
            Log(new LogMessage(LogSeverity.Info, "Core", $"Got token: \"{Token}\""));
            return Token;
        }
    }
}