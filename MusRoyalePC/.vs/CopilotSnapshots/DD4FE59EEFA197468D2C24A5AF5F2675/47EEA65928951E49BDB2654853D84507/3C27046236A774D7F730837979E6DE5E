using Google.Cloud.Firestore;
using Microsoft.Win32;
using MusRoyalePC.Controllers;
using MusRoyalePC.Reports;
using MusRoyalePC.Services;
using QuestPDF.Fluent;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusRoyalePC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1) Erabiltzaileak non gorde aukeratzeko
                var sfd = new SaveFileDialog
                {
                    Title = "Gorde ranking txostena",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"MUS_Ranking_{DateTime.Now:yyyy-MM-dd}.pdf",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (sfd.ShowDialog() != true)
                    return;

                // 2) Firestore konexioa
                var db = FirestoreService.Instance.Db;

                // ZURE COLLECTION-A: "Users" (maiuskulaz)
                var users = db.Collection("Users");

                // Rankingetan zenbat erakutsi
                const int topN = 5;

                // 3) Kontsultak (wins eta partidak ondo, dinero/oro string badira kontuz)
                var topWinsTask = users.OrderByDescending("partidaIrabaziak").Limit(topN).GetSnapshotAsync();
                var topPlayedTask = users.OrderByDescending("partidak").Limit(topN).GetSnapshotAsync();

                // OHARRA: dinero/oro STRING badira, ordena lexikografikoa egingo du Firestore-k.
                // Horregatik, hemen estrategia segurua erabiltzen dugu:
                // - user guztiak ekarri (edo N handixka) eta lokalean ordenatu.
                // Zure user kopurua oso handia bada, gomendatua: dinero/oro Firestore-n number bezala gordetzea.
                var allUsersTask = users.GetSnapshotAsync();

                await Task.WhenAll(topWinsTask, topPlayedTask, allUsersTask);

                var allDocs = allUsersTask.Result.Documents;

                // Premium/Ez-premium count
                int totalUsers = allDocs.Count;
                int premiumCount = allDocs.Count(d => d.ContainsField("premium") && d.GetValue<bool>("premium"));
                int nonPremiumCount = totalUsers - premiumCount;

                // 4) Lokalean TopMoney/TopGold (dinero/oro string-etik parse eginda)
                var allMapped = allDocs.Select(ReportDataDebugMapper.MapUserFromYourSchema).ToList();

                var topMoney = allMapped
                    .OrderByDescending(u => u.Money)
                    .ThenBy(u => u.DisplayName)
                    .Take(topN)
                    .ToList();

                var topGold = allMapped
                    .OrderByDescending(u => u.Gold)
                    .ThenBy(u => u.DisplayName)
                    .Take(topN)
                    .ToList();

                // 5) ReportData osatu (TopWins/TopPlayed Firestore query-etik, TopMoney/TopGold lokalean)
                var reportData = new ReportData
                {
                    GeneratedAt = DateTime.Now,
                    Source = "Firestore / Users",
                    TotalUsers = totalUsers,
                    PremiumCount = premiumCount,
                    NonPremiumCount = nonPremiumCount,
                    TopWins = topWinsTask.Result.Documents.Select(ReportDataDebugMapper.MapUserFromYourSchema).ToList(),
                    TopMatchesPlayed = topPlayedTask.Result.Documents.Select(ReportDataDebugMapper.MapUserFromYourSchema).ToList(),
                    TopMoney = topMoney,
                    TopGold = topGold
                };

                // 6) PDF sortu
                var doc = new MusRankingReportDocument(reportData);
                doc.GeneratePdf(sfd.FileName);

                MessageBox.Show("PDF txostena sortuta.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errorea:\n{ex}", "Errorea", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Mapping-a hemen bertan jarri dut erraz copy/paste egiteko.
        /// Nahi baduzu, eraman Reports/ReportData.cs-ra edo Services-era.
        /// </summary>
        private static class ReportDataDebugMapper
        {
            // ZURE FIRESTORE ESKEMA:
            // Users docs:
            // - username: string
            // - premium: bool
            // - dinero: string ("99777.50")
            // - oro: string ("50500")
            // - partidak: number
            // - partidaIrabaziak: number
            public static UserRow MapUserFromYourSchema(DocumentSnapshot doc)
            {
                string name = doc.ContainsField("username") ? doc.GetValue<string>("username") : doc.Id;
                bool isPremium = doc.ContainsField("premium") && doc.GetValue<bool>("premium");

                long wins = GetLong(doc, "partidaIrabaziak");
                long played = GetLong(doc, "partidak");

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

                var obj = doc.GetValue<object>(field);

                if (obj is long l) return l;
                if (obj is int i) return i;
                if (obj is double d) return (long)d;

                var s = obj?.ToString();
                if (string.IsNullOrWhiteSpace(s)) return 0;

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

                return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0m;
            }
        }
    }
}