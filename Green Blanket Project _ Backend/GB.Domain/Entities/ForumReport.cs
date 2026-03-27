using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GB.Domain.Entities
{
    public class ForumReport
    {
        [Key] // Tells EF Core this is the Primary Key
        public int ReportForumId { get; set; }

        public int ReportOptionsId { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public DateTime Date { get; set; }
    }
}