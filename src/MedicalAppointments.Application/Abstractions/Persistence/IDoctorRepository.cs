using MedicalAppointments.Domain.Doctors;

namespace MedicalAppointments.Application.Abstractions.Persistence;

public interface IDoctorRepository
{
    Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken);

    void Add(Doctor doctor);

    Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // Resolves the authenticated user's id (JWT `sub`, medical.user_profiles.id) to their
    // doctor profile. The two ids are never the same value by coincidence - callers must not
    // assume a doctor's own userId equals their doctors.id. Default returns "not found" so
    // existing repository fakes that never exercise the DOCTOR-scoped appointment paths don't
    // need to implement it.
    Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<Doctor?>(null);

    // Forces an UPDATE guarded by xmin even when Active won't change value, so a same-state PATCH still detects a stale version.
    void PrepareStatusUpdate(Doctor doctor, uint version);

    // Same rationale as PrepareStatusUpdate, but for SpecialtyId (reassigning to the same specialty).
    void PrepareSpecialtyUpdate(Doctor doctor, uint version);
}
