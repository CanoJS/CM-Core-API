using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using MedicalAppointments.Application.Abstractions.Clock;
using MedicalAppointments.Application.Abstractions.Idempotency;
using MedicalAppointments.Application.Abstractions.Persistence;
using MedicalAppointments.Application.Abstractions.Queries;
using MedicalAppointments.Application.Common.Exceptions;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Domain.Doctors;
using MedicalAppointments.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAppointments.IntegrationTests;

public sealed class AppointmentEndpointTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid OtherPatientId = Guid.NewGuid();
    private static readonly Guid DoctorUserId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid OtherDoctorUserId = Guid.NewGuid();
    private static readonly Guid OtherDoctorId = Guid.NewGuid();
    private static readonly Guid SpecialtyId = Guid.NewGuid();

    // 2026-07-20 is a Monday (verified against a real calendar).
    private static readonly DateTimeOffset ClockNow = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FutureSlot = new(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAppointment_WithoutAccessToken_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/appointments",
            new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("DOCTOR")]
    [InlineData("ADMIN")]
    public async Task CreateAppointment_WithNonPatientToken_ReturnsForbidden(string role)
    {
        Fixture fixture = BuildFixture();
        using HttpClient client = CreateAuthenticatedClient(role, PatientId, fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/appointments",
            new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_WithPatientToken_ReturnsCreatedWithExpectedJsonShape()
    {
        Fixture fixture = BuildFixture();
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/appointments",
            new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(PatientId.ToString(), body.RootElement.GetProperty("patientId").GetString());
        Assert.Equal(DoctorId.ToString(), body.RootElement.GetProperty("doctorId").GetString());
        Assert.Equal("SCHEDULED", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("version").ValueKind);
    }

    [Fact]
    public async Task CreateAppointment_WhenSlotIsTaken_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        fixture.Appointments.SeedScheduled(OtherPatientId, DoctorId, FutureSlot);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/appointments",
            new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_WithSameIdempotencyKeyTwice_ReturnsSameAppointment()
    {
        Fixture fixture = BuildFixture();
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);
        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" }),
        };
        request1.Headers.Add("Idempotency-Key", "test-key-1");

        HttpResponseMessage first = await client.SendAsync(request1, CancellationToken.None);
        using JsonDocument firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        string firstId = firstBody.RootElement.GetProperty("id").GetString()!;

        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(new { doctorId = DoctorId, startsAt = FutureSlot, reason = "Control anual" }),
        };
        request2.Headers.Add("Idempotency-Key", "test-key-1");
        HttpResponseMessage second = await client.SendAsync(request2, CancellationToken.None);
        using JsonDocument secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstId, secondBody.RootElement.GetProperty("id").GetString());
        Assert.Equal(1, fixture.Appointments.AddCount);
    }

    [Fact]
    public async Task GetMyAppointments_AsPatient_ReturnsOnlyOwnAppointmentsWithExpectedJsonShape()
    {
        Fixture fixture = BuildFixture();
        Guid ownAppointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot);
        fixture.Appointments.SeedScheduled(OtherPatientId, DoctorId, FutureSlot.AddDays(1));
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.GetAsync("/api/v1/appointments", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement item = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(ownAppointmentId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("Ana López", item.GetProperty("patientName").GetString());
        Assert.Equal("Carlos Ruiz", item.GetProperty("doctorName").GetString());
        Assert.Equal("Cardiología", item.GetProperty("specialtyName").GetString());
        Assert.True(item.TryGetProperty("endsAt", out _));
    }

    [Fact]
    public async Task GetMyAppointments_AsDoctorWithPatientNameFilter_ReturnsOnlyMatchingOwnAppointments()
    {
        Fixture fixture = BuildFixture();
        Guid matching = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot);
        fixture.Appointments.SeedScheduled(OtherPatientId, DoctorId, FutureSlot.AddDays(1));
        fixture.Appointments.SeedScheduled(PatientId, OtherDoctorId, FutureSlot.AddDays(2));
        using HttpClient client = CreateAuthenticatedClient("DOCTOR", DoctorUserId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/appointments?patientName=ana",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement item = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(matching.ToString(), item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetMyAppointments_AsDoctorWithPatientNameFilter_IncludesMedicalNoteFromPastAttendedAppointment()
    {
        Fixture fixture = BuildFixture();
        DateTimeOffset pastSlot = ClockNow.AddHours(-3);
        Guid attendedId = fixture.Appointments.SeedAttended(
            PatientId,
            DoctorId,
            pastSlot,
            "Paciente estable, sin hallazgos.");
        using HttpClient client = CreateAuthenticatedClient("DOCTOR", DoctorUserId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/appointments?patientName=ana",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement item = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(attendedId.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("ATTENDED", item.GetProperty("status").GetString());
        Assert.Equal("Paciente estable, sin hallazgos.", item.GetProperty("medicalNote").GetString());
    }

    [Fact]
    public async Task GetMyAppointments_AsPatientWithPatientNameFilter_IgnoresFilter()
    {
        Fixture fixture = BuildFixture();
        Guid ownAppointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/appointments?patientName=no-such-name",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement item = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(ownAppointmentId.ToString(), item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetMyAppointments_WithInvalidStatus_ReturnsBadRequest()
    {
        Fixture fixture = BuildFixture();
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/appointments?status=NOT_A_STATUS",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentById_WhenNotOwnedByPatient_ReturnsNotFound()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(OtherPatientId, DoctorId, FutureSlot);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/appointments/{appointmentId}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAppointmentById_WhenNotAssignedToDoctor_ReturnsNotFound()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(PatientId, OtherDoctorId, FutureSlot);
        using HttpClient client = CreateAuthenticatedClient("DOCTOR", DoctorUserId, fixture);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/appointments/{appointmentId}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelAppointment_WithMalformedVersion_ReturnsBadRequest()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/cancel",
            new { version = "not-a-number" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelAppointment_WithStaleVersion_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(
            PatientId,
            DoctorId,
            ClockNow.AddDays(3),
            version: 5);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/cancel",
            new { version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CancelAppointment_AsOwningPatientMoreThan24HoursAhead_ReturnsOk()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(
            PatientId,
            DoctorId,
            ClockNow.AddDays(3),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/cancel",
            new { version = "1" },
            CancellationToken.None);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("CANCELLED", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RescheduleAppointment_WithNonAdminToken_ReturnsForbidden()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot, version: 1);
        using HttpClient client = CreateAuthenticatedClient("PATIENT", PatientId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/reschedule",
            new { doctorId = OtherDoctorId, startsAt = FutureSlot.AddDays(1), version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_WhenSlotIsOccupied_ReturnsConflict()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot, version: 1);
        fixture.Appointments.SeedScheduled(OtherPatientId, OtherDoctorId, FutureSlot.AddDays(1));
        using HttpClient client = CreateAuthenticatedClient("ADMIN", Guid.NewGuid(), fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/reschedule",
            new { doctorId = OtherDoctorId, startsAt = FutureSlot.AddDays(1), version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_WithValidData_ReturnsOkWithNewDoctorAndTime()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(PatientId, DoctorId, FutureSlot, version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", Guid.NewGuid(), fixture);
        DateTimeOffset newSlot = FutureSlot.AddDays(1);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/reschedule",
            new { doctorId = OtherDoctorId, startsAt = newSlot, version = "1" },
            CancellationToken.None);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OtherDoctorId.ToString(), body.RootElement.GetProperty("doctorId").GetString());
    }

    [Fact]
    public async Task AttendAppointment_WithNonDoctorToken_ReturnsForbidden()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(
            PatientId,
            DoctorId,
            ClockNow.AddHours(-1),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("ADMIN", Guid.NewGuid(), fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/attend",
            new { medicalNote = "Paciente estable.", version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AttendAppointment_WhenNotAssignedToDoctor_ReturnsNotFound()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(
            PatientId,
            OtherDoctorId,
            ClockNow.AddHours(-1),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("DOCTOR", DoctorUserId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/attend",
            new { medicalNote = "Paciente estable.", version = "1" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AttendAppointment_AsAssignedDoctor_ReturnsOkWithMedicalNote()
    {
        Fixture fixture = BuildFixture();
        Guid appointmentId = fixture.Appointments.SeedScheduled(
            PatientId,
            DoctorId,
            ClockNow.AddHours(-1),
            version: 1);
        using HttpClient client = CreateAuthenticatedClient("DOCTOR", DoctorUserId, fixture);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/v1/appointments/{appointmentId}/attend",
            new { medicalNote = "Paciente estable.", version = "1" },
            CancellationToken.None);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ATTENDED", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Paciente estable.", body.RootElement.GetProperty("medicalNote").GetString());
    }

    private static Fixture BuildFixture() => new()
    {
        Appointments = new InMemoryAppointmentRepository(),
        Doctors = new InMemoryDoctorRepositoryForAppointments(),
        UserProfiles = new InMemoryUserProfileRepositoryForAppointments(),
    };

    private static HttpClient CreateAuthenticatedClient(string role, Guid userId, Fixture fixture)
    {
        var factory = new WebApplicationFactory<Program>();
        WebApplicationFactory<Program> testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                    })
                    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.Scheme,
                        options =>
                        {
                            options.Role = role;
                            options.UserId = userId;
                        });

                services.RemoveAll<IDoctorRepository>();
                services.AddSingleton<IDoctorRepository>(fixture.Doctors);

                services.RemoveAll<IAppointmentRepository>();
                services.AddSingleton<IAppointmentRepository>(fixture.Appointments);

                services.RemoveAll<IAppointmentReader>();
                services.AddSingleton<IAppointmentReader>(fixture.Appointments);

                services.RemoveAll<IUserProfileRepository>();
                services.AddSingleton<IUserProfileRepository>(fixture.UserProfiles);

                services.RemoveAll<IIdempotencyStore>();
                services.AddSingleton<IIdempotencyStore>(new InMemoryIdempotencyStore());

                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(ClockNow));

                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton<IUnitOfWork>(new InMemoryUnitOfWork(fixture.Appointments));

                services.AddLogging(logging => logging.ClearProviders());
            }));

        return testFactory.CreateClient();
    }

    private sealed class Fixture
    {
        public required InMemoryAppointmentRepository Appointments { get; set; }

        public required InMemoryDoctorRepositoryForAppointments Doctors { get; set; }

        public required InMemoryUserProfileRepositoryForAppointments UserProfiles { get; set; }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class InMemoryDoctorRepositoryForAppointments : IDoctorRepository
    {
        private readonly Dictionary<Guid, Doctor> byId = new()
        {
            [DoctorId] = new Doctor(DoctorId, DoctorUserId, SpecialtyId),
            [OtherDoctorId] = new Doctor(OtherDoctorId, OtherDoctorUserId, SpecialtyId),
        };

        public Task<bool> IsActiveAsync(Guid doctorId, CancellationToken cancellationToken) =>
            Task.FromResult(byId.ContainsKey(doctorId));

        public void Add(Doctor doctor) => byId[doctor.Id] = doctor;

        public Task<Doctor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(byId.GetValueOrDefault(id));

        public Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(byId.Values.FirstOrDefault(doctor => doctor.UserId == userId));

        public void PrepareStatusUpdate(Doctor doctor, uint version)
        {
        }

        public void PrepareSpecialtyUpdate(Doctor doctor, uint version)
        {
        }
    }

    // Only PatientId ever calls Create/Cancel as PATIENT in this file (Admin/Doctor callers never
    // trigger the Active lookup), so a single active profile is enough to keep every existing
    // "golden path" scenario passing under the new Active gate.
    private sealed class InMemoryUserProfileRepositoryForAppointments : IUserProfileRepository
    {
        private readonly Dictionary<Guid, UserProfile> byId = new()
        {
            [PatientId] = new UserProfile(PatientId, "Ana", "López", "ana@example.com", UserRole.Patient),
        };

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(byId.GetValueOrDefault(id));
    }

    /// <summary>
    /// Mimics the real repository's xmin semantics for HTTP-level status-code assertions - a
    /// pending write only "commits" (and bumps the version) when the submitted version matches
    /// the stored one. Also implements IAppointmentReader with fixed test patient/doctor/
    /// specialty names, so list/detail endpoints can be exercised without a database.
    /// </summary>
    private sealed class InMemoryAppointmentRepository : IAppointmentRepository, IAppointmentReader
    {
        private readonly Dictionary<Guid, Appointment> appointments = [];
        private readonly Dictionary<Guid, uint> versions = [];
        private Appointment? pendingAdd;
        private Guid? pendingId;
        private uint? pendingSubmittedVersion;

        public int AddCount { get; private set; }

        public Guid SeedScheduled(
            Guid patientId,
            Guid doctorId,
            DateTimeOffset startsAt,
            uint version = 1)
        {
            Appointment appointment = Appointment.Schedule(patientId, doctorId, startsAt, "Control anual", ClockNow.AddDays(-1));
            appointments[appointment.Id] = appointment;
            versions[appointment.Id] = version;
            return appointment.Id;
        }

        // Seeds a past, already-ATTENDED appointment (with a medical note) directly through the
        // domain entity - bypassing the HTTP attend flow entirely - so history/search tests can
        // assert medicalNote is present without exercising AttendAppointment separately.
        public Guid SeedAttended(
            Guid patientId,
            Guid doctorId,
            DateTimeOffset startsAt,
            string medicalNote,
            uint version = 1)
        {
            Appointment appointment = Appointment.Schedule(patientId, doctorId, startsAt, "Control anual", ClockNow.AddDays(-1));
            appointment.Attend(medicalNote, startsAt.AddMinutes(Appointment.DurationMinutes));
            appointments[appointment.Id] = appointment;
            versions[appointment.Id] = version;
            return appointment.Id;
        }

        public Task<bool> HasScheduledAppointmentAsync(
            Guid doctorId,
            DateTimeOffset startsAt,
            Guid? excludeAppointmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(appointments.Values.Any(appointment =>
                appointment.DoctorId == doctorId
                && appointment.StartsAt == startsAt.ToUniversalTime()
                && appointment.Status == AppointmentStatus.Scheduled
                && appointment.Id != excludeAppointmentId));

        public void Add(Appointment appointment)
        {
            pendingAdd = appointment;
            AddCount++;
        }

        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(appointments.GetValueOrDefault(id));

        public void PrepareStatusUpdate(Appointment appointment, uint version)
        {
            pendingId = appointment.Id;
            pendingSubmittedVersion = version;
        }

        public void PrepareRescheduleUpdate(Appointment appointment, uint version)
        {
            pendingId = appointment.Id;
            pendingSubmittedVersion = version;
        }

        public bool TryCommitPendingChanges()
        {
            if (pendingAdd is { } added)
            {
                appointments[added.Id] = added;
                versions[added.Id] = 1;
                pendingAdd = null;
                return true;
            }

            if (pendingId is not { } id || pendingSubmittedVersion is not { } submitted)
            {
                return true;
            }

            if (versions[id] != submitted)
            {
                return false;
            }

            versions[id] = submitted + 1;
            pendingId = null;
            pendingSubmittedVersion = null;
            return true;
        }

        Task<IReadOnlyList<AppointmentListItem>> IAppointmentReader.GetAsync(
            Guid? patientId,
            Guid? doctorId,
            AppointmentStatus? status,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtcExclusive,
            string? patientNameContains,
            CancellationToken cancellationToken)
        {
            IEnumerable<Appointment> query = appointments.Values;
            if (patientId is { } patient)
            {
                query = query.Where(a => a.PatientId == patient);
            }

            if (doctorId is { } doctor)
            {
                query = query.Where(a => a.DoctorId == doctor);
            }

            if (status is { } appointmentStatus)
            {
                query = query.Where(a => a.Status == appointmentStatus);
            }

            if (!string.IsNullOrWhiteSpace(patientNameContains))
            {
                query = query.Where(a =>
                    GetPatientName(a.PatientId).FullName.Contains(
                        patientNameContains, StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<AppointmentListItem> items = query
                .OrderBy(a => a.StartsAt)
                .Select(ToItem)
                .ToArray();
            return Task.FromResult(items);
        }

        private static (string FirstName, string LastName, string FullName) GetPatientName(Guid patientId)
        {
            (string firstName, string lastName) = patientId == PatientId
                ? ("Ana", "López")
                : ("Otra", "Paciente");
            return (firstName, lastName, $"{firstName} {lastName}");
        }

        Task<AppointmentListItem?> IAppointmentReader.GetByIdAsync(Guid appointmentId, CancellationToken cancellationToken) =>
            Task.FromResult(appointments.TryGetValue(appointmentId, out Appointment? appointment)
                ? ToItem(appointment)
                : null);

        private AppointmentListItem ToItem(Appointment appointment)
        {
            (string firstName, string lastName, _) = GetPatientName(appointment.PatientId);
            (string doctorFirstName, string doctorLastName) = appointment.DoctorId == DoctorId
                ? ("Carlos", "Ruiz")
                : ("Sofía", "Vega");

            return new AppointmentListItem(
                appointment.Id,
                appointment.PatientId,
                firstName,
                lastName,
                appointment.DoctorId,
                doctorFirstName,
                doctorLastName,
                SpecialtyId,
                "Cardiología",
                appointment.StartsAt,
                appointment.EndsAt,
                appointment.Status,
                appointment.Reason,
                appointment.MedicalNote,
                appointment.CreatedAt,
                appointment.UpdatedAt,
                versions.GetValueOrDefault(appointment.Id, appointment.Version));
        }
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<(Guid, string, string), IdempotencyRecord> records = [];

        public Task<IdempotencyRecord?> FindAsync(
            Guid userId,
            string operation,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(records.GetValueOrDefault((userId, operation, idempotencyKey)));

        public void Stage(
            Guid userId,
            string operation,
            string idempotencyKey,
            string requestHash,
            int responseStatus,
            string responseBody,
            DateTimeOffset expiresAt) =>
            records[(userId, operation, idempotencyKey)] = new IdempotencyRecord(requestHash, responseStatus, responseBody);
    }

    private sealed class InMemoryUnitOfWork(InMemoryAppointmentRepository repository) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!repository.TryCommitPendingChanges())
            {
                throw new ConflictException("The resource was changed by another request. Refresh and try again.");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public string Role { get; set; } = "PATIENT";

        public Guid UserId { get; set; } = Guid.NewGuid();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<TestAuthenticationSchemeOptions>(options, logger, encoder)
    {
        public new const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims =
            [
                new Claim("sub", Options.UserId.ToString()),
                new Claim("user_role", Options.Role),
            ];
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, ClaimTypes.Name, "user_role"));
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
