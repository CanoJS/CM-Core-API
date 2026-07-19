using MedicalAppointments.Application.Abstractions.Auditing;
using MedicalAppointments.Application.Abstractions.Authentication;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Idempotency;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Infrastructure.Auditing;
using MedicalAppointments.Infrastructure.Authentication;
using MedicalAppointments.Infrastructure.Clock;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using MedicalAppointments.Infrastructure.Persistence.Repositories;
using MedicalAppointments.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAppointments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string clinicTimeZoneId,
        string supabaseProjectUrl,
        IConfiguration configuration)
    {
        TimeZoneInfo clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById(clinicTimeZoneId);

        services.AddSingleton(clinicTimeZone);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IClinicSchedule, ClinicSchedule>();
        services.AddDbContext<MedicalAppointmentsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable("__ef_migrations_history", "medical")));
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<ISpecialtyRepository, SpecialtyRepository>();
        services.AddScoped<IAvailabilityReader, AvailabilityReader>();
        services.AddScoped<IDoctorCatalogReader, DoctorCatalogReader>();
        services.AddScoped<ISpecialtyCatalogReader, SpecialtyCatalogReader>();
        services.AddScoped<IAdminSpecialtyReader, AdminSpecialtyReader>();
        services.AddScoped<IUserProfileReader, UserProfileReader>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IAdminDoctorReader, AdminDoctorReader>();
        services.AddScoped<IAppointmentReader, AppointmentReader>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<IMedicalNoteAuditLog, MedicalNoteAuditLog>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // SecretKey is intentionally optional here: it is only required when a doctor is
        // actually registered, not at startup, so CI and tests can run without it configured.
        services.Configure<SupabaseAuthAdminOptions>(o =>
        {
            o.SecretKey = configuration["Supabase:SecretKey"];
        });
        services.AddHttpClient<IAuthAdminService, SupabaseAuthAdminService>(client =>
        {
            client.BaseAddress = new Uri($"{supabaseProjectUrl.TrimEnd('/')}/auth/v1/");
        });

        return services;
    }
}
