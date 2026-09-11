using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Systemd.Api.Framework;

internal interface IApiExceptionMapper
{
    InternalServerErrorResponse Map(Exception exception);
}
