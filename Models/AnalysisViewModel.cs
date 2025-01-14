namespace BellPepperMVC.Models
{
    public class AnalysisViewModel
    {
        public IFormFile? ImageFile { get; set; }
        public bool RequestDetailedAnalysis { get; set; }
        public BellPepperImage? AnalysisResult { get; set; }
        public Dictionary<string, string>? FeatureDescriptions { get; set; }

        // Statistical context
        public decimal? AverageConfidence { get; set; }
        public Dictionary<string, int>? MaturityLevelDistribution { get; set; }

        // User context
        public List<BellPepperImage>? RecentAnalyses { get; set; }
        public int TotalAnalysesCount { get; set; }
    }

    public class DashboardViewModel
    {
        public List<BellPepperImage> RecentAnalyses { get; set; }
        public Dictionary<string, int> MaturityDistribution { get; set; }
        public int TotalAnalyses { get; set; }
        public int TotalUsers { get; set; }
        public List<ChartDataPoint> AnalysisOverTime { get; set; }
        public Dictionary<string, decimal> AverageConfidenceByMaturity { get; set; }
    }
    public class ChartDataPoint
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}
