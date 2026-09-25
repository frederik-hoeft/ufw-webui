using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Model.V1.Auth;

public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
