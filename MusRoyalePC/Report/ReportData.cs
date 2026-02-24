using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MusRoyalePC.Reports
{
    public sealed class ReportData
    {
        public DateTime GeneratedAt { get; set; }
        public string Source { get; set; } = "";

        public int TotalUsers { get; set; }
        public int PremiumCount { get; set; }
        public int NonPremiumCount { get; set; }

        public List<UserRow> TopWins { get; set; } = new();
        public List<UserRow> TopMatchesPlayed { get; set; } = new();
        public List<UserRow> TopMoney { get; set; } = new();
        public List<UserRow> TopGold { get; set; } = new();

        public static ReportData FromSnapshots(
            DateTime generatedAt,
            string source,
            int totalUsers,
            int premiumCount,
            int nonPremiumCount,
            QuerySnapshot topWins,
            QuerySnapshot topPlayed,
            QuerySnapshot topMoney,
            QuerySnapshot topGold)
        {
            return new ReportData
            {
                GeneratedAt = generatedAt,
                Source = source,
                TotalUsers = totalUsers,
                PremiumCount = premiumCount,
                NonPremiumCount = nonPremiumCount,
                TopWins = topWins.Documents.Select(MapUserFromYourSchema).ToList(),
                TopMatchesPlayed = topPlayed.Documents.Select(MapUserFromYourSchema).ToList(),
                TopMoney = topMoney.Documents.Select(MapUserFromYourSchema).ToList(),
                TopGold = topGold.Documents.Select(MapUserFromYourSchema).ToList()
            };
        }

        // ====== ZURE FIRESTORE ESKEMARA EGOKITUTA ======
        // Collection: Users
        // Fields:
        // - username: string
        // - premium: bool
        // - dinero: string (adib: "99777.50")
        // - oro: string (adib: "50500")
        // - partidak: number
        // - partidaIrabaziak: number
        private static UserRow MapUserFromYourSchema(DocumentSnapshot doc)
        {
            string name = doc.ContainsField("username") ? doc.GetValue<string>("username") : doc.Id;
            bool isPremium = doc.ContainsField("premium") && doc.GetValue<bool>("premium");

            long wins = GetLong(doc, "partidaIrabaziak");
            long played = GetLong(doc, "partidak");

            // dinero/oro string moduan datoz; parse egin behar da
            decimal dinero = GetDecimalFromString(doc, "dinero");
            long oro = GetLongFromString(doc, "oro");

            return new UserRow
            {
                UserId = doc.Id,
                DisplayName = name,
                IsPremium = isPremium,
                Wins = wins,
                MatchesPlayed = played,
                Money = dinero,
                Gold = oro
            };
        }

        private static long GetLong(DocumentSnapshot doc, string field)
        {
            if (!doc.ContainsField(field)) return 0;

            var v = doc.GetValue<object>(field);
            return v switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                _ => 0
            };
        }

        private static long GetLongFromString(DocumentSnapshot doc, string field)
        {
            if (!doc.ContainsField(field)) return 0;

            // Firestore-n batzuetan string, bestetan number gisa etor daiteke.
            var obj = doc.GetValue<object>(field);

            if (obj is long l) return l;
            if (obj is int i) return i;
            if (obj is double d) return (long)d;

            var s = obj?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return 0;

            // "50500" modukoak
            return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static decimal GetDecimalFromString(DocumentSnapshot doc, string field)
        {
            if (!doc.ContainsField(field)) return 0m;

            var obj = doc.GetValue<object>(field);

            if (obj is decimal m) return m;
            if (obj is double d) return (decimal)d;
            if (obj is long l) return l;
            if (obj is int i) return i;

            var s = obj?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;

            // Kontuan: zure adibidean "99777.50" puntua darabil.
            // InvariantCulture erabiliko dugu segurua izateko.
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;
        }
    }

    public sealed class UserRow
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsPremium { get; set; }

        public long Wins { get; set; }                 // partidaIrabaziak
        public long MatchesPlayed { get; set; }        // partidak
        public decimal Money { get; set; }             // dinero
        public long Gold { get; set; }                 // oro
    }
}