namespace Ufw.Ipc.Shared.Model;

public readonly record struct MessageHeader(string? Method, string Context);
