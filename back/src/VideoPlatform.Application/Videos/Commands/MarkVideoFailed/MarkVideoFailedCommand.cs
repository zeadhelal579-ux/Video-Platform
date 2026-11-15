// File: MarkVideoFailedCommand.cs
// Purpose: Input for "processing failed permanently" — carries the error
// that will be written to ProcessingJob.ErrorMessage for manual review
// (FLOW.md, Video Processing Flow, step 7).
namespace VideoPlatform.Application.Videos.Commands.MarkVideoFailed;

public sealed record MarkVideoFailedCommand(Guid VideoId, string ErrorMessage);
