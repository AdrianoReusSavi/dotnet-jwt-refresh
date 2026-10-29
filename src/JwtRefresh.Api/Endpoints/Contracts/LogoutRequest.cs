using System.ComponentModel.DataAnnotations;

namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record LogoutRequest(
    [Required] string RefreshToken
);