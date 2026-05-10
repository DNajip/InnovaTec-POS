-- ============================================================
-- SCRIPT DE REPARACIÓN: Configuración de QUOTED_IDENTIFIER
-- Objetivo: Asegurar que los objetos críticos tengan el ajuste correcto
--           para operar con índices filtrados.
-- ============================================================

USE InnovaTecBD;
GO

PRINT 'Reparando V_ESTADO_TURNO_ACTUAL...';
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'V_ESTADO_TURNO_ACTUAL' AND schema_id = SCHEMA_ID('CAJA'))
    DROP VIEW CAJA.V_ESTADO_TURNO_ACTUAL;
GO
CREATE VIEW CAJA.V_ESTADO_TURNO_ACTUAL AS
SELECT 
    T.*,
    U.USERNAME,
    (T.MONTO_INICIAL_NIO + T.TOTAL_EFECTIVO_NIO + ISNULL((SELECT SUM(MONTO) FROM CAJA.MOVIMIENTOS_VARIOS WHERE ID_TURNO = T.ID_TURNO AND ID_MONEDA = 1), 0)) AS SALDO_TEORICO_NIO,
    (T.MONTO_INICIAL_USD + T.TOTAL_EFECTIVO_USD + ISNULL((SELECT SUM(MONTO) FROM CAJA.MOVIMIENTOS_VARIOS WHERE ID_TURNO = T.ID_TURNO AND ID_MONEDA = 2), 0)) AS SALDO_TEORICO_USD
FROM CAJA.TURNOS T
JOIN ADM.USUARIOS U ON T.ID_USUARIO = U.ID_USUARIO
WHERE T.FECHA_CIERRE IS NULL;
GO

PRINT 'Reparando sp_GestionarTurno...';
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_GestionarTurno' AND schema_id = SCHEMA_ID('CAJA'))
    DROP PROCEDURE CAJA.sp_GestionarTurno;
GO
CREATE PROCEDURE CAJA.sp_GestionarTurno
    @Accion VARCHAR(10),
    @IdUsuario INT,
    @IdTurno INT = NULL,
    @MontoInicialNio DECIMAL(12,2) = 0,
    @MontoInicialUsd DECIMAL(12,2) = 0,
    @MontoFinalNio DECIMAL(12,2) = 0,
    @MontoFinalUsd DECIMAL(12,2) = 0,
    @Observaciones NVARCHAR(MAX) = NULL,
    @ConteosJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @IdTurnoResult INT;
    BEGIN TRANSACTION;

    IF @Accion = 'ABRIR'
    BEGIN
        IF EXISTS (SELECT 1 FROM CAJA.TURNOS WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL)
            THROW 50005, 'El usuario ya tiene un turno de caja abierto.', 1;

        INSERT INTO CAJA.TURNOS (ID_USUARIO, FECHA_APERTURA, MONTO_INICIAL_NIO, MONTO_INICIAL_USD, TOTAL_VENTAS_NIO, TOTAL_VENTAS_USD, TOTAL_EFECTIVO_NIO, TOTAL_EFECTIVO_USD, TOTAL_TARJETA, TOTAL_TRANSFERENCIA, ID_ESTADO)
        VALUES (@IdUsuario, GETDATE(), @MontoInicialNio, @MontoInicialUsd, 0, 0, 0, 0, 0, 0, 1);
        SET @IdTurnoResult = SCOPE_IDENTITY();
        
        -- Guardar desglose inicial si existe
        IF @ConteosJson IS NOT NULL
        BEGIN
            INSERT INTO CAJA.CONTEO_DENOMINACIONES (ID_TURNO, ID_DENOMINACION, CANTIDAD, TIPO_CONTEO)
            SELECT @IdTurnoResult, CAST(JSON_VALUE([value], '$.IdDenominacion') AS INT), CAST(JSON_VALUE([value], '$.Cantidad') AS INT), 'APERTURA'
            FROM OPENJSON(@ConteosJson);
        END
    END
    ELSE IF @Accion = 'CERRAR'
    BEGIN
        IF @IdTurno IS NULL SELECT TOP 1 @IdTurno = ID_TURNO FROM CAJA.TURNOS WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL ORDER BY FECHA_APERTURA DESC;
        IF @IdTurno IS NULL THROW 50006, 'No se encontró un turno abierto para cerrar.', 1;

        -- Calcular diferencias basándose en el saldo teórico (Apertura + Ventas en Efectivo + Movimientos Manuales)
        DECLARE @TeoricoNio DECIMAL(18,2), @TeoricoUsd DECIMAL(18,2);
        SELECT @TeoricoNio = (MONTO_INICIAL_NIO + TOTAL_EFECTIVO_NIO + ISNULL((SELECT SUM(MONTO) FROM CAJA.MOVIMIENTOS_VARIOS WHERE ID_TURNO = @IdTurno AND ID_MONEDA = 1), 0)),
               @TeoricoUsd = (MONTO_INICIAL_USD + TOTAL_EFECTIVO_USD + ISNULL((SELECT SUM(MONTO) FROM CAJA.MOVIMIENTOS_VARIOS WHERE ID_TURNO = @IdTurno AND ID_MONEDA = 2), 0))
        FROM CAJA.TURNOS WHERE ID_TURNO = @IdTurno;

        UPDATE CAJA.TURNOS SET 
            FECHA_CIERRE = GETDATE(), 
            MONTO_CONTADO_NIO = @MontoFinalNio, 
            MONTO_CONTADO_USD = @MontoFinalUsd, 
            DIFERENCIA_NIO = @MontoFinalNio - @TeoricoNio,
            DIFERENCIA_USD = @MontoFinalUsd - @TeoricoUsd,
            OBSERVACIONES = @Observaciones, 
            ID_ESTADO = 2 
        WHERE ID_TURNO = @IdTurno;

        -- Guardar desglose final si existe
        IF @ConteosJson IS NOT NULL
        BEGIN
            INSERT INTO CAJA.CONTEO_DENOMINACIONES (ID_TURNO, ID_DENOMINACION, CANTIDAD, TIPO_CONTEO)
            SELECT @IdTurno, CAST(JSON_VALUE([value], '$.IdDenominacion') AS INT), CAST(JSON_VALUE([value], '$.Cantidad') AS INT), 'CIERRE'
            FROM OPENJSON(@ConteosJson);
        END

        SET @IdTurnoResult = @IdTurno;
    END

    COMMIT TRANSACTION;
    SELECT * FROM CAJA.TURNOS WHERE ID_TURNO = @IdTurnoResult;
END;
GO

PRINT 'Verificando resultados...';
SELECT name, uses_quoted_identifier 
FROM sys.sql_modules m 
JOIN sys.objects o ON m.object_id = o.object_id 
WHERE o.name IN ('V_ESTADO_TURNO_ACTUAL', 'sp_GestionarTurno');
GO
