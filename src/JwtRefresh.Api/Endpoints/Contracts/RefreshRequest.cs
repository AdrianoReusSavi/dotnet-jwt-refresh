using System.ComponentModel.DataAnnotations;

namespace JwtRefresh.Api.Endpoints.Contracts;

public sealed record RefreshRequest(
    [Required] string RefreshToken
);