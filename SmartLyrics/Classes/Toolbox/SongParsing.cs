﻿using SmartLyrics.Common;
using static SmartLyrics.Common.Logging;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmartLyrics.Toolbox
{
    internal class SongParsing
    {
        //returns as index 0 the title of the notification and as index 1 the artist
        public static Song GetTitleAndArtistFromExtras(string extras)
        {
            string _title = Regex.Match(extras, @"(?<=android\.title=)(.*?)(?=, android\.)").ToString();
            if (_title.Contains("Remix") || _title.Contains("remix") || _title.Contains("Mix"))
            {
                _title = Regex.Replace(_title, @"\(feat\..*?\)", "");
            }
            else
            {
                _title = Regex.Replace(_title, @"\(.*?\)", "");
                _title.Trim();
            }

            string _artist = Regex.Match(extras, @"(?<=android\.text=)(.*?)(?=, android\.)").ToString();

            Song output = new Song() { Title = _title, Artist = _artist };

            return output;
        }

        public static Song StripSongForSearch(Song input)
        {
            /* Strips artist and title strings for remixes, collabs and edits.
             * Made to work with stiuations like the ones below:
             * - Song Name (Artist's Remix)
             * - Song Name (feat. Artist) [Other Artist's Remix]
             * - Artist 1 & Artist 2
             * And any other variation of these. Since most services only
             * use brackets and parenthesis, we separate everything inside
             * them to parse those strings.
             * 
             * The objective is to search for the original song in case
             * of remixes so if a remixed version isn't on Genius, the original
             * will be shown to the user. Also, featuring 'tags' are
             * never used in Genius, so we should always remove those.
             */

            string strippedTitle = input.Title;
            string strippedArtist = input.Artist;

            //removes any Remix, Edit, or Featuring info encapsulated
            //in parenthesis or brackets
            if (input.Title.Contains("(") || input.Title.Contains("["))
            {
                List<Match> inside = Regex.Matches(input.Title, @"\(.*?\)").ToList();
                List<Match> insideBrk = Regex.Matches(input.Title, @"\[.*?\]").ToList();
                inside = inside.Concat(insideBrk).ToList();

                Log(Type.Error, $"{inside.Count()}");

                foreach (Match s in inside)
                {
                    if (s.Value.ToLowerInvariant().ContainsAny("feat", "ft", "featuring", "edit", "mix"))
                    {
                        strippedTitle = input.Title.Replace(s.Value, "");
                    }
                }
            }

            strippedTitle.Replace("🅴", ""); //remove "🅴" used by Apple Music for explicit songs

            if (input.Artist.Contains(" & "))
            {
                strippedArtist = Regex.Replace(input.Artist, @" & .*$", "");
            }

            strippedTitle.Trim();
            strippedArtist.Trim();

            Song output = new Song() { Title = strippedTitle, Artist = strippedArtist };
            Log(Type.Processing, $"Stripped title from {input} to {output.Title}");
            return output;
        }

        public static async Task<int> CalculateLikeness(Song result, Song notification, int index)
        {
            /* This method is supposed to accurately measure how much the detected song
             * is like the song from a search result. It's based on the Text Distance concept.
             * 
             * It's made to work with titles and artists like:
             * - "Around the World" by "Daft Punk" | Standard title
             * - "Mine All Day" by "PewDiePie & BoyInABand" | Collabs
             * - "さまよいよい咽　(Samayoi Yoi Ondo)" by "ずとまよ中でいいのに　(ZUTOMAYO)" | Titles and/or artists with romanization included
             * 
             * And any combination of such. Works in conjunction with a search method that includes
             * StripSongForSearch, so that titles with (Remix), (Club Mix) and such can be
             * found if they exist and still match if they don't.
             * 
             * For example, "Despacito (Remix)" will match exactly with a Genius search since they have a
             * remixed and non-remixed version. "Daddy Like (Diveo Remix)" will match the standard
             * song, "Daddy Like", since Genius doesn't have the remixed version.
            */

            string title = result.Title.ToLowerInvariant();
            string artist = result.Artist.ToLowerInvariant();

            string ntfTitle = notification.Title.ToLowerInvariant();
            ntfTitle.Replace("🅴", ""); //remove "🅴" used by Apple Music for explicit songs
            //remove anything inside brackets since almost everytime
            //it's not relevant info
            ntfTitle = Regex.Replace(ntfTitle, @"\[.*?\]", "").Trim();
            string ntfArtist = notification.Artist.ToLowerInvariant();

            title = await JapaneseTools.StripJapanese(title);
            artist = await JapaneseTools.StripJapanese(artist);

            int titleDist = Text.Distance(title, ntfTitle);
            int artistDist = Text.Distance(artist, ntfArtist);

            //add likeness points if title or artist is incomplete.
            //more points are given to the artist since it's more common to have
            //something like "pewdiepie" vs. "pewdiepie & boyinaband"
            if (ntfTitle.Contains(title)) { titleDist -= 3; }
            if (ntfArtist.Contains(artist)) { artistDist -= 4; }

            int likeness = titleDist + artistDist + index;
            if (likeness < 0) { likeness = 0; }

            Log(Type.Info, $"SmartLyrics", $"Title - {title} vs {ntfTitle}\nArtist - {artist} vs {ntfArtist}\nLikeness - {likeness}");
            return likeness;
        }
    }
}