# Convenciones de API

- Prefijo de versión: `/api/v1`.
- JSON en `camelCase`, nombres del contrato en inglés.
- UUID serializado como `string`.
- Instantes en ISO 8601 UTC, por ejemplo `2026-07-20T15:30:00Z`.
- Fechas sin hora en `YYYY-MM-DD`.
- Errores con RFC 9457 `ProblemDetails`.
- `401`: token ausente o inválido.
- `403`: identidad válida sin permiso.
- `404`: recurso inexistente o no visible para el usuario.
- `409`: bloque ocupado, versión obsoleta o reintento incompatible.

OpenAPI generado por la API es la fuente de verdad. Los modelos Angular y Dart deben
adaptarse o generarse desde ese documento; no definen el modelo de almacenamiento.
