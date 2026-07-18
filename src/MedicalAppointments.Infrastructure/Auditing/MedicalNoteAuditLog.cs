using MedicalAppointments.Application.Abstractions.Auditing;
using Microsoft.Extensions.Logging;

namespace MedicalAppointments.Infrastructure.Auditing;

public sealed partial class MedicalNoteAuditLog(ILogger<MedicalNoteAuditLog> logger) : IMedicalNoteAuditLog
{
    public void RecordRead(Guid appointmentId, Guid viewerUserId) =>
        LogMedicalNoteRead(logger, appointmentId, viewerUserId);

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Medical note read for appointment {AppointmentId} by user {ViewerUserId}")]
    private static partial void LogMedicalNoteRead(ILogger logger, Guid appointmentId, Guid viewerUserId);
}
