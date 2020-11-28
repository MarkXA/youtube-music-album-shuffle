using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YouTubeMusicAlbumShuffle
{
    public class Program
    {
        private class Album
        {
            public string Title { get; set; }
            public string BrowseId { get; set; }
        }

        private class Playlist
        {
            public string Title { get; set; }
            public string BrowseId { get; set; }
            public string PlaylistId { get; set; }
            public int NumTracks { get; set; }
        }

        private class Track
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string AlbumName { get; set; }
            public string AlbumId { get; set; }
            public string VideoId { get; set; }
            public JObject RemoveData { get; set; }
        }

        private static readonly HttpClient ytClient = new HttpClient();
        private static string ytLastResponse;

        public static async Task Main(string[] args)
        {
            ytClient.DefaultRequestHeaders.Add("User-Agent", "MxaYtmClient");
            ytClient.DefaultRequestHeaders.Add("Referer", "https://music.youtube.com/");
            ytClient.DefaultRequestHeaders.Add("X-Origin", "https://music.youtube.com");
            ytClient.DefaultRequestHeaders.Add("Authorization",
                "SAPISIDHASH 1593883007_515ff28f11ea7688fc2c6070d243c0e5128baf4f");
            ytClient.DefaultRequestHeaders.Add("Cookie",
                @"VISITOR_INFO1_LIVE=JH7hbi1PlX0; YSC=3EVRZviJJCQ; HSID=Ak4NQXBgdIE73e81x; SSID=AD9WHr6gH329-Uy8-; APISID=WfCt9nL2REg-UMNW/AaTcFJPicRfgF5_Xl; SAPISID=80jPT8RPLWrSlPbu/ADVrtuK3eXn54ZfYH; __Secure-HSID=Ak4NQXBgdIE73e81x; __Secure-SSID=AD9WHr6gH329-Uy8-; __Secure-APISID=WfCt9nL2REg-UMNW/AaTcFJPicRfgF5_Xl; __Secure-3PAPISID=80jPT8RPLWrSlPbu/ADVrtuK3eXn54ZfYH; SID=yAc9RJiIXFWEEVoQEkDWgFqAZKJ7rbTGoF6AomRF7tb5ZPNKkRgx-S-V_LItNeflGcOT0w.; __Secure-3PSID=yAc9RJiIXFWEEVoQEkDWgFqAZKJ7rbTGoF6AomRF7tb5ZPNK60CRgLe-vzg3vdXhARR1cg.; LOGIN_INFO=AFmmF2swRAIgfBlkK-aMbg2izEE07pwfSogPbHUvoNo2GyiWTh5aFWoCIErKNcRNc4sa92Y_34kfiygd0AbVt5pG3qjX_Ieh6cHp:QUQ3MjNmeElXcEQ1X1JROERCR3pKNndzNGJuTllJSXZ4NWpIQk1lU2lwRm5Oa3JwNEhzbzlrWExjZWd6RGdhcUtFclZNbUVOb1NVZS1vQzdwR1BlbGRSSWRDc1Z0LUdYSVl3bWp2Z0lVd04zTHZGQW4wNEw0S0R5WE8zNUZhZGFDYWlzYlRSQ3dQWDlqeVhHdm1kU0FQYVV3TFByQ092SUtOSTFhT2ZhekxBVkE1TkVJQmlqV2h3; PREF=f5=30000&al=en-GB%2Ben&f6=80&library_tab_browse_id=FEmusic_liked_videos&volume=100&hl=en-GB&autoplay=false&repeat=NONE&location_based_recommendations=2; YTC=liv|1593883586; SIDCC=AJi4QfE3WCf9fpvQ0RViwzrvqign_591NsindDWy7OtjnE-kjpg71UgxjDeNoleGkhNQSP4W61o");

            var playlist = await GetAlbumShufflePlaylist();

            while (true)
            {
                var tracks = await GetPlaylistTracks(playlist.BrowseId);
                var albumId = tracks[0].AlbumId;
                Console.Write($"Remove {tracks[0].Artist} - {tracks[0].AlbumName}? ");
                var reply = Console.ReadLine();
                if (!reply.ToUpperInvariant().StartsWith("Y"))
                    break;
                foreach (var track in tracks.TakeWhile(t => t.AlbumId == albumId))
                {
                    Console.WriteLine(track.Title);
                    await EditPlaylist(track.RemoveData);
                }
            }

            var albums = await GetAllAlbums();

            while (true)
            {
                playlist = await GetAlbumShufflePlaylist();
                Console.WriteLine($"{playlist.NumTracks} tracks");
                if (playlist.NumTracks > 900) break;

                var album = albums[new Random().Next(albums.Count)];
                Console.WriteLine($"Adding {album.Title}");

                var albumInfo = await BrowseSingle(album.BrowseId);
                var props = albumInfo.Descendants().OfType<JProperty>().ToList();
                var albumPlaylistId = (props.FirstOrDefault(p => p.Name == "audioPlaylistId") ??
                                       props.First(p => p.Name == "playlistId")).Value.Value<string>();
                
                await EditPlaylist(new JObject
                {
                    ["playlistId"] = playlist.PlaylistId,
                    ["actions"] = new JArray(
                        new JObject
                        {
                            ["action"] = "ACTION_ADD_PLAYLIST",
                            ["addedFullListId"] = albumPlaylistId
                        })
                });
            }
        }

        private static async Task<Playlist> GetAlbumShufflePlaylist()
        {
            var playlist =
                (await GetPlaylists()).First(p => p.Title.Equals("Album shuffle", StringComparison.OrdinalIgnoreCase));
            return playlist;
        }

        private static async Task<List<Playlist>> GetPlaylists()
        {
            return await Browse("FEmusic_liked_playlists", d =>
                    d.SelectTokens(@"$..items[?(@..pageType=='MUSIC_PAGE_TYPE_PLAYLIST')]").First().Parent as JArray,
                    //d
                    //        ["contents"]["singleColumnBrowseResultsRenderer"]["tabs"][0]["tabRenderer"]["content"]
                    //    ["sectionListRenderer"]["contents"][1]["itemSectionRenderer"]["contents"][0]["gridRenderer"][
                    //        "items"] as
                    //JArray,
                e =>
                {
                    var run = e["musicTwoRowItemRenderer"]?["title"]?["runs"]?[0];

                    var playlist = new Playlist
                    {
                        Title = run?["text"].Value<string>(),
                        BrowseId = run?["navigationEndpoint"]?["browseEndpoint"]?["browseId"]?.Value<string>(),
                        PlaylistId =
                            e.Descendants().OfType<JProperty>()
                                .FirstOrDefault(p => p.Name == "watchPlaylistEndpoint")?.Value?["playlistId"]
                                ?.Value<string>(),
                        NumTracks = int.Parse(e["musicTwoRowItemRenderer"]?["subtitle"]?["runs"]
                            ?.Select(elt => elt["text"]?.Value<string>())
                            .FirstOrDefault(s => s.Contains(" songs"))?.Split(" ")[0]
                            .Replace(",", "") ?? "0")
                    };
                    return playlist;
                });
        }

        private static async Task<List<Track>> GetPlaylistTracks(string playlistId)
        {
            return await Browse(playlistId,
                d => d["contents"]["singleColumnBrowseResultsRenderer"]["tabs"][0]["tabRenderer"]["content"]
                    ["sectionListRenderer"]["contents"][0]["musicPlaylistShelfRenderer"]["contents"] as JArray,
                e =>
                {
                    var runs = e["musicResponsiveListItemRenderer"]["flexColumns"]
                        .Select(c => c["musicResponsiveListItemFlexColumnRenderer"]["text"]["runs"][0])
                        .ToList();
                    return new Track
                    {
                        Title = runs[0]["text"].Value<string>(),
                        Artist = runs[1]["text"].Value<string>(),
                        AlbumName = runs[2]["text"].Value<string>(),
                        AlbumId = runs[2].SelectToken(@"$..browseEndpoint")?["browseId"].Value<string>() ?? runs[2]["text"].Value<string>(),
                        VideoId = e.SelectToken(@"$..removedVideoId").Value<string>(),
                        RemoveData = e["musicResponsiveListItemRenderer"]["menu"]["menuRenderer"]["items"]
                            .First(i => i.ToString().Contains("ACTION_REMOVE_VIDEO"))
                            ["menuServiceItemRenderer"]["serviceEndpoint"]["playlistEditEndpoint"] as JObject
                    };
                },
                tracks => tracks.First().AlbumId != tracks.Last().AlbumId);
        }

        private static async Task<List<Album>> GetAllAlbums()
        {
            var albums = await Browse("FEmusic_library_privately_owned_releases",
                d => d.SelectToken("..gridRenderer")["items"] as JArray,
                e =>
                {
                    var run = e["musicTwoRowItemRenderer"]["title"]["runs"][0];
                    return new Album
                    {
                        Title = run["text"].Value<string>(),
                        BrowseId = run["navigationEndpoint"]["browseEndpoint"]["browseId"].Value<string>()
                    };
                });

            albums.AddRange(await Browse("FEmusic_liked_albums",
                d => d.SelectToken("$..gridRenderer")["items"] as JArray,
                e =>
                {
                    var run = e["musicTwoRowItemRenderer"]["title"]["runs"][0];
                    return new Album
                    {
                        Title = run["text"].Value<string>(),
                        BrowseId = run["navigationEndpoint"]["browseEndpoint"]["browseId"].Value<string>()
                    };
                }));
            return albums;
        }

        private static async Task<List<T>> Browse<T>(
            string browseId,
            Func<JObject, JArray> getItems,
            Func<JObject, T> parseItem,
            Func<IList<T>, bool> fetchUntil = null)
        {
            var body = new
            {
                context = new
                {
                    client = new {clientName = "WEB_REMIX", clientVersion = "0.1.PWA"}
                },
                browseId
            };

            var continuationText = "";

            var arrayName = "";
            var items = new List<T>();
            while (true)
            {
                using var response = await ytClient.PostAsync(
                    "https://music.youtube.com/youtubei/v1/browse?alt=json&key=AIzaSyC9XL3ZjWddXya6X74dJoCTL-WEYFDNX30" +
                    continuationText,
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();

                var doc = JObject.Parse(await response.Content.ReadAsStringAsync());
                ytLastResponse = doc.ToString();

                JArray itemArray;
                if (string.IsNullOrEmpty(arrayName))
                {
                    itemArray = getItems(doc);
                    arrayName = ((JProperty) itemArray.Parent).Name;
                }
                else
                {
                    var continuationContents = doc["continuationContents"] as JObject;
                    if (continuationContents == null)
                        break;
                    itemArray = continuationContents.OfType<JProperty>().First().Value[arrayName] as JArray;
                }

                if (itemArray == null)
                    break;

                items.AddRange(itemArray.Select(t => parseItem((JObject) t)));

                if (fetchUntil != null && fetchUntil(items))
                    break;

                var continuations = itemArray.Parent.Parent["continuations"] as JArray;
                if (continuations == null)
                    break;
                continuationText = "&" + string.Join("&",
                    continuations[0]["nextContinuationData"].OfType<JProperty>()
                        .Select(p => p.Name + "=" + p.Value));
                Console.WriteLine(items.Count);
            }

            return items;
        }

        private static async Task EditPlaylist(JObject data)
        {
            data["context"] = JObject.FromObject(new
            {
                client = new {clientName = "WEB_REMIX", clientVersion = "0.1.PWA"}
            });

            var response = await ytClient.PostAsync(
                "https://music.youtube.com/youtubei/v1/browse/edit_playlist?alt=json&key=AIzaSyC9XL3ZjWddXya6X74dJoCTL-WEYFDNX30",
                new StringContent(data.ToString(), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
        }

        private static async Task<JObject> BrowseSingle(string albumId)
        {
            var data = JObject.FromObject(new
            {
                context = new
                {
                    client = new {clientName = "WEB_REMIX", clientVersion = "0.1.PWA"}
                },
                browseId = albumId
            });

            var response = await ytClient.PostAsync(
                "https://music.youtube.com/youtubei/v1/browse?alt=json&key=AIzaSyC9XL3ZjWddXya6X74dJoCTL-WEYFDNX30",
                new StringContent(data.ToString(), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var doc = JObject.Parse(await response.Content.ReadAsStringAsync());
            ytLastResponse = doc.ToString();

            return doc;
        }
    }
}