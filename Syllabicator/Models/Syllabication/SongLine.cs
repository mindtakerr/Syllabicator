namespace Syllabicator.Models.Syllabication
{
    public class SongLine
    {
        public string Starter { get; set; }
        public int StartingTime { get; set; }
        public int NoteLength { get; set; }
        public int NoteValue { get; set; }
        public string Syllable { get; set; }

        public string SyllableWord
        {
            get
            {
                return Syllable.Trim().ToLower();
            }
        }

        public bool IsCapital
        {
            get
            {
                return Syllable.Substring(0, 1).ToUpper() == Syllable.Substring(0, 1);
            }
        }

        public bool HasLeadingSpace { get; set; }
        public bool HasTrailingSpace { get; set; }

        public SongLine()
        {
        }

        public SongLine(string ExistingLine)
        {
            var Pieces = ExistingLine.Split(' ');
            Starter = Pieces[0];
            StartingTime = int.Parse(Pieces[1]);
            NoteLength = int.Parse(Pieces[2]);
            NoteValue = int.Parse(Pieces[3]);
            HasLeadingSpace = false;

            // Pieces.ToList().RemoveAll(x=>x.leng)

            if (Pieces.Length == 5)
            {
                var SyllableStart = ExistingLine.IndexOf(Pieces[4]);
                Syllable = ExistingLine.Substring(SyllableStart);
            }
            else
            {
                if (Pieces[5].Length < 1)
                {
                    HasTrailingSpace = true;
                    Syllable = Pieces[4];
                }
                if (Pieces[4].Length < 1)
                {
                    HasLeadingSpace = true;
                    Syllable = Pieces[5];
                }
            }
        }

        public override string ToString()
        {
            return Starter + " " + StartingTime + " " + NoteLength + " " + NoteValue + " " + Syllable;
        }
    }
}