using Microsoft.AspNetCore.Mvc;

namespace Ufw.Web.Api.V1.Errors;

public sealed record DaemonApiError(int StatusCode, ProblemDetails Problem);
