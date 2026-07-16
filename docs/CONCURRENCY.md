# Concurrencia e idempotencia

La comprobación previa de disponibilidad mejora el mensaje al usuario, pero no evita una
carrera por sí sola. PostgreSQL garantiza la regla final mediante el índice parcial único
`ux_appointments_doctor_slot_scheduled`.

Flujo al reservar:

1. La query comprueba que el bloque parece libre.
2. El command crea la cita dentro de una unidad de trabajo.
3. PostgreSQL acepta una sola cita `SCHEDULED` por médico y hora.
4. Una violación única se traduce a `409 Conflict`.

Las actualizaciones usan `xmin` como token de concurrencia optimista. Cancelar, atender o
reagendar con una versión obsoleta también debe responder `409`.

La tabla `medical.idempotency_requests` reserva una llave única por usuario y operación. El
middleware que persiste/reproduce respuestas idempotentes se añadirá antes de habilitar los
commands de cancelación y reagendamiento.
