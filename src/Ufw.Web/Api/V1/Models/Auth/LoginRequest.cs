using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Api.V1.Models.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);
