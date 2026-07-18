using MedicalAppointments.Domain.Doctors;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IDoctorRepository
{
    Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken);

    void Add(Doctor doctor);

    Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Forces an UPDATE guarded by xmin even when Active won't change value, so a same-state PATCH still detects a stale version.
    void PrepareStatusUpdate(Doctor doctor, uint version);

    // Same rationale as PrepareStatusUpdate, but for SpecialtyId (reassigning to the same specialty).
    void PrepareSpecialtyUpdate(Doctor doctor, uint version);
}
