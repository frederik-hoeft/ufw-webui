using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Model.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class TrimmedStringLengthAttribute(int maximumLength) : ValidationAttribute
{
    public int MaximumLength { get; } = maximumLength;

    public int MinimumLength { get; init; }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }
        if (value is not string text)
        {
            return false;
        }

        int length = text.Trim().Length;
        return length >= MinimumLength && length <= MaximumLength;
    }

    public override string FormatErrorMessage(string name) =>
        MinimumLength > 0
            ? $"The field {name} must be a string with a trimmed length between {MinimumLength} and {MaximumLength}."
            : $"The field {name} must be a string with a trimmed length of at most {MaximumLength}.";
}
