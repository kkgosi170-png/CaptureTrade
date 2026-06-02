using TradeCapture.Api.Models;

namespace TradeCapture.Api.Services;

/// <summary>Thrown when an incoming trade fails business validation. Maps to HTTP 400.</summary>
public sealed class TradeValidationException : Exception
{
    public TradeValidationException(IReadOnlyList<string> errors)
        : base("Validation failed.") => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Thrown when no currency rate is available to enrich a trade. Maps to HTTP 422.</summary>
public sealed class RateNotFoundException : Exception
{
    public RateNotFoundException(string from, string to, DateOnly asOf)
        : base($"No currency rate found for {from}->{to} as of {asOf:yyyy-MM-dd}.") { }
}
