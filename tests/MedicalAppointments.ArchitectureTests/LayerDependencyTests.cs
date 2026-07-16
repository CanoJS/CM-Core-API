using System.Reflection;
using MedicalAppointments.Application.Appointments.CreateAppointment;
using MedicalAppointments.Domain.Appointments;
using MedicalAppointments.Infrastructure.Persistence;

namespace MedicalAppointments.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotReferenceOuterLayers()
    {
        Assembly assembly = typeof(Appointment).Assembly;

        AssertDoesNotReference(assembly, "MedicalAppointments.Application");
        AssertDoesNotReference(assembly, "MedicalAppointments.Infrastructure");
        AssertDoesNotReference(assembly, "MedicalAppointments.Api");
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureOrApi()
    {
        Assembly assembly = typeof(CreateAppointmentCommand).Assembly;

        AssertDoesNotReference(assembly, "MedicalAppointments.Infrastructure");
        AssertDoesNotReference(assembly, "MedicalAppointments.Api");
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        Assembly assembly = typeof(MedicalAppointmentsDbContext).Assembly;

        AssertDoesNotReference(assembly, "MedicalAppointments.Api");
    }

    private static void AssertDoesNotReference(Assembly assembly, string forbiddenAssembly)
    {
        string[] references = assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        Assert.DoesNotContain(forbiddenAssembly, references);
    }
}
