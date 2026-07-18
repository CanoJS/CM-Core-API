namespace MedicalAppointments.Application.Common.Exceptions;

public sealed class AuthServiceUnavailableException(string message) : Exception(message);
