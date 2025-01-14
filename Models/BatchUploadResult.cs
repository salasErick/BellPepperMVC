namespace BellPepperMVC.Models
{
    public class BatchUploadResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public int? AnalysisId { get; set; }
        public string Error { get; set; }
    }
}