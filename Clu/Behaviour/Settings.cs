using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.WebSocket;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Clu
{    
    // ----------------------------------------------
    //  Settings behaviour (classes, interfaces, etc)
    // ----------------------------------------------

    public enum ValueData 
    {
        Bool, Role, User, Users, Roles
    }

    /* bare-minimum interface for a bot setting. Provides requirements for the below class that is deserialized to from JSON */
    public interface IBotSetting
    {   
        string Identifier { get; }
        string Description { get; }
        string ValueTypeString { get; }
        ValueData ValueType { get; }
    }

    // This is basically just the interface as a class, which we'll deserialize to. You cannot deserialize to abstract classes/interfaces
    // due to the fact that they can't be instantiated. The idea is to never use or store data as this type for an extended period of time.
    // Once deserialized, ideally send a message to the channel (or look for one), attach it, and cast to IGuildBotSetting ASAP
    class DSRBotSetting : IBotSetting
    {
        public string Identifier { get; }
        public string Description { get; }
        public string ValueTypeString { get; }
        public ValueData ValueType 
            => Utils.StringAsEnum<ValueData>(ValueTypeString);
    }

    // This interface outlines a setting which has an actual value and a message attached to it
    // and so is more than just a representation of some JSON
    // First, we need a non-generic one so that we can store them
    public interface IGuildBotSetting : IBotSetting
    {
        IUserMessage Message { get; }
        IGuild Guild { get; }
    }
    
    // Then have one with a value. We can attempt to cast to a particular type of IGuildBotSetting based on ValueData, and then
    // attempt to get a value out of it
    public interface IGuildBotSetting<T> : IGuildBotSetting
    {
        T Value { get; set; }
    }

    // Abstract class to save me some typing
    // Would make it private but it must be public to be base for a public class
    public abstract class GuildBotSetting<T> : IGuildBotSetting<T>
    {
        // Each derived class is going to have its own way of determining its value
        // so we make this abstract
        public abstract T Value { get; set; } 
        public IUserMessage Message { get; protected set; }
        public IGuild Guild => (this.Message.Channel as IGuildChannel)?.Guild;
        public string Identifier { get; protected set; }
        public string Description { get; protected set; }
        
        public string ValueTypeString { get; protected set; }
        public ValueData ValueType 
            => Utils.StringAsEnum<ValueData>(ValueTypeString);
    }

    // As an example of a bool GuildBotSetting, here is a bool Y/N setting
    // which uses the reactions to its message as its value:
    public class GuildBotSettingYN : GuildBotSetting<bool>, IGuildBotSetting<bool>
    {
        private bool _Value { get; set; } // Backing field for custom accessor behaviour
        public override bool Value { 
            get => (Message.GetReactionCount(new Emoji("🇾")) > 1);
            // There's no actual need for a N emoji because simply removing your reaction from Y can count as 'no'              
            set => _Value = value;
        }

        // constructor from setting
        // (can't define casts with interfaces or base classes)
        public GuildBotSettingYN(IBotSetting BaseSetting, IUserMessage _Message)
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
    public static partial class Utils
    {
        public static int GetReactionCount(this IUserMessage Message, Emoji e)
            => Message.Reactions[e].ReactionCount;
        
        public static T StringAsEnum<T>(string Str)
            => (T)Enum.Parse(typeof(T), Str);
        // Yeah, it's only one line, but I think StringAsEnum<ValueData>("Bool") is much more readable than the below line of code.
    }
}