namespace MedicalAppointments.Application.Abstractions.Messaging;

public interface ICommand<out TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
