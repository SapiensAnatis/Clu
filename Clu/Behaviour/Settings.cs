using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Clu
{    

    public partial class Behaviour
    {
        
    }

    // ----------------------------------------------
    //  Settings behaviour (classes, interfaces, etc)
    // ----------------------------------------------

    // Static class to access throughout the bot to determine preferences for behaviour
    public class Settings 
    {
        // Get settings based on identifiers
        // Make lists so we can easily lookup settings based on identifiers
        // I could use a dictionary indexed by KeyValuePairs but I think that's rather confusing and messy.
        // We will use LINQ to search instead - seen in FromIdentifier within the SettingsInstance class.
        private static List<IGuildBotSetting> _AllSettings = new List<IGuildBotSetting>();
        // However, it is appropriate here because each guild should have only one instance, so we can index by that.
        private static Dictionary<IGuild, SettingsInstance> _AllInstances = new Dictionary<IGuild, SettingsInstance>();

        // My design philosophy behind this class + nested class(es) is that other code should never, ever have a handle on
        // a type which isn't immediately able to be interpreted, i.e. a value of a standard type. This is why I have these
        // helper methods to return standard types and access the private material for use in other places, to avoid doing it there.
        private static T GetGuildSetting<T>(IGuild guild, string identifier)
        {
            var Instance = _AllInstances[guild];
            var Setting = Instance.FromIdentifier(identifier);
            return ((IGuildBotSetting<T>)Setting).Value;
        }

        // Need this helper method as SettingsInstance is private & public nested classes are BAD
        public static void MakeSettingsInstance(IGuild guild)
        {
            var _ = new SettingsInstance(guild);
            // Cannot return that which we just made because it's a private class, but this method is public.
        }

        private class SettingsInstance
        {
            // Subroutine to save long lines of code. 
            // In our getters, now all we need to do is cast the returned value of this to our desired type
            public IGuildBotSetting FromIdentifier(string identifier)
                => _AllSettings
                    .Where(s => s.Guild == Guild)
                    .Where(s => s.Identifier == identifier)
                    .First(); // Should only be one element

            private IGuild Guild { get; set; }

            private GuildBotSettingYN _RenameVoiceChannels { 
                get => (GuildBotSettingYN)FromIdentifier("RenameVoiceChannels");
            }
            public bool RenameVoiceChannels { get => _RenameVoiceChannels.Value; }

            private GuildBotSettingYN _AllowExtraCommands {
                get => (GuildBotSettingYN)FromIdentifier("AllowExtraCommands");
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
            ValueData ValueType { get; }
        }

        // This is basically just the interface as a class, which we'll deserialize to. You cannot deserialize to abstract classes/interfaces
        // due to the fact that they can't be instantiated. The idea is to never use or store data as this type for an extended period of time.
        // Once deserialized, ideally send a message to the channel (or look for one), attach it, and cast to IGuildBotSetting ASAP
        private class DeserializedBotSetting : IBotSetting
        {
            public string Identifier { get; }
            public string Description { get; }
            public string ValueTypeString { get; }
            public ValueData ValueType 
                => Utils.StringAsEnum<ValueData>(ValueTypeString);
        }

        /* This interface is a setting which rationalizes a read-from-JSON setting to one that
           actually exists in a guild and has a message. */
        private interface IGuildBotSetting : IBotSetting
        {
            IUserMessage Message { get; }
            IGuild Guild { get; }
        }

        
        // The value could be anything from a list to a bool, so it's generic
        private interface IGuildBotSetting<T> : IGuildBotSetting
        {
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
            // so we make this abstract
            public abstract T Value { get; } 
            public IUserMessage Message { get; protected set; }
            public IGuild Guild => (this.Message.Channel as IGuildChannel)?.Guild;
            public string Identifier { get; protected set; }
            public string Description { get; protected set; }
            
            public string ValueTypeString { get; protected set; }
            public ValueData ValueType 
                => Utils.StringAsEnum<ValueData>(ValueTypeString);
            
            public GuildBotSetting()
                => _AllSettings.Add(this);
        }

        // As an example of a bool GuildBotSetting, here is a bool Y/N setting
        // which uses the reactions to its message as its value:
        private class GuildBotSettingYN : GuildBotSetting<bool> // No need to inherit from IGuildBotSetting as its base class does already
        {
            private bool _Value { get; set; } // Backing field for custom accessor behaviour
            public override bool Value { 
                get => (Message.GetReactionCount("🇾") > 1);
                // There's no actual need for a N emoji because simply removing your reaction from Y can count as 'no'              
            }

            // constructor from setting
            // (can't define casts with interfaces or base classes)
            public GuildBotSettingYN(IBotSetting BaseSetting, IUserMessage _Message) : base()
            {
                if (BaseSetting.ValueType == ValueData.Bool) {
                    this.Identifier = BaseSetting.Identifier;
                    this.Description = BaseSetting.Description;
                    this.ValueTypeString = BaseSetting.ValueTypeString;

                    this.Message = _Message;
                } else {
                    throw new System.ArgumentException("Attempted to make a bool setting from a setting which wasn't of the right type to do so");
                }
            }
        }
    }
    public static partial class Utils
    {
        public static int GetReactionCount(this IUserMessage Message, string e)
            => Message.Reactions[new Emoji(e)].ReactionCount;
        // This was probably just me being lazy, but still I think Message.Reactions[new Emoji("x")].ReactionCount was a bit clunky
        // In cases where I already have an emoji object (e.g. in checking if a reaction is already present before adding one), I won't use this

        public static T StringAsEnum<T>(string Str)
            => (T)Enum.Parse(typeof(T), Str);
        // Yeah, it's only one line, but I think StringAsEnum<ValueData>("Bool") is much more readable than that line of code.
    }
}