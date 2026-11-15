// File: ConfirmUploadCommand.cs
// Purpose: Input for "the raw file finished uploading to storage" — the
// exact handoff point between "the Api's job" and "the Worker's job"
// (ARCHITECTURE.md section 2.2).
namespace VideoPlatform.Application.Videos.Commands.ConfirmUpload;

public sealed record ConfirmUploadCommand(Guid VideoId, Guid RequesterId);
