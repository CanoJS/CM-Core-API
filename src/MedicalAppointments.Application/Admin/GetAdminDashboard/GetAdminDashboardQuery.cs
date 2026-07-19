using MedicalAppointments.Application.Abstractions.Messaging;

namespace MedicalAppointments.Application.Admin.GetAdminDashboard;

public sealed record GetAdminDashboardQuery : IQuery<AdminDashboardResponse>;

public sealed record AdminDashboardResponse(int ScheduledToday);
