-- EJECUTAR EN LA BASE FilaCeroDB (no ejecutar CREATE DATABASE de nuevo).
-- Este script es para una base nueva sin estas tablas.
-- Si ya creaste el esquema de la conversación, ejecuta 02_ajustes_sobre_bd_existente.sql.

CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(20) NOT NULL UNIQUE
);
INSERT INTO roles(nombre) VALUES ('admin'), ('usuario');

CREATE TABLE usuarios (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre_completo VARCHAR(100) NOT NULL,
    correo VARCHAR(150) UNIQUE NOT NULL,
    telefono VARCHAR(10),
    contrasena_hash TEXT NOT NULL,
    rol_id INT NOT NULL REFERENCES roles(id),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    creado_en TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE sucursales (
    id VARCHAR(40) PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    ubicacion VARCHAR(180) NOT NULL,
    descripcion TEXT,
    activa BOOLEAN NOT NULL DEFAULT TRUE
);

-- Convención: 1=lunes ... 7=domingo.
CREATE TABLE horarios_sucursal (
    sucursal_id VARCHAR(40) NOT NULL REFERENCES sucursales(id),
    dia_semana INT NOT NULL CHECK (dia_semana BETWEEN 1 AND 7),
    hora_apertura TIME NOT NULL,
    hora_cierre TIME NOT NULL,
    minutos_por_cita INT NOT NULL DEFAULT 30 CHECK (minutos_por_cita > 0),
    PRIMARY KEY (sucursal_id, dia_semana),
    CONSTRAINT horario_orden CHECK (hora_cierre > hora_apertura)
);

CREATE TABLE citas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL REFERENCES usuarios(id),
    sucursal_id VARCHAR(40) NOT NULL REFERENCES sucursales(id),
    fecha DATE NOT NULL,
    hora TIME NOT NULL,
    estado_registro VARCHAR(20) NOT NULL DEFAULT 'Agendada'
        CHECK (estado_registro IN ('Agendada', 'EnProceso', 'Finalizada', 'Cancelada')),
    creada_en TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    cancelada_en TIMESTAMPTZ
);

-- Cancelar libera el horario; EnProceso y Finalizada siguen ocupando su horario histórico.
CREATE UNIQUE INDEX uq_horario_activo
    ON citas(sucursal_id, fecha, hora)
    WHERE estado_registro <> 'Cancelada';

CREATE TABLE triajes (
    cita_id UUID PRIMARY KEY REFERENCES citas(id),
    dolor_pecho BOOLEAN DEFAULT FALSE,
    dificultad_respiratoria BOOLEAN DEFAULT FALSE,
    inconsciente BOOLEAN DEFAULT FALSE,
    fiebre BOOLEAN DEFAULT FALSE,
    dolor_intenso BOOLEAN DEFAULT FALSE,
    mareo BOOLEAN DEFAULT FALSE,
    presion_sistolica INT,
    presion_diastolica INT,
    descripcion VARCHAR(400),
    nivel VARCHAR(30),
    color VARCHAR(10),
    titulo VARCHAR(150),
    mensaje TEXT,
    revisado BOOLEAN DEFAULT FALSE
);

CREATE INDEX ix_citas_usuario ON citas(usuario_id, fecha DESC);
CREATE INDEX ix_citas_fecha ON citas(fecha, sucursal_id);
