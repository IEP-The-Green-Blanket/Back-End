using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GB.Domain.Entities
{
    [Table("report_forum")] // Tells EF the exact lowercase table name
    public class ForumReport
    {
        [Key]
        [Column("report_forum_id")] // Maps directly to the pgAdmin column
        public int ReportForumId { get; set; }

        [Column("report_options_id")]
        public int ReportOptionsId { get; set; }

        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("location")]
        public string Location { get; set; } = string.Empty;

        [Column("date")]
        public DateTime Date { get; set; }
    }
}