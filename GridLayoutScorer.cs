namespace WindowThumbWall;

internal readonly record struct GridCandidateMetrics(
    double ThumbnailWidth,
    double ThumbnailHeight,
    double Deadspace,
    double Distortion);

internal static class GridLayoutScorer
{
    internal static (int rows, int cols) ChooseGrid(
        int count,
        double wallWidth,
        double wallHeight,
        IReadOnlyList<double> aspectRatios,
        double titleBarHeight,
        double horizontalChrome,
        double verticalChrome,
        double deadspaceWeight,
        double distortionWeight)
    {
        if (count <= 0)
            return (1, 1);

        double bestScore = double.PositiveInfinity;
        double bestDeadspace = double.PositiveInfinity;
        int bestRows = 1;
        int bestCols = count;

        for (int rows = 1; rows <= count; rows++)
        {
            int cols = (int)Math.Ceiling((double)count / rows);
            GridCandidateMetrics candidate = EvaluateCandidate(
                count,
                rows,
                cols,
                wallWidth,
                wallHeight,
                aspectRatios,
                titleBarHeight,
                horizontalChrome,
                verticalChrome);

            double score = deadspaceWeight * candidate.Deadspace + distortionWeight * candidate.Distortion;
            bool isBetter = score < bestScore - 0.000001;
            bool tieButLessDeadspace = Math.Abs(score - bestScore) <= 0.000001 && candidate.Deadspace < bestDeadspace;

            if (isBetter || tieButLessDeadspace)
            {
                bestScore = score;
                bestDeadspace = candidate.Deadspace;
                bestRows = rows;
                bestCols = cols;
            }
        }

        return (bestRows, bestCols);
    }

    internal static GridCandidateMetrics EvaluateCandidate(
        int count,
        int rows,
        int cols,
        double wallWidth,
        double wallHeight,
        IReadOnlyList<double> aspectRatios,
        double titleBarHeight,
        double horizontalChrome,
        double verticalChrome)
    {
        double cellWidth = wallWidth / Math.Max(cols, 1);
        double cellHeight = wallHeight / Math.Max(rows, 1);
        double thumbnailWidth = Math.Max(cellWidth - horizontalChrome, 0);
        double thumbnailHeight = Math.Max(cellHeight - verticalChrome - titleBarHeight, 0);
        double coverage =
            wallWidth <= 0 || wallHeight <= 0
                ? 0
                : (thumbnailWidth * thumbnailHeight * count) / (wallWidth * wallHeight);
        double deadspace = 1.0 - Math.Clamp(coverage, 0.0, 1.0);

        double distortion = 0;
        if (aspectRatios.Count > 0)
        {
            double thumbnailAspect = Math.Max(thumbnailWidth, 0.01) / Math.Max(thumbnailHeight, 0.01);
            for (int i = 0; i < aspectRatios.Count; i++)
            {
                double ratio = thumbnailAspect / Math.Max(aspectRatios[i], 0.01);
                double delta = Math.Log(ratio);
                distortion += delta * delta;
            }
            distortion /= aspectRatios.Count;
        }

        return new GridCandidateMetrics(thumbnailWidth, thumbnailHeight, deadspace, distortion);
    }
}
