using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Orders;

// Polish F-220 — PreleveurId is mandatory for an assignment.
public record OrderAssignDto([Required] string PreleveurId);
