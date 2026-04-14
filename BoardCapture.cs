using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using Core = Hearthstone_Deck_Tracker.API.Core;

namespace AmalgadonPlugin
{
    internal static class BoardCapture
    {
        private const string BaseUrl = "https://www.amalgadon.com/";

        public static void OpenCurrentBoard()
        {
            if (Core.Game.CurrentGameType != GameType.GT_BATTLEGROUNDS)
            {
                MessageBox.Show(
                    "You must be in a Battlegrounds game to use Amalgadon Board Capture.",
                    "Amalgadon Board Capture",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                string encoded = EncodeBuild();
                Log.Info($"[Amalgadon] Opening URL with encoded board");
                Process.Start(BaseUrl + "?remix=" + encoded);
            }
            catch (Exception ex)
            {
                Log.Error($"[Amalgadon] Failed to open: {ex}");
                MessageBox.Show(
                    "Failed to open Amalgadon: " + ex.Message,
                    "Amalgadon Board Capture",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string EncodeBuild()
        {
            // find the hero.
            var heroEntity = Core.Game.Entities.Values
                .FirstOrDefault(e =>
                    e.IsHero &&
                    e.IsInPlay &&
                    e.IsControlledBy(Core.Game.Player.Id));
            string heroCardId = StripHeroSkin(heroEntity?.CardId);

            // Use Entities.Values with IsMinion to exclude hero powers, enchantments, and the
            // hero entity itself — Core.Game.Player.Board can include those in BG games.
            var boardMinions = Core.Game.Entities.Values
                .Where(e =>
                    e.IsMinion &&
                    e.IsInPlay &&
                    e.IsControlledBy(Core.Game.Player.Id))
                .OrderBy(e => e.GetTag(GameTag.ZONE_POSITION))
                .ToList();

            // Build exactly 7-slot array using ZONE_POSITION (1-based) → index (0-based).
            // This handles non-contiguous positions correctly.
            var slots = new string[7];
            foreach (var entity in boardMinions)
            {
                if (string.IsNullOrEmpty(entity.CardId))
                    continue;

                int pos = entity.GetTag(GameTag.ZONE_POSITION);
                if (pos < 1 || pos > 7)
                    continue;

                bool golden = entity.GetTag(GameTag.PREMIUM) != 0;
                slots[pos - 1] = golden ? entity.CardId + ":g" : entity.CardId;
            }

            // Trinket detection: not implemented in v1.
            // Investigate entities in Zone.HAND / Zone.SETASIDE with CardType == BATTLEGROUND_TRINKET
            // once the zone / enum values are confirmed in-game.
            string lesserTrinket  = null;
            string greaterTrinket = null;

            var compact = new CompactState
            {
                Hero          = heroCardId,
                Slots         = slots.ToList(),
                LesserTrinket  = lesserTrinket,
                GreaterTrinket = greaterTrinket,
            };

            // NullValueHandling.Ignore omits null *properties* (h, lt, gt) from the JSON
            // while preserving null *elements* inside the slots array.
            string json = JsonConvert.SerializeObject(
                compact,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            byte[] jsonBytes  = Encoding.UTF8.GetBytes(json);
            byte[] compressed = ZlibCompress(jsonBytes);
            return "p" + ToBase64Url(compressed);
        }

        /// <summary>
        /// Returns the base hero card ID, stripping any skin suffix.
        /// e.g. "TB_BaconShop_HERO_49_SKIN_F" → "TB_BaconShop_HERO_49"
        /// </summary>
        private static string StripHeroSkin(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return cardId;
            int idx = cardId.IndexOf("_SKIN_", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? cardId.Substring(0, idx) : cardId;
        }

        /// <summary>
        /// Wraps raw deflate output in a zlib envelope (RFC 1950) at compression level 9.
        /// .NET Framework 4.7.2 lacks ZLibStream, so the header and Adler-32 trailer are
        /// written manually around DeflateStream.
        /// </summary>
        private static byte[] ZlibCompress(byte[] input)
        {
            using (var output = new MemoryStream())
            {
                // zlib header: CMF=0x78 (deflate, 32 KB window), FLG=0xDA (best compression)
                // Invariant: (CMF * 256 + FLG) % 31 == 0  →  (0x78 * 256 + 0xDA) % 31 == 0  ✓
                output.WriteByte(0x78);
                output.WriteByte(0xDA);

                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(input, 0, input.Length);
                }

                // Adler-32 checksum of the *original* (uncompressed) bytes, big-endian
                uint adler = Adler32(input);
                output.WriteByte((byte)(adler >> 24));
                output.WriteByte((byte)(adler >> 16));
                output.WriteByte((byte)(adler >> 8));
                output.WriteByte((byte)adler);

                return output.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint MOD = 65521;
            uint s1 = 1, s2 = 0;
            foreach (byte b in data)
            {
                s1 = (s1 + b) % MOD;
                s2 = (s2 + s1) % MOD;
            }
            return (s2 << 16) | s1;
        }

        private static string ToBase64Url(byte[] bytes)
            => Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
    }

    internal class CompactState
    {
        [JsonProperty("h")]
        public string Hero { get; set; }

        /// <summary>
        /// Exactly 7 elements. Null elements represent empty board slots.
        /// Non-null elements are either "CARD_ID" or "CARD_ID:g" for golden.
        /// </summary>
        [JsonProperty("s")]
        public List<string> Slots { get; set; }

        [JsonProperty("lt")]
        public string LesserTrinket { get; set; }

        [JsonProperty("gt")]
        public string GreaterTrinket { get; set; }
    }
}
