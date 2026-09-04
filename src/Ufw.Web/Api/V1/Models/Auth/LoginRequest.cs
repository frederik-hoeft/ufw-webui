using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Api.V1.Models.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);
