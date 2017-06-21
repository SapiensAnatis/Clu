
using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Clu
{
    class Program // The main components to get the bot running
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();
        
        public async Task MainAsync()
        {
            string Token = RetrieveToken();
            
            var Client = new DiscordSocketClient();
            Client.Log += Log;

            await Client.LoginAsync(TokenType.Bot, Token);
            await Client.StartAsync();

            // Prevent the program exiting
            await Task.Delay(-1);
        }

        private Task Log(LogMessage Message)
        {
            Console.WriteLine(Message.ToString());
            return Task.CompletedTask;
            // TODO: Severities (overload for regular function, or default param?)
        }
        
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
            
            Console.WriteLine($"Got token: \"{Token}\"");
            return Token;
        }
    }
}