using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Rules.Authoring;

internal interface IRuleValidationMessageLocalizer
{
    string Localize(ModelValidationError error);
}
