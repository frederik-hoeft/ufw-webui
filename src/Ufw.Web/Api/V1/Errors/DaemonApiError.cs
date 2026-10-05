using Microsoft.AspNetCore.Mvc;

namespace Ufw.Web.Api.V1.Errors;

internal sealed record DaemonApiError(int StatusCode, ProblemDetails Problem);
