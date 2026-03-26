namespace WindowThumbWall.Tests;

public sealed class GridLayoutScorerTests
{
    [Fact]
    public void EvaluateCandidate_SubtractsTitleBarAreaFromThumbnailArea()
    {
        GridCandidateMetrics metrics = GridLayoutScorer.EvaluateCandidate(
            count: 100,
            rows: 100,
            cols: 1,
            wallWidth: 1000,
            wallHeight: 1000,
            aspectRatios: [1.0],
            titleBarHeight: 20,
            horizontalChrome: 6,
            verticalChrome: 6);

        Assert.Equal(994, metrics.ThumbnailWidth, precision: 6);
        Assert.Equal(0, metrics.ThumbnailHeight, precision: 6);
        Assert.Equal(1, metrics.Deadspace, precision: 6);
    }

    [Fact]
    public void EvaluateCandidate_UsesThumbnailViewportAspectForDistortion()
    {
        GridCandidateMetrics metrics = GridLayoutScorer.EvaluateCandidate(
            count: 4,
            rows: 2,
            cols: 2,
            wallWidth: 1200,
            wallHeight: 800,
            aspectRatios: [1.5],
            titleBarHeight: 20,
            horizontalChrome: 6,
            verticalChrome: 6);

        Assert.True(metrics.Distortion > 0);
    }
}
