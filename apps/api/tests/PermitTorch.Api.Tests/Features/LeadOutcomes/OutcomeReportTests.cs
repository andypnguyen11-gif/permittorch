using PermitTorch.Api.Data;
using PermitTorch.Api.Features.LeadOutcomes;

namespace PermitTorch.Api.Tests.Features.LeadOutcomes;

public class OutcomeReportTests
{
    [Theory]
    [InlineData(100, 9)]
    [InlineData(90, 9)]
    [InlineData(89, 8)]
    [InlineData(60, 6)]
    [InlineData(9, 0)]
    [InlineData(0, 0)]
    public void Bands_are_ten_points_wide_with_100_in_the_top_band(int score, int band) =>
        Assert.Equal(band, OutcomeReport.BandOf(score));

    [Fact]
    public void Ten_bands_are_always_returned_top_first()
    {
        var report = OutcomeReport.Build([]);
        Assert.Equal(10, report.Bands.Count);
        Assert.Equal([90, 80, 70, 60, 50, 40, 30, 20, 10, 0], report.Bands.Select(b => b.Low));
        Assert.Equal(100, report.Bands[0].High);
        Assert.Equal(89, report.Bands[1].High);
        Assert.Equal(9, report.Bands[9].High);
        Assert.Equal(OutcomeReport.MinAnswersPerBand, report.MinAnswersPerBand);
        Assert.Equal(10, OutcomeReport.MinAnswersPerBand);
        Assert.All(report.Bands, b => { Assert.Equal(0, b.Total); Assert.Null(b.WorthContactingShare); });
    }

    [Fact]
    public void Nine_answers_give_counts_but_no_share()
    {
        var answers = Enumerable.Repeat((85, LeadOutcomeKind.WorthContacting), 9);
        var band = OutcomeReport.Build(answers).Bands.Single(b => b.Low == 80);
        Assert.Equal(9, band.Total);
        Assert.Equal(9, band.WorthContacting);
        Assert.Null(band.WorthContactingShare);
    }

    [Fact]
    public void Ten_answers_give_the_worth_contacting_share()
    {
        var answers = Enumerable.Repeat((62, LeadOutcomeKind.WorthContacting), 4)
            .Concat(Enumerable.Repeat((65, LeadOutcomeKind.AlreadyAwarded), 3))
            .Concat(Enumerable.Repeat((60, LeadOutcomeKind.WrongTrade), 2))
            .Append((69, LeadOutcomeKind.TooLate));
        var band = OutcomeReport.Build(answers).Bands.Single(b => b.Low == 60);
        Assert.Equal((4, 3, 2, 1, 10), (band.WorthContacting, band.AlreadyAwarded, band.WrongTrade, band.TooLate, band.Total));
        Assert.Equal(0.4, band.WorthContactingShare);
    }
}
