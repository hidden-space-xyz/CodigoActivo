# anon-ratings — estado
Base: HEAD 1f5e84f, árbol limpio. Riesgo ALTO (datos personales, migración, contrato API).

## Problema
event_ratings.user_id + índice único (event_id,user_id) + created_at/updated_at vinculan autor y contenido.
GET history devuelve MyRating (editable) → exige vínculo.

## Diseño (decidido por orquestador)
- Nueva entidad EventRatingSubmission(EventId, UserId): PK compuesta, sin Id ni timestamps; cascade event/user.
- EventRating: sin UserId/User/CreatedAt/UpdatedAt; Id Guid v4 aleatorio.
- Envío único e inmutable (editar es incompatible con anonimato real).
- POST /api/events/{id}/rating -> 204; duplicado -> 409 EventRatingAlreadySubmitted (también ante carrera por PK).
- History: MyRating -> HasRated bool. List item sin fechas; sort solo score, default -score, tie Id.
- Migración: poblar submissions en orden aleatorio, drop columnas, CLUSTER ambas tablas para reescribir heap
  (DROP COLUMN no borra datos físicos y el orden físico correlaciona).
- Riesgo residual documentado: xmin misma transacción, WAL, backups previos, logs de PostgreSQL.
- Borrado de cuenta: marcador se borra en cascada; valoración anónima permanece.

## Ronda 1 (hecha): backend 1499/1499, npm run check verde, docs SECURITY/DEPLOYMENT.
## Revisión: code-reviewer + security-reviewer CHANGES_REQUESTED
- P1 xmin compartido (misma tx) y orden físico → vínculo en filas nuevas; docs inexactas.
- P2 Down falla con datos; k-anonimato no documentado; migración conserva xmin cronológico de ratings.
- P3 ClearAllPools en test; logs HTTP = nginx IP/UA, no usuario.
## Ronda 2 decisión
- Shuffle-on-write: en una tx con lock por evento, insertar y reescribir TODAS las filas del evento en ambas
  tablas (DELETE…RETURNING + INSERT ORDER BY random()) → xmin único por evento, orden físico aleatorio.
- Migración: reescribir event_ratings aleatoriamente antes de CLUSTER. Down → NotSupportedException.
- k-umbral en admin: NO implementado (decisión de producto) → documentar y proponer.

## Fases
1. implementer backend  2. implementer frontend  3. test-runner  4. code-reviewer + security-reviewer  5. docs
