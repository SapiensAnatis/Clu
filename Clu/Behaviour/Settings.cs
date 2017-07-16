using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Clu
{    

    // Settings behvaiour. I could subscribe to the settings methods but then I need to change their arguments. Also for consistency, it's nice to have all
    // delegates in the Behaviour class.

    public partial class Behaviour
    {
        public async Task Settings_OnStartup()
        {
            Settings.LoadSettings();
            
            foreach (SocketGuild Guild in await _Client.GetGuildsAsync())
                await Settings.InitializeGuild(Guild, _Client.CurrentUser as IUser); // The initialize guild function does not only exist for joining guilds, but also loads instances of settings into memory.
        }

        public async Task Settings_OnJoinedGuild(SocketGuild guild)
        {
            await Settings.InitializeGuild(guild, _Client.CurrentUser as IUser);
        }


        /* These handlers are for reaction changes which queue messages to be updated in the relevant settings,
           if the message that was reacted to is determined to be a settings message. We have to update messages
           as the reaction data is cached and cannot be considered reliable otherwise. We can't simply update the
           data before a request is made, because then the request for the value of the setting has to be async,
           which is just stupid. */
        public async Task Settings_OnReactionChange(Cacheable<IUserMessage, ulong> EventData, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            // Hand over to class internals so we have access to data such as settings lists
            await Settings.UpdateSettingMessage(EventData);
        }

        // Different args (no emoji)
        public async Task Settings_OnReactionsCleared(Cacheable<IUserMessage, ulong> EventData, ISocketMessageChannel arg2)
        {
            await Settings.UpdateSettingMessage(EventData);
        }
    }

    // ----------------------------------------------
    //  Settings backend (classes, interfaces, etc)
    // ----------------------------------------------

    // This class is static because outside of its nested Instance class (which is non-static) it's mostly just helper methods/accessors
    public static class Settings 
    {
        public static void LoadSettings()
        {
            // On startup, deserialize the settings from JSON
            string DataFilepath = Utils.FileInData("DefaultSettings.json");
            string RawJSON = System.IO.File.ReadAllText(DataFilepath);
            var JSON = JObject.Parse(RawJSON);

            _BaseSettings = JsonConvert.DeserializeObject<List<DeserializedBotSetting>>(
                JSON["Settings"].ToString()
            );

            // This prepares our list of default settings to post to guilds (if not already there)
            // I don't do the foreach guild initilization here because that requires making this method async,
            // which I want to avoid.
        }

        public static async Task InitializeGuild(SocketGuild guild, IUser botUser)
        {
            AuxillaryLogger.Log(LogSeverity.Info, "Settings", $"Initializing settings for guild {guild.Name}...");
            var StartTime = DateTime.UtcNow;

            // Helper function to setup guild; didn't want to make LoadSettings() async so it's not called from there
            MakeSettingsInstance(guild);
            var SettingsChannel = (ITextChannel)guild.TextChannels.Where(c => c.Name == "clu-bot-settings").FirstOrDefault();
            if (SettingsChannel == null) {
                SettingsChannel = (ITextChannel)await guild.CreateTextChannelAsync("clu-bot-settings"); 
                await SettingsChannel.AddPermissionOverwriteAsync(
                    guild.EveryoneRole, OverwritePermissions.DenyAll(SettingsChannel)
                );
            }

            // Don't await getting the message every time in the function call in the loop, get it once and for all
            var Messages = await SettingsChannel.GetMessagesAsync().Flatten();

            // Also, we can clean out the channel here
            foreach (IMessage m in Messages) {
                if (m.Author.Id != botUser.Id) {
                    await m.DeleteAsync();
                    await Task.Delay(2000);
                }
            }
            
            foreach (DeserializedBotSetting Setting in _BaseSettings) {
                var PossiblyExistingMessage = (IUserMessage)SettingInChannel(Setting, Messages);
                if (PossiblyExistingMessage == null) {
                    // Performance diagnostics and that
                    AuxillaryLogger.Log(LogSeverity.Verbose, "Settings", "\tEncountered missing settings message, posting... (+6 seconds)");
                    var SettingsMessage = await SettingsChannel.SendMessageAsync(
                        Setting.Description + $" (default: {Setting.DefaultValueStr})"
                    );
                    // I've introduced sleep statements at various places to keep the rate-limits at bay...
                    // as it's a setup function, speed isn't of the essence.
                    await Task.Delay(2000);
                    
                    // RestMessage inherits from IUserMessage, so we can now get to a real type
                    // No method for this as the resultant type will vary...
                    switch (Setting.ValueType)
                    {
                        case ValueData.Bool:
                            // Add relevant reactions
                            await SettingsMessage.AddReactionAsync(new Emoji("✔"));
                            await Task.Delay(2000);
                            await SettingsMessage.AddReactionAsync(new Emoji("❌"));
                            await Task.Delay(2000);
                            // The constructor will add it to the relevant lists. No need to assign.
                            // In addition, the guild isn't passed because it's already implied by the message
                            new GuildBotSettingYN(Setting, SettingsMessage);
                            break;
                        case ValueData.Role:
                        case ValueData.Roles:
                        case ValueData.User:
                        case ValueData.Users:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentException($"Could not determine what type a message with ValueDataString {Setting.ValueTypeString} should be remade into!");
                    }
                } else {
                    // Here's why it returns an IMessage, not a bool
                    // If already found, but no reactions for some reason, add them
                    var ReactionDataTick = await PossiblyExistingMessage.GetReactionUsersAsync("✔");
                    if (!ReactionDataTick.Any(u => u.Id == botUser.Id)) {
                        AuxillaryLogger.Log(LogSeverity.Verbose, "Settings", "\tEncountered missing bot reaction, adding... (+2 seconds)");
                        await PossiblyExistingMessage.AddReactionAsync(new Emoji("✔")); 
                        await Task.Delay(2000);
                    }

                    var ReactionDataCross = await PossiblyExistingMessage.GetReactionUsersAsync("❌");
                    if (!ReactionDataCross.Any(u => u.Id == botUser.Id)) {
                        AuxillaryLogger.Log(LogSeverity.Verbose, "Settings", "\tEncountered missing bot reaction, adding... (+2 seconds)");
                        await PossiblyExistingMessage.AddReactionAsync(new Emoji("❌")); 
                        await Task.Delay(2000);
                    }
                    // Once corrected, create setting objects
                    switch (Setting.ValueType)
                    {
                        case ValueData.Bool:
                            new GuildBotSettingYN(Setting, PossiblyExistingMessage);
                            break;
                        case ValueData.Role:
                        case ValueData.Roles:
                        case ValueData.User:
                        case ValueData.Users:
                            throw new NotImplementedException();
                        default:
                            throw new ArgumentException($"Could not determine what type a message with ValueDataString {Setting.ValueTypeString} should be remade into!");
                    }
                    
                }
            }
            double TimeDelta = Math.Round((DateTime.UtcNow - StartTime).TotalSeconds, 3);

            AuxillaryLogger.Log(LogSeverity.Info, "Settings", 
                $"Completed settings initialization for guild {guild.Name} in {TimeDelta} seconds."
            );

        }
        
        // Make lists so we can easily lookup settings based on identifiers
        // I could use a dictionary indexed by KeyValuePairs but I think that's rather confusing and messy.
        // We will use LINQ to search instead - seen in FromIdentifier within the SettingsInstance class.
        // In initializing new guilds, refer to this list to post messages
        private static List<DeserializedBotSetting> _BaseSettings = new List<DeserializedBotSetting>();
        
        private static List<IGuildBotSetting> _AllSettings = new List<IGuildBotSetting>();
        // However, it is appropriate here because each guild should have only one instance, so we can index by that.
        private static Dictionary<IGuild, SettingsInstance> _AllInstances = new Dictionary<IGuild, SettingsInstance>();

        // Store all setting message IDs, so we have an easy way to check in ReactionUpdated events if the message that
        // was affected is one we should update, rather than doing a LINQ expression every time someone reacts (which will
        // be very very frequent). We can do some LINQ to get the right objects *after* we confirm we want it.
        
        // Considered making it an array with length = defaultsettings*guildsjoined. However we wouldn't be able to grow
        // this when the bot joined new guilds mid-session.
        private static List<ulong> _SettingMessageIds = new List<ulong>();

        /* My design philosophy behind this class + nested class(es) is that other code should never, ever have a handle on
           a type which I've created here, like IGuildBotSettings. Types returned should be data and only data. This is why I have these
           helper methods to return standard types and access the private material for use in other places. */
        
        public static T GetGuildSetting<T>(IGuild guild, string identifier)
        {
            var Instance = _AllInstances[guild];
            var Setting = Instance.FromIdentifier(identifier);
            if (Setting == null) { return default(T); }
            return ((IGuildBotSetting<T>)Setting).Value;
        }

        public static async Task UpdateSettingMessage(Cacheable<IUserMessage, ulong> Data)
        {
            if (!_SettingMessageIds.Contains(Data.Id)) return; // Not bothered about random reactions

            var RelevantSetting = _AllSettings.Where(s => s.Message.Id == Data.Id).FirstOrDefault();
            RelevantSetting.Message = await Data.DownloadAsync(); // Update message
        }

        // Need this helper method as SettingsInstance is private & public nested classes are BAD
        private static void MakeSettingsInstance(IGuild guild)
        {
            if (!_AllInstances.ContainsKey(guild))
                new SettingsInstance(guild);
            // Cannot return that which we just made because it's a private class, but this method is public.
            // The constructor will add it to the instances list anyway
        }

        private static IMessage SettingInChannel(DeserializedBotSetting setting, IEnumerable<IMessage> messages) 
        {
            foreach (IMessage m in messages) {
                // It's 'contains' because for more complex data (e.g. list of roles) which can't be shown by reactions
                // the data will probably end up being added to the body of the settings message.
                if (m.Content.Contains(setting.Description))
                    return m;
            }

            return null;
        }
        

        private class SettingsInstance
        {
            /*
              Settings instance:
              Every setting is stored as an object and has a matching property, the getter of which redirects to the value
              of the setting.
              
               One is made for every guild and is registered by adding it to a list. These instances can them be retrieved with LINQ */

            // Subroutine to save long lines of code. 
            // In our getters, now all we need to do is cast the returned value of this to our desired type
            public IGuildBotSetting FromIdentifier(string identifier)
            {
                return _AllSettings
                .Where(s => s.Identifier == identifier)
                .FirstOrDefault();
            } 

            private IGuild Guild { get; set; }

            private GuildBotSettingYN _RenameVoiceChannels { 
                get => new GuildBotSettingYN(FromIdentifier("RenameVoiceChannels"));
            }
            public bool RenameVoiceChannels { get => _RenameVoiceChannels.Value; }

            private GuildBotSettingYN _AllowExtraCommands {
                get => new GuildBotSettingYN(FromIdentifier("AllowExtraCommands"));
            }
            public bool AllowExtraCommands { get => _AllowExtraCommands.Value; }

            public SettingsInstance(IGuild guild) 
            {
                Guild = guild;
                _AllInstances.Add(guild, this);
            }
        }

        private enum ValueData 
        {
            Bool, Role, Roles, User, Users, 
        }

        /* bare-minimum interface for a bot setting. Provides requirements for the below class that is deserialized to from JSON */
        private interface IBotSetting
        {   
            string Identifier { get; }
            string Description { get; }
            string ValueTypeString { get; }
            string DefaultValueStr { get; }
            ValueData ValueType { get; }
        }

        // This is basically just the interface as a class, which we'll deserialize to. You cannot deserialize to abstract classes/interfaces
        // due to the fact that they can't be instantiated. The idea is to never use or store data as this type for an extended period of time.
        // Once deserialized, ideally send a message to the channel (or look for one), attach it, and cast to IGuildBotSetting ASAP
        private class DeserializedBotSetting : IBotSetting
        {
            public string Identifier { get; set; } // We don't have a constructor so set must be public
            public string Description { get; set; }
            public string ValueTypeString { get; set; }
            public ValueData ValueType 
                => (ValueData)Enum.Parse(typeof(ValueData), ValueTypeString);
            public string DefaultValueStr { get; set; }
        }

        /* This interface is a setting which rationalizes a read-from-JSON setting to one that
           actually exists in a guild and has a message. */
        private interface IGuildBotSetting : IBotSetting
        {
            // The SendMessage (the method we encapsulate the channel messages with) returns RestMessage
            IUserMessage Message { get; set; }
            IGuild Guild { get; }
        }
        
        // The value could be anything from a list to a bool, so it's generic
        private interface IGuildBotSetting<T> : IGuildBotSetting
        {
            T DefaultValue { get; }
            T Value { get; }
        }

        /* As far as values go, I'm not allowing values to be stored in JSON - they must have a getter which
           reads some data attached or near their respective message (e.g. reactions, or the body of the message
           to gleam mentions of roles/users). This is because keeping track of values if in JSON is very finnicky;
           I would have to work in server individuality and save the values to a string and rework the whole class 
           structure to allow for a value from the start. If they stored like this, I would then have to implement 
           more operations to get from IDs to IUser or IRole objects.
           
           It also means that the bot is less centralized - a user can be sure of what a setting's value is by looking
           at their own server - even if they don't have access to the running environment of the instance of the bot
           which they are using.
           
           My system is a bit crap as well, but I think it's better. If it doesn't work, you won't see this comment
           as I'll probably have rewritten this entirely; it's fundamentally incompatible with storing values in JSON. */


        // Abstract class to save me some typing
        private abstract class GuildBotSetting<T> : IGuildBotSetting<T>
        {
            // Each derived class is going to have its own way of determining its value
            // so we make this abstract. In line with my reasoning above, there shall be no set accessor
            public abstract T Value { get; } 
            public IUserMessage Message { get; set; } // Public set because we need to update it in reactions
            public IGuild Guild { get; private set; }
            public string Identifier { get; protected set; }
            public string Description { get; protected set; }
            
            public string ValueTypeString { get; protected set; }
            public ValueData ValueType 
                => (ValueData)Enum.Parse(typeof(ValueData), ValueTypeString);
            public string DefaultValueStr { get; protected set; }
            public virtual T DefaultValue
            {
                get {
                    T Result; // predeclare so we can return afterward

                    /* Here, we try and convert the string to a value of type T. This isn't as risky as it seems,
                       because all of the default values are pre-specified in the JSON so if it screws up it's entirely
                       my fault. */
                    try {
                        Result =  (T)Convert.ChangeType(DefaultValueStr, typeof(T)); 
                    } catch { 
                        AuxillaryLogger.Log(LogSeverity.Error, "Settings.cs", $"Failed to convert default value str {DefaultValueStr} to type {typeof(T)}!"); 
                        Result = default(T);
                    }

                    return Result;
                }
            }

            protected void InheritProperties(IBotSetting BaseSetting)
            {
                this.Identifier = BaseSetting.Identifier;
                this.Description = BaseSetting.Description;
                this.ValueTypeString = BaseSetting.ValueTypeString;
                this.DefaultValueStr = BaseSetting.DefaultValueStr;
            }
                
            public GuildBotSetting(IBotSetting BaseSetting, IUserMessage _Message)
            {
                InheritProperties(BaseSetting);
                Message = _Message;

                _AllSettings.Add(this); 
                _SettingMessageIds.Add(_Message.Id);
            }
            
        }

        // As an example of a bool GuildBotSetting, here is a bool Y/N setting
        // which uses the reactions to its message as its value:
        private class GuildBotSettingYN : GuildBotSetting<bool> // No need to inherit from IGuildBotSetting as its base class does already
        {
            private bool _Value { get; set; } // Backing field for custom accessor behaviour
            public override bool Value { 
                get
                {
                    int Yeas = this.Message.Reactions[new Emoji("✔")].ReactionCount;
                    int Nays = this.Message.Reactions[new Emoji("❌")].ReactionCount;
                    if (Yeas == Nays) { return this.DefaultValue; }
                    // I also must consider that multiple users can react...the channel is only visible to admins (unless changed) so just do greater than
                    else { return (Yeas > Nays); }
                }        
            }

            // Simple subroutine for absorbing a base setting and taking its common properties.
            // Not appropriate for a cast since an IBotSetting requires additional data to be turned into this
            // (i.e. a Message object)
            

            // constructor from setting
            // (can't define casts with interfaces or base classes)
            public GuildBotSettingYN(IBotSetting BaseSetting, IUserMessage _Message) : base(BaseSetting, _Message)
            {
                if (BaseSetting.ValueType != ValueData.Bool)
                    throw new ArgumentException("Attempted to create a bool setting from an IBotSetting which wasn't of the right type");

                // Base constructor does pretty much everything
            }

            public GuildBotSettingYN(IGuildBotSetting BaseSetting) : base(BaseSetting, BaseSetting.Message)
            {
                // Clean list of the old settings, which would have this identifier and could mess with lookups
                if (BaseSetting.ValueType != ValueData.Bool)
                    throw new ArgumentException("Attempted to create a bool setting from an IBotSetting which wasn't of the right type");
                _AllSettings.RemoveAll(s => (s.Identifier == BaseSetting.Identifier) && s != this);
            }
        }
    }
}