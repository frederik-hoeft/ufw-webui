using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal interface IRuleValidationMessageLocalizer
{
    string Localize(ModelValidationError error);
}
