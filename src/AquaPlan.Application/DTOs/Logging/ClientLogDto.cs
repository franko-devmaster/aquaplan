using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Logging;

public record ClientLogDto(
    // Sprint Robustesse F-114 — cap field lengths so a client cannot flood the Serilog
    // sinks with oversized payloads.
    [Required][StringLength(16)] string Level,
    [Required][StringLength(4000)] string Message,
    [StringLength(256)] string? Context,
    DateTime? Timestamp);
