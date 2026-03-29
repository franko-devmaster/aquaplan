using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Logging;

public record ClientLogDto(
    [Required] string Level,
    [Required] string Message,
    string? Context,
    DateTime? Timestamp);
