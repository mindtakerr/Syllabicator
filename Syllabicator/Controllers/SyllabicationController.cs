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

        [HttpPost]
        public IActionResult SyllabicateUltraStarFile([FromBody] string UltraStarFile, [FromQuery] string lang)
        {
            List<string> Output = new List<string>();
            var lines = UltraStarFile.Split('\n');

            foreach (var line in lines)
            {
                string tline = line.Trim();
                Output.AddRange(SplitLine(tline, lang));
            }

            CleanGlossary(lang);

            return Ok(Output);
        }

        [HttpPost]
        public IActionResult SyllabicateUltraStarLine([FromBody] string UltraStarLine, [FromQuery] string lang)
        {
            return Ok(SplitLine(UltraStarLine, lang));
        }

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
            var Glossary = LoadGlossary(lang);
            var Item = Glossary.SingleOrDefault(x => x.Word == word);
            if (Item != null)
            {
                return Ok(Item);
            }

            return Ok(new GlossaryItem() { Word = word, Syllables = new string[] { word } });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="UltraStarFile"></param>
        /// <param name="lang"></param>
        /// <param name="beats"></param>
        /// <returns></returns>
        public IActionResult CreateSongSkeleton([FromBody] string UltraStarFile, [FromQuery] string lang, [FromQuery] int beats)
        {
            return Ok(SongSkeleton(UltraStarFile, lang, beats));
        }

        private List<string> SongSkeleton(string UltraStarFile, string lang, int beats)
        {
            List<string> Output = new List<string>();

            var lines = UltraStarFile.Split('\n');
            var Glossary = LoadGlossary(lang);

            int SongCounter = 0;

            foreach (var line in lines)
            {
                string tline = line.Trim();
                var Words = line.Split(' ');
                foreach (var W in Words)
                {
                    // Clean up the word
                    string Word = W.Replace(",", string.Empty).Replace("\"", string.Empty).Replace("?", string.Empty).Replace("!", string.Empty).Replace(".", string.Empty).Replace(";", string.Empty);

                    GlossaryItem Syllabicated = Glossary.SingleOrDefault(x => x.Word == Word.Trim().ToLower());
                    if (Syllabicated == null)
                    {
                        if (lang == "English")
                        {
                            // Try to get the syllables from howmanysyllables.com
                            string url = "https://www.howmanysyllables.com/syllables/" + Word;
                            string html = new WebClient().DownloadString(url);
                            string SyllableInfoCheckString = "into syllables: &nbsp; <span class=\"Answer_Red\" data-nosnippet>";
                            Thread.Sleep(102); // Sleep so that we don't slam the how many syllables website

                            if (html.Contains(SyllableInfoCheckString))
                            {
                                var parts = html.Replace(SyllableInfoCheckString, "☯").Split('☯');
                                string syllables = parts[1].Split('<')[0];
                                Syllabicated = new GlossaryItem
                                {
                                    Word = Word,
                                    Syllables = syllables.Split('-')
                                };

                                // Can we add this to our glossary?
                                //string p = jsonPath.Replace(".json", ".json.flat");
                                //System.IO.File.WriteAllText(p, "Hello");
                                Glossary.Add(Syllabicated);
                                SaveGlossary(Glossary, lang);
                            }
                        }
                    }

                    // If, after all this, Syllabicated is still null... just make it a one-syllable word
                    if (Syllabicated == null)
                    {
                        Syllabicated = new GlossaryItem() { Word = Word, Syllables = new string[] { Word } };
                    }

                    for (int i = 0; i < Syllabicated.Syllables.Length; i++)
                    {
                        var ThisSongLine = new SongLine();
                        ThisSongLine.Starter = ":";
                        ThisSongLine.StartingTime = SongCounter;
                        ThisSongLine.NoteLength = beats;
                        ThisSongLine.NoteValue = 12;
                        ThisSongLine.Syllable = Syllabicated.Syllables[i];

                        if (i == 0 && Word.Substring(0, 1).ToUpper() == Word.Substring(0, 1))
                        {
                            // Capitalize this syllable
                            ThisSongLine.Syllable = ThisSongLine.Syllable.Substring(0, 1).ToUpper() + ThisSongLine.Syllable.Substring(1);
                        }
                        if (i == Syllabicated.Syllables.Length - 1)
                            ThisSongLine.Syllable += " "; // Add a space at the end of the word
                        Output.Add(ThisSongLine.ToString());
                        SongCounter += beats + 1;
                    }
                }

                SongCounter++;
                Output.Add("- " + SongCounter);

                SongCounter += beats * 2; // Add more beats for the line break?
            }

            return Output;
        }

        private void CleanGlossary(string lang)
        {
            var Glossary = LoadGlossary(lang);

            // Only save words to the glossary if they are devoid of punctuation
            Glossary.RemoveAll(x => x.Word.Contains("?") || x.Word.Contains("\"") || x.Word.Contains(",") || x.Word.Contains("!") || x.Word.Contains(".") || x.Word.Contains(";") || x.Word.Contains("(") || x.Word.Contains(")") || x.Word.EndsWith("-"));

            // Sort the glossary
            Glossary = Glossary.OrderBy(x => x.Word).ToList();

            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", lang + ".json");
            System.IO.File.WriteAllText(jsonPath, JsonConvert.SerializeObject(Glossary), System.Text.Encoding.UTF8);
        }

        private List<GlossaryItem> LoadGlossary(string lang)
        {
            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", lang + ".json");
            string json = System.IO.File.ReadAllText(jsonPath);
            var Glossary = JsonConvert.DeserializeObject<List<GlossaryItem>>(json);
            return Glossary;
        }

        private void SaveGlossary(List<GlossaryItem> Glossary, string lang)
        {
            //File.WriteAllText("C:\\Users\\graph\\source\\repos\\SongSyllabicator\\SongSyllabicator\\glossary-en.json", JsonConvert.SerializeObject(Glossary, Formatting.Indented), System.Text.Encoding.UTF8);

            string jsonPath = Path.Combine(_hostingEnvironment.WebRootPath, "json", lang + ".json");
            System.IO.File.WriteAllText(jsonPath, JsonConvert.SerializeObject(Glossary), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Splits a word line from UltraStar into multiple lines across its syllables
        /// </summary>
        /// <param name="UltraStarLine">A line from UltraStar that includes the word and its place in the file (i.e. : 338 40 11 Hello) </param>
        /// <param name="lang">The language of the dictionary to check</param>
        /// <returns></returns>
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
                        Thread.Sleep(102); // Sleep so that we don't slam the how many syllables website

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
                            //string p = jsonPath.Replace(".json", ".json.flat");
                            //System.IO.File.WriteAllText(p, "Hello");
                            Glossary.Add(GItem);
                            SaveGlossary(Glossary, lang);
                        }
                    }
                }

                // If, after all this, GItem is still null... just make it a one-syllable word
                if (GItem == null)
                {
                    GItem = new GlossaryItem() { Word = songLine.SyllableWord, Syllables = new string[] { songLine.SyllableWord } };
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
    }
}