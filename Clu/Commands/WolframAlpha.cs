using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

using Discord;
using Discord.Commands;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Clu
{
    public partial class ExtraCommandsModule : ModuleBase
    {
        [Command("wolfram"), Summary("Query Wolfram Alpha and display the result as an embed")]
        public async Task Wolfram([Remainder] string Query)
        {
            if (!Settings.GetGuildSetting<bool>(Context.Guild, "AllowExtraCommands"))
                return;

            DateTime StartTime = DateTime.UtcNow;

            var ResponseMessage = await ReplyAsync("Calculating...");
            string Result;

            List<Pod> Pods = new List<Pod>();

            using (var HC = new HttpClient())
            {

                // https://api.wolframalpha.com/v2/query?input=What+is+Google's+share+price&format=image,plaintext&output=JSON&appid=DEMO
                HC.BaseAddress = new Uri("http://api.wolframalpha.com");
                HttpResponseMessage Response = await HC.GetAsync($"/v2/query?appid={Keychain.WolframClientID}&input={Query}&format=image,plaintext&output=JSON");

                Console.WriteLine($"POST: {HC.BaseAddress}v2/result?appid={Keychain.WolframClientID}&i={Query}");
                Console.WriteLine($"RESPONSE: {Response.StatusCode} (status code: {(int)Response.StatusCode})");

                Result = await Response.Content.ReadAsStringAsync();
                var JSON = JObject.Parse(Result);

                Pods = JsonConvert.DeserializeObject<List<Pod>>(
                    JSON["queryresult"]["pods"].ToString()
                );
            }

            // TODO:
            // Fix the formatting for Wolfram Alpha embeds. I think I will follow these rules:
            // Each embed field is a pod. The title of the field is either the title of the pod, or the contents of the foremost subpod.
            // The content of all subpods thereafter are each lines in the field.

            var ResultEmbed = new EmbedBuilder()
            .WithAuthor(new EmbedAuthorBuilder()
                .WithName($"Wolfram Alpha: \"{Query}\"")
                .WithIconUrl("http://i.imgur.com/TxBlgDc.png")
                .WithUrl($"https://www.wolframalpha.com/input/?i={Query.Replace(" ", "%20")}")
            )
            .WithColor(Discord.Color.Red)
            .WithFooter(new EmbedFooterBuilder()
                .WithText(
                    $"Query completed in {(DateTime.UtcNow - StartTime).Milliseconds}ms."
                )
            );
            

            List<Subpod> SubPods = Pods.SelectMany(p => p.subpods).ToList();
            List<EmbedFieldBuilder> Fields = new List<EmbedFieldBuilder>();
            for (int i = 0; i < SubPods.Count; i++)
            {
                var p = SubPods[i];


                if (p.plaintext.Length > 256)
                    p.plaintext = p.plaintext.Substring(0, 256);
                
                // Remove symbols that can cause unwanted formatting
                p.plaintext = p.plaintext.Replace("~~", "~");

                if (String.IsNullOrEmpty(p.title)) {
                    if (String.IsNullOrEmpty(p.plaintext))
                        continue;
                    
                    string CodePoint = "200B";
                    int Code = int.Parse(CodePoint, System.Globalization.NumberStyles.HexNumber);
                    string CharString = char.ConvertFromUtf32(Code);
                    var piped = p.plaintext.Split('|');
                    if (piped.Length > 1) {
                        Fields.Add(new EmbedFieldBuilder()
                            .WithName(piped[0])
                            .WithValue(String.Join("\n", piped.Skip(1)))
                        );
                    } else { 
                        if (Fields.Count > 0)
                            Fields.Last().Value += $"\n{p.plaintext}";
                        else
                            Fields.Add(new EmbedFieldBuilder()
                            .WithName(p.plaintext)
                            .WithValue(CharString)); // Empty field
                    }
                } else {
                    //ResultEmbed.AddField(p.title, p.plaintext);
                }
            }

            foreach (EmbedFieldBuilder f in Fields) {
                ResultEmbed.AddField(f);
            }

            await ResponseMessage.ModifyAsync(m => { m.Embed = ResultEmbed.Build(); m.Content = ""; });
        }

        // Subclasses for deserialization

        public class Img
        {
            public string src { get; set; }
            public string alt { get; set; }
            public string title { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Subpod
        {
            public string title { get; set; }
            public Img img { get; set; }
            public string plaintext { get; set; }
        }

        public class Pod
        {
            public string title { get; set; }
            public string scanner { get; set; }
            public string id { get; set; }
            public int position { get; set; }
            public bool error { get; set; }
            public int numsubpods { get; set; }
            public List<Subpod> subpods { get; set; }
            public bool? primary { get; set; }
        }       
    }
}