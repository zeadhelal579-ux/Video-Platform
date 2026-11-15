// File: JobStatus.cs
// Purpose: Life cycle of a single ProcessingJob (one processing *attempt*),
// tracked independently from the owning Video's own Status
// (see ARCHITECTURE.md, "Why ProcessingJobs is separate from Video.Status").
namespace VideoPlatform.Domain.Enums;

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,

    // Retries were exhausted and RabbitMQ routed the message to its Dead-Letter Exchange.
    DeadLettered = 4
}
