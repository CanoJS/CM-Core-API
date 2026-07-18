namespace MedicalAppointments.Application.Abstractions.Auditing;

public interface IMedicalNoteAuditLog
{
    // docs/SECURITY.md: "Toda búsqueda o lectura de notas médicas deberá generar auditoría."
    // Records that a note was read - never its content.
    void RecordRead(Guid appointmentId, Guid viewerUserId);
}
