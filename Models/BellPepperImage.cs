using BellPepperMVC.Areas.Identity.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BellPepperMVC.Models
{
    public class BellPepperImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public byte[] Image { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [Required]
        public string FileName { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [Required]
        public string ContentType { get; set; }

        [Required]
        public DateTime UploadDate { get; set; }

        [Column(TypeName = "nvarchar(50)")]
        [Required]
        public string PredictedMaturityLevel { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PredictionConfidence { get; set; }

        public bool HasDetailedAnalysis { get; set; }

        // Detailed analysis results
        public byte[]? ProcessedImage { get; set; }
        public byte[]? SpectrumR { get; set; }
        public byte[]? SpectrumG { get; set; }
        public byte[]? SpectrumB { get; set; }
        public byte[]? SpectrumCombined { get; set; }
        public byte[]? InverseFFT { get; set; }
        public byte[]? SobelH1 { get; set; }
        public byte[]? SobelH2 { get; set; }

        // Feature values
        public decimal MaxValue { get; set; }
        public decimal MinValue { get; set; }
        public decimal StdValue { get; set; }
        public decimal MeanValue { get; set; }
        public decimal MedianValue { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
