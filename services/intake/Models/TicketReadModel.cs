using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intake.Models;

// Temporary: maps only the columns Intake needs to check whether a customer
// already has a ticket. This exists because both services share the same database
// during the migration period. Once Review owns a separate database, this model
// and the Tickets DbSet in IntakeDbContext should be removed and replaced with
// an HTTP call to the Review service.
public class TicketReadModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TicketId { get; set; }

    public int CustomerId { get; set; }
}
