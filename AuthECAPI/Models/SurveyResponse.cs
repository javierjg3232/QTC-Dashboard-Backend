namespace AuthECAPI.Models
{
    public class SurveyResponse
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public DateTime SurveyDate { get; set; }
        public string FileName { get; set; }  // Store file name for reference
        public string UploadedBy { get; set; } // Optional: Track who uploaded
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
