using Discord;

/* Didn't feel like having the client declaration tucked away in the voice channel renaming script.
   This file outlines a key purpose of making behaviour a partial class with ends in all files in this folder:
   it gives easy access to the client. 
   
   May add some more 'global' variables in future. */

namespace Clu
{
    public partial class Behaviour
    {
        private IDiscordClient _Client { get; set; }
            
        // This constructor is how we make an instance and subscribe to events from within our main Program class.
        // It also hands us some control over the client.
        public Behaviour(IDiscordClient client) 
            => this._Client = client;   
    }
}
                 