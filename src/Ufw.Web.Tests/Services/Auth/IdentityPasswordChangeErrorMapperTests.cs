using Microsoft.AspNetCore.Identity;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class IdentityPasswordChangeErrorMapperTests
{
    [TestMethod]
    public void Map_PasswordMismatch_TargetsCurrentPassword()
    {
        IdentityError[] errors = [new() { Code = "PasswordMismatch", Description = "Incorrect password." }];

        IReadOnlyList<PasswordChangeValidationError> mapped = IdentityPasswordChangeErrorMapper.Map(errors);

        Assert.HasCount(1, mapped);
        Assert.AreEqual(PasswordChangeValidationField.CurrentPassword, mapped[0].Field);
        Assert.AreEqual("Incorrect password.", mapped[0].ErrorMessage);
    }

    [TestMethod]
    [DataRow("PasswordRequiresDigit")]
    [DataRow("PasswordRequiresLower")]
    [DataRow("PasswordRequiresNonAlphanumeric")]
    [DataRow("PasswordRequiresUniqueChars")]
    [DataRow("PasswordRequiresUpper")]
    [DataRow("PasswordTooShort")]
    [DataRow("CustomPasswordValidator")]
    public void Map_NewPasswordAndUnknownErrors_TargetReplacementPassword(string code)
    {
        IdentityError[] errors = [new() { Code = code, Description = "Rejected." }];

        IReadOnlyList<PasswordChangeValidationError> mapped = IdentityPasswordChangeErrorMapper.Map(errors);

        Assert.HasCount(1, mapped);
        Assert.AreEqual(PasswordChangeValidationField.NewPassword, mapped[0].Field);
    }
}
