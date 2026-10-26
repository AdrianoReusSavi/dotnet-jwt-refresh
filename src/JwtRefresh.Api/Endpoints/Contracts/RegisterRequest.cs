using System.ComponentModel.DataAnnotations;

namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);