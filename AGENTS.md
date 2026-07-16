# Reglas del backend

Antes de editar código, leer `ARCHITECTURE.md`.

- No referenciar Infrastructure o Api desde Application o Domain.
- No inyectar `MedicalAppointmentsDbContext` en endpoints o handlers.
- Implementar escrituras como commands y lecturas como queries.
- No exponer entidades de dominio o EF en el contrato HTTP.
- Agregar pruebas para cada regla de negocio o corrección.
- Mantener UUID, UTC y los nombres JSON definidos en `docs/API-CONVENTIONS.md`.
- No debilitar restricciones de base de datos, RLS ni validación JWT para resolver errores.
- Ejecutar format, build, test y pruebas de arquitectura antes de considerar terminado un cambio.
