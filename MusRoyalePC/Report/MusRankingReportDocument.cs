using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusRoyalePC.Reports
{
    public sealed class MusRankingReportDocument : IDocument
    {
        private readonly ReportData _data;

        // Kolore “theme” sinplea
        private const string Brand = "#2D6CDF";
        private const string BrandDark = "#1E3A8A";
        private const string Success = "#16A34A";
        private const string Warning = "#F59E0B";
        private const string Danger = "#DC2626";
        private const string MutedText = "#475569";
        private const string Border = "#E2E8F0";
        private const string CardBg = "#F8FAFC";

        public MusRankingReportDocument(ReportData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(16);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(CardBg)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("MUS — Ranking Txostena (Snapshot)")
                                .FontSize(18).SemiBold().FontColor(BrandDark);

                            left.Item().Text("Erabiltzaileak • Rankingak • Premium konparaketa")
                                .FontSize(9).FontColor(MutedText);
                        });

                        row.ConstantItem(120).AlignRight().Column(right =>
                        {
                            right.Item().Text("Sortze data").FontSize(8).FontColor(MutedText);
                            right.Item().Text(_data.GeneratedAt.ToString("yyyy-MM-dd"))
                                .FontSize(11).SemiBold();

                            right.Item().PaddingTop(4).Text("Iturria").FontSize(8).FontColor(MutedText);
                            right.Item().Text(_data.Source).FontSize(9);
                        });
                    });

                    col.Item().Height(4).Background(Brand);
                });
        }

        private void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(10);

                col.Item().Element(ComposeKpis);

                // Grafikoa + Premium taula txikia (bi zutabe)
                col.Item().Row(row =>
                {
                    row.Spacing(10);

                    row.RelativeItem(2).Element(c => ComposeBarChartCard(
                        c,
                        title: "Top 5 — Irabaziak (bar chart)",
                        rows: _data.TopWins,
                        valueSelector: u => (double)u.Wins,
                        labelSelector: u => u.DisplayName
                    ));

                    row.RelativeItem().Element(ComposePremiumTableCard);
                });

                // 2x2 ranking taulak
                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(c => ComposeRankingCard(c, "Gehien irabazi dutenak (Top)", _data.TopWins, u => u.Wins.ToString(), "Irabaziak"));
                    row.RelativeItem().Element(c => ComposeRankingCard(c, "Gehien jolastu dutenak (Top)", _data.TopMatchesPlayed, u => u.MatchesPlayed.ToString(), "Jokatutakoak"));
                });

                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(c => ComposeRankingCard(c, "Diru gehien dutenak (Top)", _data.TopMoney, u => $"{u.Money:0.##}", "Dirua"));
                    row.RelativeItem().Element(c => ComposeRankingCard(c, "Urre gehien dutenak (Top)", _data.TopGold, u => u.Gold.ToString(), "Urrea"));
                });

                col.Item().Element(ComposePremiumComparisonBar);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(9).FontColor(MutedText));
                text.Span("MUS Royale PC — ");
                text.Span(_data.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
                text.Span(" — Orri 1/1");
            });
        }

        private void ComposeKpis(IContainer container)
        {
            var ratio = _data.TotalUsers == 0 ? 0 : (double)_data.PremiumCount / _data.TotalUsers * 100;

            container.Row(row =>
            {
                row.Spacing(10);

                row.RelativeItem().Element(c => KpiBox(c, "Erabiltzaileak", _data.TotalUsers.ToString(), Brand, "Guztira"));
                row.RelativeItem().Element(c => KpiBox(c, "Premium", _data.PremiumCount.ToString(), Success, "Aktiboak"));
                row.RelativeItem().Element(c => KpiBox(c, "Ez-Premium", _data.NonPremiumCount.ToString(), Warning, "Free"));
                row.RelativeItem().Element(c => KpiBox(c, "Premium %", $"{ratio:0.#}%", BrandDark, "Ratio"));
            });
        }

        private void KpiBox(IContainer container, string title, string value, string accentColor, string subtitle)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(CardBg)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(2);

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text(title).FontColor(MutedText).FontSize(9);
                        r.ConstantItem(10).Height(10).Background(accentColor).AlignMiddle();
                    });

                    col.Item().Text(value).FontSize(16).SemiBold();

                    col.Item().Text(subtitle).FontColor(MutedText).FontSize(8);
                });
        }

        private void ComposeRankingCard(
            IContainer container,
            string title,
            List<UserRow> rows,
            Func<UserRow, string> valueSelector,
            string valueHeader)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(Colors.White)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Text(title).SemiBold().FontSize(12).FontColor(BrandDark);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(18);   // #
                            cols.RelativeColumn();     // name
                            cols.ConstantColumn(62);   // value
                            cols.ConstantColumn(58);   // premium badge
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCellStyle).Text("#");
                            h.Cell().Element(HeaderCellStyle).Text("Erabiltzailea");
                            h.Cell().Element(HeaderCellStyle).AlignRight().Text(valueHeader);
                            h.Cell().Element(HeaderCellStyle).AlignCenter().Text("Premium");
                        });

                        if (rows == null || rows.Count == 0)
                        {
                            table.Cell().ColumnSpan(4).PaddingTop(8).Text("Ez dago daturik.")
                                .FontColor(MutedText);
                            return;
                        }

                        for (int i = 0; i < rows.Count; i++)
                        {
                            var u = rows[i];

                            table.Cell().Element(BodyCellStyle).Text((i + 1).ToString());
                            table.Cell().Element(BodyCellStyle).Text(u.DisplayName);

                            table.Cell().Element(BodyCellStyle).AlignRight().Text(valueSelector(u));

                            table.Cell().Element(BodyCellStyle).AlignCenter().Element(badge =>
                            {
                                var bg = u.IsPremium ? "#DCFCE7" : "#F1F5F9";
                                var fg = u.IsPremium ? Success : MutedText;

                                badge
                                    .Background(bg)
                                    .Border(1).BorderColor(Border)
                                    .PaddingHorizontal(6).PaddingVertical(2)
                                    .AlignCenter()
                                    .Text(u.IsPremium ? "PREM" : "FREE")
                                    .FontSize(8)
                                    .FontColor(fg)
                                    .SemiBold();
                            });
                        }
                    });
                });
        }

        private static IContainer HeaderCellStyle(IContainer c) =>
            c.DefaultTextStyle(t => t.SemiBold().FontSize(9).FontColor("#0F172A"))
             .PaddingVertical(4)
             .BorderBottom(1)
             .BorderColor(Border);

        private static IContainer BodyCellStyle(IContainer c) =>
            c.DefaultTextStyle(t => t.FontSize(9))
             .PaddingVertical(4)
             .BorderBottom(1)
             .BorderColor("#F1F5F9");

        private void ComposePremiumTableCard(IContainer container)
        {
            var total = _data.TotalUsers;
            var premPct = total == 0 ? 0 : (double)_data.PremiumCount / total * 100;
            var freePct = total == 0 ? 0 : (double)_data.NonPremiumCount / total * 100;

            container
                .Border(1).BorderColor(Border)
                .Background(Colors.White)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text("Premium laburpena").SemiBold().FontSize(12).FontColor(BrandDark);

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(50);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderCellStyle).Text("Mota");
                            h.Cell().Element(HeaderCellStyle).AlignRight().Text("Kop.");
                            h.Cell().Element(HeaderCellStyle).AlignRight().Text("%");
                        });

                        AddRow("Premium", _data.PremiumCount, premPct, Success);
                        AddRow("Ez-Premium", _data.NonPremiumCount, freePct, Warning);

                        void AddRow(string label, int count, double pct, string color)
                        {
                            t.Cell().Element(BodyCellStyle).Row(r =>
                            {
                                r.ConstantItem(8).Height(8).Background(color).AlignMiddle();
                                r.RelativeItem().PaddingLeft(6).Text(label);
                            });

                            t.Cell().Element(BodyCellStyle).AlignRight().Text(count.ToString());
                            t.Cell().Element(BodyCellStyle).AlignRight().Text($"{pct:0.#}%");
                        }
                    });
                });
        }

        private void ComposePremiumComparisonBar(IContainer container)
        {
            int total = _data.TotalUsers;
            double premiumRatio = total == 0 ? 0 : (double)_data.PremiumCount / total;
            double nonPremiumRatio = total == 0 ? 0 : (double)_data.NonPremiumCount / total;

            container
                .Border(1).BorderColor(Border)
                .Background(Colors.White)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Text("Premium vs Ez-Premium (barra)").SemiBold().FontSize(12).FontColor(BrandDark);

                    col.Item().Height(18).Row(bar =>
                    {
                        bar.RelativeItem((float)Math.Max(0.1, premiumRatio * 100)).Background(Success);
                        bar.RelativeItem((float)Math.Max(0.1, nonPremiumRatio * 100)).Background("#CBD5E1");
                    });

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Premium: {_data.PremiumCount} ({premiumRatio * 100:0.#}%)").FontColor(MutedText).FontSize(9);
                        r.RelativeItem().AlignRight().Text($"Ez-Premium: {_data.NonPremiumCount} ({nonPremiumRatio * 100:0.#}%)").FontColor(MutedText).FontSize(9);
                    });
                });
        }

        /// <summary>
        /// Bar chart sinplea (TopN): balio max-aren arabera barra proportzionalak marrazten ditu.
        /// </summary>
        private void ComposeBarChartCard(
            IContainer container,
            string title,
            List<UserRow> rows,
            Func<UserRow, double> valueSelector,
            Func<UserRow, string> labelSelector)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(Colors.White)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(6);

                    col.Item().Text(title).SemiBold().FontSize(12).FontColor(BrandDark);

                    if (rows == null || rows.Count == 0)
                    {
                        col.Item().Text("Ez dago daturik.").FontColor(MutedText);
                        return;
                    }

                    double max = rows.Max(valueSelector);
                    if (max <= 0) max = 1;

                    foreach (var u in rows)
                    {
                        var label = labelSelector(u);
                        var value = valueSelector(u);
                        var pct = value / max;

                        col.Item().Row(r =>
                        {
                            r.ConstantItem(110).Text(label).FontSize(9).FontColor("#0F172A");

                            r.RelativeItem().Height(12).Element(bar =>
                            {
                                bar
                                    .Border(1).BorderColor(Border)
                                    .Background("#F1F5F9")
                                    .Padding(1)
                                    .Row(inner =>
                                    {
                                        inner.RelativeItem((float)Math.Max(0.05, pct * 100)).Background(Brand);
                                        inner.RelativeItem((float)Math.Max(0.05, (1 - pct) * 100)).Background("#F1F5F9");
                                    });
                            });

                            r.ConstantItem(40).AlignRight().Text(value.ToString("0")).FontSize(9).FontColor(MutedText);
                        });
                    }
                });
        }
    }
}