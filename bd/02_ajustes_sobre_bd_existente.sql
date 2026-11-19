-- EJECUTA ESTO SOLAMENTE SI YA CREASTE LAS 6 TABLAS de la conversación
-- y tu estructura coincide con la versión UUID de usuarios.
-- Antes de aplicar una migración a datos reales, haz un respaldo.

BEGIN;

INSERT INTO roles(nombre) VALUES ('usuario'), ('admin')
ON CONFLICT (nombre) DO NOTHING;

-- Mantiene las citas de EnProceso/Finalizada reservadas e impide otros estados.
ALTER TABLE citas ALTER COLUMN estado_registro SET NOT NULL;
ALTER TABLE citas DROP CONSTRAINT IF EXISTS ck_citas_estado;
ALTER TABLE citas ADD CONSTRAINT ck_citas_estado
    CHECK (estado_registro IN ('Agendada','EnProceso','Finalizada','Cancelada'));

DROP INDEX IF EXISTS uq_horario_activo;
CREATE UNIQUE INDEX uq_horario_activo
    ON citas(sucursal_id, fecha, hora)
    WHERE estado_registro <> 'Cancelada';

CREATE INDEX IF NOT EXISTS ix_citas_usuario ON citas(usuario_id, fecha DESC);
CREATE INDEX IF NOT EXISTS ix_citas_fecha ON citas(fecha, sucursal_id);

COMMIT;
