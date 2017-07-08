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
                await Settings.InitializeGuild(Guild); // The initialize guild function does not only exist for joining guilds, but also loads instances of settings into memory.
        }

        public async Task Settings_OnJoinedGuild(SocketGuild guild)
        {
            await Settings.InitializeGuild(guild);
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

        public static async Task InitializeGuild(SocketGuild guild)
        {
            // Helper function to setup guild; didn't want to make LoadSettings() async
            MakeSettingsInstance(guild);
            var SettingsChannel = guild.TextChannels.Where(c => c.Name == "clu-bot-settings").FirstOrDefault();
            if (SettingsChannel == null) {
                await guild.CreateTextChannelAsync("clu-bot-settings"); 
                // Don't seem to be able to convert that RestTextChannel into SocketTextChannel.. 
                SettingsChannel = guild.TextChannels.Where(c => c.Name == "clu-bot-settings").First();
            }

            // Don't await getting the message every time in the function call in the loop, get it once and for all
            var Messages = await SettingsChannel.GetMessagesAsync().Flatten();

            foreach (DeserializedBotSetting Setting in _BaseSettings) {
                if (!SettingInChannel(Setting, Messages)) {
                    RestUserMessage SettingsMessage = await SettingsChannel.SendMessageAsync(Setting.Description);
                    // Add relevant reactions
                    await SettingsMessage.AddReactionAsync(new Emoji("✔️"));
                    await SettingsMessage.AddReactionAsync(new Emoji("❌"));
                    // RestMessage inherits from IUserMessage, so we can now get to a real type
                    // No method for this as the resultant type will vary...
                    switch (Setting.ValueType)
                    {
                        case ValueData.Bool:
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
                }
            }
        }

        // In initializing new guilds, refer to this list to post messages
        private static List<DeserializedBotSetting> _BaseSettings = new List<DeserializedBotSetting>();
        // Make lists so we can easily lookup settings based on identifiers
        // I could use a dictionary indexed by KeyValuePairs but I think that's rather confusing and messy.
        // We will use LINQ to search instead - seen in FromIdentifier within the SettingsInstance class.
        private static List<IGuildBotSetting> _AllSettings = new List<IGuildBotSetting>();
        // However, it is appropriate here because each guild should have only one instance, so we can index by that.
        private static Dictionary<IGuild, SettingsInstance> _AllInstances = new Dictionary<IGuild, SettingsInstance>();

        // My design philosophy behind this class + nested class(es) is that other code should never, ever have a handle on
        // a type which I've created here, like IGuildBotSEttings. Types returned should be data and only data. This is why I have these
        // helper methods to return standard types and access the private material for use in other places.
        private static T GetGuildSetting<T>(IGuild guild, string identifier)
        {
            var Instance = _AllInstances[guild];
            var Setting = Instance.FromIdentifier(identifier);
            return ((IGuildBotSetting<T>)Setting).Value;
        }

        // Need this helper method as SettingsInstance is private & public nested classes are BAD
        private static void MakeSettingsInstance(IGuild guild)
        {
            if (!_AllInstances.ContainsKey(guild))
                new SettingsInstance(guild);
            // Cannot return that which we just made because it's a private class, but this method is public.
            // The constructor will add it to the instances list anyway
        }

        private static bool SettingInChannel(DeserializedBotSetting setting, IEnumerable<IMessage> messages) 
        {
            foreach (IMessage m in messages) {
                // It's 'contains' because for more complex data (e.g. list of roles) which can't be shown by reactions
                // the data will probably end up being added to the body of the settings message.
                if (m.Content.Contains(setting.Description))
                    return true;
            }

            return false;
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
                => _AllSettings
                    .Where(s => s.Guild == Guild)
                    .Where(s => s.Identifier == identifier)
                    .First(); // Should only be one element

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
            public string Identifier { get; private set; } // Must at least have a private set in order for Json.net to deserialize right
            public string Description { get; private set; }
            public string ValueTypeString { get; private set; }
            public string DefaultValueStr { get; private set; }
            public ValueData ValueType 
                => Utils.StringAsEnum<ValueData>(ValueTypeString);
        }

        /* This interface is a setting which rationalizes a read-from-JSON setting to one that
           actually exists in a guild and has a message. */
        private interface IGuildBotSetting : IBotSetting
        {
            // The SendMessage (the method we encapsulate the channel messages with) returns RestMessage
            IUserMessage Message { get; }
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
           I would have to store IDs and rework the whole class structure to allow for a value from the start. If
           stored as IDs, I would then have to implement more operations to get from IDs to IUser or IRole objects.
           
           It also means that the bot is less centralized - a user can be sure of what a setting's value is by looking
           at their own server - even if they don't have access to the running environment of the instance of the bot
           which they are using.
           
           My system is a bit crap as well, but I think it's better. If it doesn't work, you won't see this comment
           as I'll probably have rewritten this entirely; it's fundamentally incompatible with storing values in JSON. */


        // Abstract class to save me some typing
        // Would make it private but it must be public to be base for a public class
        private abstract class GuildBotSetting<T> : IGuildBotSetting<T>
        {
            // Each derived class is going to have its own way of determining its value
            // so we make this abstract. In line with my reasoning above, there shall be no set accessor
            public abstract T Value { get; } 
            public IUserMessage Message { get; protected set; }
            public IGuild Guild => (this.Message.Channel as IGuildChannel)?.Guild; // Seriously if I assign a DM message as a settings message, something went horribly wrong
            public string Identifier { get; protected set; }
            public string Description { get; protected set; }
            
            public string ValueTypeString { get; protected set; }
            public ValueData ValueType 
                => Utils.StringAsEnum<ValueData>(ValueTypeString);
            public string DefaultValueStr { get; protected set; }
            public virtual T DefaultValue
            {
                get {
                    T Result; // predeclare so we can return afterward

                    // Conversion attempt. Should never fail if class was typed properly. But just in case...
                    // I mean, I hope I specified a parseable result in each field of my JSON.
                    // If it goes wrong, it's 100% my fault (assuming the user didn't change the JSON in any way, which is a safe one since they have no reason to,
                    // unless they are extending the bot in a major way).
                    try {
                        Result =  (T)Convert.ChangeType(ValueTypeString, typeof(T)); 
                    } catch { 
                        AuxillaryLogger.Log(LogSeverity.Error, "Settings.cs", $"Failed to convert default value str {DefaultValueStr} to type {typeof(T)}!"); 
                        Result = default(T);
                    }

                    return Result;
                }
            }
                
            public GuildBotSetting()
                => _AllSettings.Add(this);
        }

        // As an example of a bool GuildBotSetting, here is a bool Y/N setting
        // which uses the reactions to its message as its value:
        private class GuildBotSettingYN : GuildBotSetting<bool> // No need to inherit from IGuildBotSetting as its base class does already
        {
            private bool _Value { get; set; } // Backing field for custom accessor behaviour
            public override bool Value { 
                get
                {
                    // I previously just had one Y emoji for yes or no, but the cross and tick look nicer and allow for default values
                    if (this.Message.GetReactionCount("✔️") > 1) { return true; }
                    else if (this.Message.GetReactionCount("❌") > 1) { return false; }
                    else { return this.DefaultValue; }  // If ambivalent, return default value
                }        
            }

            // Simple subroutine for absorbing a base setting and taking its common properties.
            // Not appropriate for a cast since an IBotSetting requires additional data to be turned into this
            // (i.e. a Message object)
            private void InheritProperties(IBotSetting BaseSetting)
            {
                this.Identifier = BaseSetting.Identifier;
                this.Description = BaseSetting.Description;
                this.ValueTypeString = BaseSetting.ValueTypeString;
            }

            // constructor from setting
            // (can't define casts with interfaces or base classes)
            public GuildBotSettingYN(IBotSetting BaseSetting, IUserMessage _Message)
            {
                if (BaseSetting.ValueType != ValueData.Bool)
                    throw new ArgumentException("Attempted to create a bool setting from an IBotSetting which wasn't of the right type");
                
                InheritProperties(BaseSetting);

                this.Message = _Message;
              
            }

            public GuildBotSettingYN(IGuildBotSetting BaseSetting)
            {
                // Clean list of the old settings, which would have this identifier and could mess with lookups
                if (BaseSetting.ValueType != ValueData.Bool)
                    throw new ArgumentException("Attempted to create a bool setting from an IBotSetting which wasn't of the right type");
                _AllSettings.RemoveAll(s => (s.Identifier == BaseSetting.Identifier) && s != this);

                InheritProperties(BaseSetting);

                this.Message = BaseSetting.Message;
            }
        }
    }
    public static partial class Utils
    {
        public static int GetReactionCount(this IUserMessage message, string e)
            => message.Reactions[new Emoji(e)].ReactionCount;
        // This was probably just me being lazy, but still I think Message.Reactions[new Emoji("x")].ReactionCount was a bit clunky.
        // Particularly in if statements, of which there are a lot around the Y/N setting area.
        
        // In cases where I already have an emoji object (e.g. in checking if a reaction is already present before adding one), I won't use this

        public static T StringAsEnum<T>(string str)
            => (T)Enum.Parse(typeof(T), str);
        // Yeah, it's only one line, but I think StringAsEnum<ValueData>("Bool") is much more readable than what's inside the function.
    }
}