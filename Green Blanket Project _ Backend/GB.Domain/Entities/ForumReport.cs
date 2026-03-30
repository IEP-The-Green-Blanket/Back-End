using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GB.Domain.Entities
{
    [Table("report_forum", Schema = "hartbeespoortdam")] // <--- THIS IS THE FIX
    public class ForumReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("report_forum_id")]
        public int ReportForumId { get; set; }

        [Column("report_options_id")]
        public int ReportOptionsId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("location")]
        public string Location { get; set; } = string.Empty;

        [Column("date")]
        public DateTime Date { get; set; } = DateTime.UtcNow;
    }
}