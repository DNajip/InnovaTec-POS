-- ============================================================
-- REFACTORIZACIÓN FASE 3: CAJA (CAJA) - CORREGIDO
-- ============================================================

USE InnovaTecBD;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. VISTA DE ESTADO DE TURNO ACTUAL
IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'V_ESTADO_TURNO_ACTUAL' AND schema_id = SCHEMA_ID('CAJA'))
    DROP VIEW CAJA.V_ESTADO_TURNO_ACTUAL;
GO

CREATE VIEW CAJA.V_ESTADO_TURNO_ACTUAL AS
SELECT 
    T.ID_TURNO,
    T.ID_USUARIO,
    U.USERNAME,
    T.FECHA_APERTURA,
    T.MONTO_INICIAL_NIO,
    T.MONTO_INICIAL_USD,
    T.TOTAL_EFECTIVO_NIO,
    T.TOTAL_EFECTIVO_USD,
    T.TOTAL_TARJETA,
    T.TOTAL_TRANSFERENCIA,
    T.TOTAL_VENTAS_NIO,
    T.TOTAL_VENTAS_USD,
    (T.MONTO_INICIAL_NIO + T.TOTAL_EFECTIVO_NIO) AS SALDO_TEORICO_NIO,
    (T.MONTO_INICIAL_USD + T.TOTAL_EFECTIVO_USD) AS SALDO_TEORICO_USD
FROM CAJA.TURNOS T
JOIN ADM.USUARIOS U ON T.ID_USUARIO = U.ID_USUARIO
WHERE T.FECHA_CIERRE IS NULL;
GO

-- 2. PROCEDIMIENTO DE GESTIÓN DE TURNOS
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_GestionarTurno' AND schema_id = SCHEMA_ID('CAJA'))
    DROP PROCEDURE CAJA.sp_GestionarTurno;
GO

CREATE PROCEDURE CAJA.sp_GestionarTurno
    @Accion VARCHAR(10), -- 'ABRIR' o 'CERRAR'
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
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdTurnoResult INT;

    BEGIN TRANSACTION;

    IF @Accion = 'ABRIR'
    BEGIN
        IF EXISTS (SELECT 1 FROM CAJA.TURNOS WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL)
            THROW 50005, 'El usuario ya tiene un turno de caja abierto.', 1;

        INSERT INTO CAJA.TURNOS (ID_USUARIO, FECHA_APERTURA, MONTO_INICIAL_NIO, MONTO_INICIAL_USD, TOTAL_VENTAS_NIO, TOTAL_VENTAS_USD, TOTAL_EFECTIVO_NIO, TOTAL_EFECTIVO_USD, TOTAL_TARJETA, TOTAL_TRANSFERENCIA, ID_ESTADO)
        VALUES (@IdUsuario, GETDATE(), @MontoInicialNio, @MontoInicialUsd, 0, 0, 0, 0, 0, 0, 1);
        
        SET @IdTurnoResult = SCOPE_IDENTITY();

        IF @ConteosJson IS NOT NULL
        BEGIN
            INSERT INTO CAJA.CONTEO_DENOMINACIONES (ID_TURNO, ID_DENOMINACION, CANTIDAD, TIPO_CONTEO)
            SELECT @IdTurnoResult, 
                   CAST(JSON_VALUE(c.[value], '$.IdDenominacion') AS INT),
                   CAST(JSON_VALUE(c.[value], '$.Cantidad') AS INT),
                   'APERTURA'
            FROM OPENJSON(@ConteosJson) AS c;
        END
    END
    ELSE IF @Accion = 'CERRAR'
    BEGIN
        IF @IdTurno IS NULL 
            SELECT TOP 1 @IdTurno = ID_TURNO FROM CAJA.TURNOS WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL ORDER BY FECHA_APERTURA DESC;

        IF @IdTurno IS NULL THROW 50006, 'No se encontró un turno abierto para cerrar.', 1;

        DECLARE @SaldoTeoricoNio DECIMAL(12,2), @SaldoTeoricoUsd DECIMAL(12,2);
        
        SELECT @SaldoTeoricoNio = (MONTO_INICIAL_NIO + TOTAL_EFECTIVO_NIO),
               @SaldoTeoricoUsd = (MONTO_INICIAL_USD + TOTAL_EFECTIVO_USD)
        FROM CAJA.TURNOS WHERE ID_TURNO = @IdTurno;

        DECLARE @DiferenciaNio DECIMAL(12,2) = @MontoFinalNio - @SaldoTeoricoNio;
        DECLARE @DiferenciaUsd DECIMAL(12,2) = @MontoFinalUsd - @SaldoTeoricoUsd;

        UPDATE CAJA.TURNOS SET
            FECHA_CIERRE = GETDATE(),
            MONTO_CONTADO_NIO = @MontoFinalNio,
            MONTO_CONTADO_USD = @MontoFinalUsd,
            DIFERENCIA_NIO = @DiferenciaNio,
            DIFERENCIA_USD = @DiferenciaUsd,
            ESTADO_CUADRE = CASE WHEN @DiferenciaNio = 0 AND @DiferenciaUsd = 0 THEN 'CUADRADO' ELSE 'DESCUADRE' END,
            OBSERVACIONES = @Observaciones,
            ID_ESTADO = 2 -- CERRADO
        WHERE ID_TURNO = @IdTurno;

        SET @IdTurnoResult = @IdTurno;

        IF @ConteosJson IS NOT NULL
        BEGIN
            INSERT INTO CAJA.CONTEO_DENOMINACIONES (ID_TURNO, ID_DENOMINACION, CANTIDAD, TIPO_CONTEO)
            SELECT @IdTurno, 
                   CAST(JSON_VALUE(c.[value], '$.IdDenominacion') AS INT),
                   CAST(JSON_VALUE(c.[value], '$.Cantidad') AS INT),
                   'CIERRE'
            FROM OPENJSON(@ConteosJson) AS c;
        END
    END

    COMMIT TRANSACTION;
    
    SELECT * FROM CAJA.TURNOS WHERE ID_TURNO = @IdTurnoResult;
END;
GO

PRINT '>>> Refactorización Fase 3 corregida completada con éxito. <<<';
GO
