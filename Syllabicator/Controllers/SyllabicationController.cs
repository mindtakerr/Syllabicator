using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Syllabicator.Models.Syllabication;
using System.Net;

namespace Syllabicator.Controllers
{
    public class SyllabicationController : Controller
    {
        private readonly Microsoft.AspNetCore.Hosting.IHostingEnvironment _hostingEnvironment;

        public SyllabicationController(Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        /// <summary>
        /// Get syllables from a dictionary with no language specified (so we'll check each until we find it)
        /// </summary>
        /// <param name="word">The word for which to get syllables</param>
        /// <returns></returns>

        public IActionResult Syllables([FromQuery] string word)
        {
            word = word.ToLower().Trim();

            // Try to find the word in dictionaries
            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", "English.json");
            string json = System.IO.File.ReadAllText(jsonPath);
            var Glossary = JsonConvert.DeserializeObject<List<GlossaryItem>>(json);

            var Item = Glossary.SingleOrDefault(x => x.Word == word);
            if (Item != null)
            {
                return Ok(Item);
            }

            return Ok(new GlossaryItem() { Word = word, Syllables = new string[] { word } });
        }

        /// <summary>
        /// Get syllables from a dictionary with the language specified
        /// </summary>
        /// <param name="word">The word for which to get syllables</param>
        /// <param name="lang">The language of the dictionary to check</param>
        /// <returns></returns>
        public IActionResult SyllablesWithLanguage([FromQuery] string word, [FromQuery] string lang)
        {
            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", lang + ".json");
            string json = System.IO.File.ReadAllText(jsonPath);
            var Glossary = JsonConvert.DeserializeObject<List<GlossaryItem>>(json);

            var Item = Glossary.SingleOrDefault(x => x.Word == word);
            if (Item != null)
            {
                return Ok(Item);
            }

            return Ok(new GlossaryItem() { Word = word, Syllables = new string[] { word } });
        }

        private List<string> SplitLine(string UltraStarLine, string lang)
        {
            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", lang + ".json");
            string json = System.IO.File.ReadAllText(jsonPath);
            var Glossary = JsonConvert.DeserializeObject<List<GlossaryItem>>(json);

            if (UltraStarLine.StartsWith(":") || UltraStarLine.StartsWith("*") || UltraStarLine.StartsWith("R") || UltraStarLine.StartsWith("G"))
            {
                // Do Something
                var songLine = new SongLine(UltraStarLine);

                // See if the syllable word is already in our Glossary
                GlossaryItem GItem = Glossary.SingleOrDefault(x => x.Word == songLine.SyllableWord);

                if (GItem == null)
                {
                    if (lang == "English")
                    {
                        // Try to get the syllables from howmanysyllables.com
                        string url = "https://www.howmanysyllables.com/syllables/" + songLine.SyllableWord;
                        string html = new WebClient().DownloadString(url);
                        string SyllableInfoCheckString = "into syllables: &nbsp; <span class=\"Answer_Red\" data-nosnippet>";
                        Thread.Sleep(502); // Sleep so that we don't slam the how many syllables website

                        if (html.Contains(SyllableInfoCheckString))
                        {
                            var parts = html.Replace(SyllableInfoCheckString, "☯").Split('☯');
                            string syllables = parts[1].Split('<')[0];
                            GItem = new GlossaryItem
                            {
                                Word = songLine.SyllableWord,
                                Syllables = syllables.Split('-')
                            };

                            // Can we add this to our glossary?
                        }
                    }
                    else
                    {
                        GItem = new GlossaryItem() { Word = songLine.SyllableWord, Syllables = new string[] { songLine.SyllableWord } };
                    }
                }

                if (GItem.Syllables.Length == 1)
                {
                    return new List<string>() { UltraStarLine };
                }
                else
                {
                    List<string> Output = new List<string>();

                    // Split up the line into syllables and add them all
                    int noteLength = songLine.NoteLength / GItem.Syllables.Length;
                    int noteRemainder = songLine.NoteLength % GItem.Syllables.Length;

                    int currentNote = songLine.StartingTime;
                    for (int i = 0; i < GItem.Syllables.Length; i++)
                    {
                        var NewSongLine = new SongLine
                        {
                            Starter = songLine.Starter,
                            StartingTime = currentNote,
                            NoteLength = noteLength,
                            NoteValue = songLine.NoteValue,
                            Syllable = GItem.Syllables[i]
                        };
                        if (i == 0)
                            NewSongLine.NoteLength += noteRemainder;
                        currentNote += NewSongLine.NoteLength;

                        // If the word starts with a capital letter, then make sure that the output is capitalized
                        if (songLine.IsCapital && i == 0)
                            NewSongLine.Syllable = NewSongLine.Syllable.Substring(0, 1).ToUpper() + NewSongLine.Syllable.Substring(1);

                        // If the line ends in a space, then make sure to add that space to the end
                        if (i == GItem.Syllables.Length - 1 && UltraStarLine.EndsWith(" "))
                            NewSongLine.Syllable += " ";

                        if (songLine.HasLeadingSpace && i == 0)
                            NewSongLine.Syllable = " " + NewSongLine.Syllable;

                        Output.Add(NewSongLine.ToString());
                    }

                    return Output;
                }
            }
            else
            {
                // Just return as is
                return new List<string>() { UltraStarLine };
            }
        }

        [HttpPost]
        public IActionResult SyllabicateUltraStarLine([FromBody] string UltraStarLine, [FromQuery] string lang)
        {
            return Ok(SplitLine(UltraStarLine, lang));
        }
    }
}