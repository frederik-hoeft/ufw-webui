using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Model.V1.Auth;

public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required] string NewPassword);
