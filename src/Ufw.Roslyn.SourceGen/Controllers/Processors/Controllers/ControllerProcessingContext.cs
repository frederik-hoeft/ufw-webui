namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;

internal record ControllerProcessingContext(ControllerProcessor Processor, string ControllerTypeFullName, string? Route, int? Priority);
