using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

using Discord;
using Discord.Commands;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Clu
{
    public partial class ExtraCommandsModule : ModuleBase
    {
        // Adding a second argument for result count is far too dificult for it to be worth it when
        // the link is at the top :/
        [Command("google"), Summary("Retrieve a handful of the top results from Google for a given search query")]
        public async Task Google([Remainder] string Query)
        {
            // First and foremost check that the command is enabled:
            if (!Settings.GetGuildSetting<bool>(Context.Guild, "AllowExtraCommands"))
                return;

            // Start time. Later in the embed I want to display search time elapsed,
            // this is the first step of being able to do that.
            var SearchStart = DateTime.UtcNow;

            /* Google's CSE API is utilized by querying the following URL:
               https://www.googleapis.com/customsearch/v1?parameters
               You can include such paramaters as (notably) query, API key, custom engine to use,
               count, data filters and (not as notably) heuristics to provide personalized search
               results. We won't be using those as this command is going to be used by all sorts
               of people, in theory. */

            if (String.IsNullOrEmpty(Keychain.GoogleAPIKey)) {
                AuxillaryLogger.Log(new LogMessage(LogSeverity.Error, "ExtraCommands.cs", 
                "A Google query was requested but no API key is present in Clu/keychain/searchkey.txt." +
                " Aborting..."));
                return;
            }

            // Probably the first thing we want to do is anchor in a message that we'll edit later with the embed.
            // Imagine a scenario where a user requests a search and somehow this function hangs for 10 minutes,
            // perhaps due to an API outage, bad connection, some other Web mystery...
            // then once everyone has forgotten about it, the bot posts a random message and nobody has any idea why
            var EmbedMessage = await ReplyAsync("Searching...");

            // Now, we should construct the URL:
            string SearchURL = "https://www.googleapis.com/customsearch/v1" +
                                $"?q={Query}" + // Our query
                                "&cx=011947631902407852034:gq02yx0e1mq" +
                                // cx: A custom search engine I made that searches the whole web, to reduce setup
                                // Don't think you can get in trouble for spam on this, unlike API keys,
                                // so I've left mine in here. If I find out that you can, I'll have to add
                                // some instructions for making your own in the README in keychain/
                                "&prettyPrint=false" +
                                // prettyPrint: Disable indendations/line breaks for increased performance
                                "&fields=items(title,snippet,link)" +
                                // fields: Filter out everything except the results' titles and descriptions.
                                // Increases performance. Currently hardcoded. May change later.
                                $"&num=3" +
                                // num: Limit the number of results to how many the user wants
                                // Defaults to three. Should increase performance.
                                $"&key={Keychain.GoogleAPIKey ?? String.Empty}";
                                // key: And finally, add our API key. It can be null, so I've put this here for safety,
                                // although the above if statement should prevent a search query from getting 
                                // this far without any API key.
                                
            // Prepare ourselves to decompress the gzip which we will request
            // Requesting gzip moves a considerable amount of work from the network
            // and then gives it to the CPU. Since network is usually the bottleneck here,
            // there should be substantial performance benefits.
            HttpClientHandler Handler = new HttpClientHandler()
            { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            string StringResponse = String.Empty; 

            using (var HC = new HttpClient(Handler))
            {
                // Tell Google to send us gzip
                HC.DefaultRequestHeaders.Add("User-Agent", "CluSearch (gzip)");
                HC.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
                
                // Pre-declare so we can use outside of try-catch later
                var Response = await HC.GetAsync(new Uri(SearchURL));

                // Try and read it to a string
                try { 
                    // Ensure everything went OK (in try block for obvious reasons)
                    // Throws HttpRequestException for abnormal status codes
                    Response.EnsureSuccessStatusCode(); 
                    // Get the string content of our response
                    StringResponse = await Response.Content.ReadAsStringAsync();
                }
                catch(HttpRequestException) { 
                    // With a proper web exception we can provide a more appropriate response
                    AuxillaryLogger.Log(
                        new LogMessage(LogSeverity.Error, "ExtraCommands.cs",
                        "The HTTP request to Google's CSE API failed with" +
                        $"status code {Response.StatusCode.ToString()}.")); 
                        throw; // Apparently this maintains the call stack
                } catch(Exception e) {
                    // Else, if we have no idea what happened, just print a generic message
                    AuxillaryLogger.Log(
                        new LogMessage(LogSeverity.Error, "ExtraCommands.cs",
                        "An unknown error occured in querying the Google CSE API.\n" +
                        $"{e.Message}: {e.ToString()} (status: {Response.StatusCode.ToString()}"));
                        throw;
                }
            }

            var JSON = JObject.Parse(StringResponse);
            // Serialize it into our Result class which has fields Title and Snippet
            List<Result> Results = JsonConvert.DeserializeObject<List<Result>>(
                JSON["items"].ToString()
            );

            // Get our elapsed time
            TimeSpan SearchTimeTaken = DateTime.UtcNow.Subtract(SearchStart);

            AuxillaryLogger.Log(new LogMessage(LogSeverity.Debug, "ExtraCommands.cs", "Making embed..."));
            // Generate our embed
            // See https://cdn.discordapp.com/attachments/84319995256905728/252292324967710721/embed.png
            // for what all the fields are and how they end up looking
            var Embed = new EmbedBuilder()
                .WithAuthor(new EmbedAuthorBuilder()
                    // Set the little icon in the top left to be the Google logo
                    .WithIconUrl("http://i.imgur.com/EgnqiMG.png")
                    .WithName($"\"{Query}\"")) // You need a name for the icon to show up :/
                // Add time stats at the bottom
                .WithFooter(new EmbedFooterBuilder()
                    .WithText($"Search completed in {SearchTimeTaken.Milliseconds}ms."))
                // Set the URL that our title will point to as the actual Google results page
                // for the given query, should the user wish to know more.
                // Points to my CSE so as to ensure results are (for the most part) the same.
                .WithUrl("https://cse.google.co.uk/cse/" +
                        "publicurl?cx=011947631902407852034:gq02yx0e1mq" +
                        $"&q={Query.Replace(" ", "%20")}")
                        // We must use %20 rather than spaces, otherwise the embed's URL is considered
                        // to be invalid by Discord, and a BadRequest error will result if we try
                        // and push it through.
                .WithColor(RandomColor);
                    
            foreach (Result r in Results) {
                Embed.AddField(
                    // Add our results to the embed
                    r.Title, 
                    $"({URLPreview(r.Link)}) " + r.Snippet.Replace("\n", "")
                    // And get rid of the newlines in snippets...they're everywhere...seeing one word lines :(
                    // We have wrap anyway
                    // Also have a URL preview to give the results a bit of context
                ); 
            }

            // Finally, edit our message w/ embed:
            await EmbedMessage.ModifyAsync(m => {
                m.Content = string.Empty;
                m.Embed = Embed.Build();
            });
        
        }

        // The red, yellow, green and blue of the Google logo...
        // You can add Colors to embeds, and I couldn't settle on just one for
        // such a colourful company as Google. With none, it looks bland.
        // The only option, therefore, is to randomly select a colour of those four :P
        private static readonly List<Discord.Color> EmbedColors = new List<Discord.Color>
        {
            // Couldn't be bothered to convert from the hex code I had
            new Discord.Color(0xEA, 0x43, 0x35), // Red
            new Discord.Color(0xFB, 0xBC, 0x05), // Yellow
            new Discord.Color(0x34, 0xA8, 0x53), // Green
            new Discord.Color(0x42, 0x85, 0xF4)  // Blue
        };

        private static readonly Random RNG = new Random(); // Need a means to roll it

        public static Discord.Color RandomColor
        {
            get 
            {
                int R = RNG.Next(EmbedColors.Count);
                Discord.Color SelectedColor = EmbedColors[R];
                // If it isn't the same colour, return it, otherwise return a new random colour
                return !IsSameColorAs(SelectedColor, LastColor) ? SelectedColor : RandomColor;
            }
        }

        private static Discord.Color LastColor { get; set; } = new Discord.Color(0, 0, 0);
        // Prevent same colour twice in a row
        // Default colour to always allow the first colour selected
        // (black can never be selected, so the equality always fails)

        private static bool IsSameColorAs(Discord.Color A, Discord.Color B)
            => (A.R == B.R && A.B == B.B && A.G == B.G);
        

        public static string URLPreview(string URL)
        {
            URL = URL.Substring(0, URL.IndexOf('/', 8));
            // Return the URL up to the first slash AFTER http(s)://
            // assuming that they all start with that
            
            // Remove additional fluff
            URL = URL.Replace("//www.", "");
            URL = URL.Replace("/", ""); // Remove slashes
            URL = URL.Replace("http:", ""); // Remove http, https etc
            URL = URL.Replace("https:", "");
            // The above makes it impossible to click on the links, but that was useless anyway,
            // since they would just lead you to the top-level domain: nowhere near the actual
            // result in the vast majority of cases.
            return URL;
        }
    }

    public class Result
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string Link { get; set; }
    }
}