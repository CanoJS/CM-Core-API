using MedicalAppointments.Application.Abstractions.Auditing;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Messaging;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;

namespace MedicalAppointments.Application.Appointments.GetAppointmentById;

public sealed class GetAppointmentByIdQueryHandler(
    ICurrentUser currentUser,
    IDoctorRepository doctorRepository,
    IAppointmentReader appointmentReader,
    IMedicalNoteAuditLog medicalNoteAuditLog)
    : IQueryHandler<GetAppointmentByIdQuery, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(
        GetAppointmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        AppointmentListItem item = await appointmentReader.GetByIdAsync(query.AppointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment does not exist.");

        // A missing appointment and one that exists but isn't visible to this caller return the
        // exact same 404, so the response never reveals whether a given id belongs to someone
        // else.
        switch (currentUser.Role)
        {
            case UserRole.Patient:
                if (item.PatientId != currentUser.UserId)
                {
                    throw new NotFoundException("The appointment does not exist.");
                }

                break;
            case UserRole.Doctor:
                Doctor doctor = await doctorRepository.GetByUserIdAsync(currentUser.UserId, cancellationToken)
                    ?? throw new NotFoundException("The appointment does not exist.");
                if (item.DoctorId != doctor.Id)
                {
                    throw new NotFoundException("The appointment does not exist.");
                }

                break;
            case UserRole.Admin:
                break;
        }

        AppointmentResponse response = AppointmentResponseMapper.ToResponse(item, currentUser.Role);
        if (response.MedicalNote is not null)
        {
            medicalNoteAuditLog.RecordRead(item.Id, currentUser.UserId);
        }

        return response;
    }
}
