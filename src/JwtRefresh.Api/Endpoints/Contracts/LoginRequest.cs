using System.ComponentModel.DataAnnotations;

namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);