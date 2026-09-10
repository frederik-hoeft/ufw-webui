namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;

internal sealed record ControllerProcessingContext(ControllerProcessor Processor, string ControllerTypeFullName, string? Route, int? Priority);
