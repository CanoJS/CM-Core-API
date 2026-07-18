namespace MedicalAppointments.Application.Common.Exceptions;

public sealed class AuthServiceException(string message) : Exception(message);
