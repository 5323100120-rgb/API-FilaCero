-- DATOS FICTICIOS PARA PROBAR; sustituye nombres/direcciones por sucursales reales.
INSERT INTO sucursales(id, nombre, ubicacion, descripcion)
VALUES ('demo-centro', 'Sucursal demostración', 'Dirección de ejemplo, no real',
        'Sucursal ficticia para pruebas de API')
ON CONFLICT (id) DO NOTHING;

INSERT INTO horarios_sucursal(sucursal_id,dia_semana,hora_apertura,hora_cierre,minutos_por_cita)
SELECT 'demo-centro', dia, '09:00', '15:00', 30 FROM generate_series(1,5) AS dia
ON CONFLICT (sucursal_id, dia_semana) DO NOTHING;
