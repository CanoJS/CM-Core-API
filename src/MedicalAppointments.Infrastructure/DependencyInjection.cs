using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Abstractions.Scheduling;
using MedicalAppointments.Infrastructure.Clock;
using MedicalAppointments.Infrastructure.Persistence;
using MedicalAppointments.Infrastructure.Persistence.Readers;
using MedicalAppointments.Infrastructure.Persistence.Repositories;
using MedicalAppointments.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAppointments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string clinicTimeZoneId)
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
        services.AddScoped<IAvailabilityReader, AvailabilityReader>();
        services.AddScoped<IDoctorCatalogReader, DoctorCatalogReader>();
        services.AddScoped<ISpecialtyCatalogReader, SpecialtyCatalogReader>();
        services.AddScoped<IUserProfileReader, UserProfileReader>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
