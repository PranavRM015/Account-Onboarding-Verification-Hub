using Admins;
using AuditTrails;
using Customers;
using Data;
using Flags;
using Microsoft.AspNetCore.Mvc;
using MockVendor;
using Tickets;

namespace TicketIntakeNamespace
{
    // Opens the ticket for a customer who is already saved, runs the vendor checks and writes the audit rows. The input form and the
    // pending-customer screen both use it, so a ticket is created the same way whichever way the customer arrived.
    // It runs inside the caller's transaction: on an error the caller just disposes the transaction, and on success it commits.
    public static class TicketIntake
    {
        public static (IActionResult? Error, object? Body) Open(AppDbContext db, Customer customer, string accountType, int? jointCustomerId, string assignedTo)
        {
            var ticket = new Ticket(customer.CustomerId, accountType, assignedTo, jointCustomerId)
            {
                TicketStatus = "Review", // moves straight from Submitted to Review
            };
            var ticketValidation = new TicketValidator().Validate(ticket);
            if (!ticketValidation.IsValid)
            {
                return (new BadRequestObjectResult(string.Join(" ", ticketValidation.Errors.Select(e => e.ErrorMessage))), null);
            }
            db.Tickets.Add(ticket);
            db.SaveChanges(); // generates ticket.TicketId

            var admin = db.Admins.FirstOrDefault(a => a.Username == assignedTo);
            if (admin is null)
            {
                return (new UnauthorizedObjectResult("Your admin account is no longer active."), null);
            }

            var vendor = new Vendor(customer);
            var vendorResponse = vendor.Verify(customer, ticket.TicketId);
            var raisedFlags = new[]
            {
                vendor.CheckVerification(admin, vendorResponse, ticket.TicketId),
                vendor.CheckOFACVerification(admin, customer, ticket.TicketId),
                vendor.CheckCountryTier(admin, customer, ticket.TicketId),
                vendor.CheckIllicitSubstances(admin, customer, ticket.TicketId),
            }.OfType<Flag>().ToList();
            ticket.RiskRating = vendor.FinalRiskRating();
            db.VendorResponses.Add(vendorResponse);
            db.Flags.AddRange(raisedFlags);

            var audit = new AuditTrail(ticket.TicketId, customer.CustomerId, "Submitted", "Review",
                "Ticket created", "Submission received");
            var auditValidation = new AuditTrailValidator().Validate(audit);
            if (!auditValidation.IsValid)
            {
                return (new BadRequestObjectResult(string.Join(" ", auditValidation.Errors.Select(e => e.ErrorMessage))), null);
            }
            db.AuditTrails.Add(audit);
            db.AuditTrails.Add(new AuditTrail(ticket.TicketId, customer.CustomerId, "Review", "Review", "Validation pipeline completed", $"{raisedFlags.Count} flag(s) raised; risk rating {ticket.RiskRating}"));
            db.SaveChanges();

            return (null, new { message = "Customer saved", customer.CustomerId, ticket.TicketId, verified = raisedFlags.Count == 0, riskRating = ticket.RiskRating, flags = raisedFlags.Select(f => f.ViolationType), vendorResponse = new { vendorResponse.Score, vendorResponse.RiskCodes } });
        }
    }
}
