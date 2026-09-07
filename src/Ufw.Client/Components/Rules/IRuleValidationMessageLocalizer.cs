using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Components.Rules;

internal interface IRuleValidationMessageLocalizer
{
    string Localize(ModelValidationError error);
}
