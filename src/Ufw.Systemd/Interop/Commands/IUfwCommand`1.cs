namespace Ufw.Systemd.Interop.Commands;

internal interface IUfwCommand<TResult> : IUfwCommand
{
    ValueTask<TResult?> GetResultAsync(CancellationToken cancellationToken);
}
