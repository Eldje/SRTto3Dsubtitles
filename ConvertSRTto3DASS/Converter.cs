using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;
using File = System.IO.File;

//SSA/ASS specification v4+ http://www.tcax.org/docs/ass-specs.htm
//SRT (SubRip) specification https://en.wikipedia.org/wiki/SubRip

namespace ConvertSRTto3DASS
{
    class Converter
    {
        internal static string srtFilePath;
        internal static int width;
        internal static int height;
        internal static int depthOffset;
        internal static int fontSize;

        // On définit une classe pour stocker les paramètres
        public class Options
        {
            [Option('f', "srtFilePath", Required = true, HelpText = "Chemin du fichier .srt")]
            public string SrtFilePath { get; set; }

            [Option('w', "width", Required = false, HelpText = "Résolution horizontale du film")]
            public int? Width { get; set; }

            [Option('h', "heigh", Required = false, HelpText = "Résolution verticale du film")]
            public int? Height { get; set; }

            [Option('d', "dephtOffset", Required = false, HelpText = "Offset de profondeur")]
            public int? DepthOffset { get; set; }

            [Option('s', "size", Required = false, HelpText = "Taille de la police")]
            public int? Size { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions)
                .WithNotParsed(HandleParseError);
        }

        // Cette méthode est appelée si le parsing réussit
        static void RunOptions(Options opts)
        {
            // 4. Initialisation des variables globales
            srtFilePath = opts.SrtFilePath;
            width = opts.Width ?? 1920;
            height = opts.Height ?? 1080;
            depthOffset = opts.DepthOffset ?? 15;
            fontSize = opts.Size ?? 50;

            // Appel de la logique principale de votre application
            var extracted = ExtractSubFromSRT();
            var header = CreateHeader();
            var style = CreateStandardStyle();
            var events = ProcessSubs(extracted);

            var finished = "[Script Info]\n" + header + "\n\n[V4+ Styles]\n" + style + "\n\n[Events]\n" + events;
            File.WriteAllText(Path.GetFileNameWithoutExtension(srtFilePath) + ".ass", finished, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        static void HandleParseError(System.Collections.Generic.IEnumerable<Error> errs)
        {
            // En cas d'erreur (ex: paramètre obligatoire manquant), 
            // la bibliothèque affiche automatiquement l'aide.
            Console.WriteLine("Erreur lors de la lecture des arguments.");
        }

        //Is there a better way to do it?
        private static Dictionary<Regex, string> regexReplacementDict =
            new Dictionary<Regex, string> {
                {new Regex("<b>"), "{\\b1}"},
                {new Regex("(</b>)"),"{\\b0}"},
                {new Regex("(<i>)"),"{\\i1}"},
                {new Regex("(</i>)"),"{\\i0}"},
                {new Regex("(<u>)"),"{\\u1}" },
                {new Regex("(</u>)"),"{\\u0}"},
                {new Regex("(</font>)"),"{\\c&HFFFFFF&}"} //TODO custom colors. When you add custom colors, it needs to change this too

            };
        private static Regex color = new Regex("<font color=\"#.{6}\">");

        //TODO Add positional data to the formatting. 
        private static string ChangeFormatting(string line)
        {
            foreach (var tuple in regexReplacementDict)
            {
                line = tuple.Key.Replace(line, tuple.Value);
            }

            while (color.IsMatch(line))
            {
                var match = color.Match(line);
                string str_match = match.Value;
                var color_str = str_match.Substring(14, 6); //RGB value in HEX

                //ASS uses RGB value, but in reverse order so got to reverse it
                var char_array = color_str.ToCharArray();
                Array.Reverse(char_array);
                color_str = new string(char_array);

                line = color.Replace(line, "{\\c&" + color_str + "&}");

            }

            return removeFormatting(line);
        }


        //TODO: remove this and use the above method instead where it actually uses the formatting of the file
        private static Regex reg = new Regex("<.+?>|(\\r)"); //Used to remove html tags used and line breaks
        private static string removeFormatting(string line)
        {
            var replacement = reg.Replace(line, "");
            return replacement;

        }


        private static string ProcessSubs(List<Tuple<string, string, string, string>> srt)
        {
            string endResult = "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n";
            foreach (var events in srt)
            {
                var start = ConvertTimeStamp(events.Item1);
                var end = ConvertTimeStamp(events.Item2);

                //First layer, for the right eye
                var line = "Dialogue: " + 0 + "," + start + "," + end + ",Right,,0,0,0,," + events.Item3 + "\n";
                //Second layer, for the left eye
                line += "Dialogue: " + 1 + "," + start + "," + end + ",Left,,0,0,0,," + events.Item3;

                endResult += line + "\n";
            }
            return endResult;

        }


        private static string ConvertTimeStamp(string timeStamp)
        {
            //Change double digit hour (for .srt) to single digit hour marker (for .ass) <- very crude
            var tmp = timeStamp.Substring(1);

            //Change thousands of a second hunderdth second
            tmp = tmp.Substring(0, tmp.Length - 1);//TODO: Do actual rounding
            return tmp.Replace(",", ".");
        }

        //TODO: Make this human readable
        //TODO: Add adjustable parameters
        private static string CreateStandardStyle(int width = 1920, int depthOffset = 10)
        {
            int offset = (int)Math.Floor((double)width / 2) - depthOffset;

            string style = "Format: " +
                "Name, " +
                "Fontname, " +
                "Fontsize, " +
                "PrimaryColour, " +
                "SecondaryColour, " +
                "OutlineColour, " +
                "BackColour, " +
                "Bold, " +
                "Italic, " +
                "Underline, " +
                "StrikeOut, " +
                "ScaleX, " +
                "ScaleY, " +
                "Spacing, " +
                "Angle, " +
                "BorderStyle, " +
                "Outline, " +
                "Shadow, " +
                "Alignment, " +
                "MarginL, " +
                "MarginR, " +
                "MarginV, " +
                "Encoding\n" +

                "Style: " +
                "Right," +
                "Arial," +
                fontSize + "," +
                "&H00ffffff," +
                "&H0000ffff," +
                "&H00000000," +
                "&H00000000," +
                "0," +
                "0," +
                "0," +
                "0," +
                "100," +
                "100," +
                "0," +
                "0," +
                "1," +
                "1," +
                "0," +
                "2," +
                offset + "," +
                "0," +
                "10," +
                "0\n" +


                "Style: " +
                "Left," +
                "Arial," +
                fontSize + "," +
                "&H00ffffff," +
                "&H0000ffff," +
                "&H00000000," +
                "&H00000000," +
                "0," +
                "0," +
                "0," +
                "0," +
                "100," +
                "100," +
                "0," +
                "0," +
                "1," +
                "1," +
                "0," +
                "2," +
                "0," +
                offset + "," +
                "10," +
                "0";
            return style;
        }

        //TODO: create a system where you can actually give paramaters to it
        private static string CreateHeader()
        {
            var name = Path.GetFileNameWithoutExtension(srtFilePath);
            string scriptinfo = "; Generated by ConvertSRTto3D\n" +
                "Title: " + name + "\n" +
                "ScriptType: v4.00+\n" +
                "PlayDepth: 0\n" +
                "Collisions: Normal\n" +
                "PlayResX: " + width + "\n" +
                "PlayResY: " + height + "\n";
            return scriptinfo;
        }

        private static string ReadTextSmart(string path)
        {
            // Try reading as UTF-8 with BOM detection
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string text = reader.ReadToEnd();

                // If UTF-8 decoding produced replacement character <?>, fallback to CP1252
                if (text.Contains('\uFFFD'))
                    text = File.ReadAllText(path, Encoding.GetEncoding(1252));

                return text;
            }
        }

        //start, end, text, format <- tuple format
        //Note - I want to change the tuple as format bit is useless as the data is carried in the text field for both .srt and .ass
        //but I could reprepose it for any positional data
        private static List<Tuple<string, string, string, string>> ExtractSubFromSRT()
        {

            var converted = new List<Tuple<string, string, string, string>>();

            var timestamp_start = "";
            var timestamp_end = "";
            var subtitles = "";
            string srt = ReadTextSmart(srtFilePath);


            int dialogNumber = 1; // Which dialog we are on
            int dialogLine = 0; // Which line of the dialog block we are on 
            int linecounter = 0; //Where we are in the .srt, meant for debugging

            foreach (string line in srt.Split('\n'))
            {
                linecounter++;
                //Empty line assumes that the next line with event number thus previous dialog is finished and can be saved
                if (line == "" | line == "\r")
                {
                    dialogLine = 0;
                    converted.Add(new Tuple<string, string, string, string>(timestamp_start, timestamp_end, ChangeFormatting(subtitles), ""));
                    subtitles = "";
                    continue;
                }

                //Handle the first line
                if (dialogLine == 0)
                {
                    //Check that the line is an actual number
                    if (int.TryParse(line, out int k) && k == dialogNumber)
                    {
                        dialogNumber++;
                        dialogLine++;
                        continue;
                    }

                    Console.Error.WriteLine("Something went wrong");
                    Console.Error.WriteLine("Something wrong on line:" + linecounter);
                    System.Environment.Exit(-1);
                }

                //Timestamp extraction
                if (dialogLine == 1)
                {
                    timestamp_start = line.Substring(0, 12);
                    timestamp_end = line.Substring(17, 12);
                    dialogLine++;
                }
                else
                {
                    if (subtitles == "")
                    {
                        subtitles = line;
                        subtitles = subtitles.Replace("\\.r", "");
                    }
                    else
                    {
                        subtitles = subtitles + "\\N" + line;
                    }
                }
            }
            return converted;
        }
    }
}
