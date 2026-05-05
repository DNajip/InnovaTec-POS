-- ============================================================
-- REFACTORIZACIÓN FASE 2: VENTAS (VEN) - CORREGIDO
-- ============================================================

USE InnovaTecBD;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. VISTA DE HISTORIAL DE VENTAS
IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'V_HISTORIAL_VENTAS' AND schema_id = SCHEMA_ID('VEN'))
    DROP VIEW VEN.V_HISTORIAL_VENTAS;
GO

CREATE VIEW VEN.V_HISTORIAL_VENTAS AS
SELECT 
    V.ID_VENTA,
    V.NUMERO_FACTURA,
    V.FECHA_VENTA,
    U.USERNAME AS CAJERO,
    COALESCE(P.NOMBRE_COMPLETO, 'CLIENTE GENERAL') AS CLIENTE,
    V.SUBTOTAL_NIO,
    V.DESCUENTO_NIO,
    V.TOTAL_NIO,
    V.TASA_CAMBIO_USD,
    (V.TOTAL_NIO / V.TASA_CAMBIO_USD) AS TOTAL_USD,
    V.ANULADA,
    V.ID_TURNO,
    V.ID_USUARIO
FROM VEN.VENTAS V
JOIN ADM.USUARIOS U ON V.ID_USUARIO = U.ID_USUARIO
LEFT JOIN ADM.PERSONAS P ON V.ID_PERSONA = P.ID_PERSONA;
GO

-- 2. PROCEDIMIENTO MAESTRO DE CHECKOUT
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_ProcesarVenta' AND schema_id = SCHEMA_ID('VEN'))
    DROP PROCEDURE VEN.sp_ProcesarVenta;
GO

CREATE PROCEDURE VEN.sp_ProcesarVenta
    @IdUsuario INT,
    @IdPersona INT = NULL,
    @DescuentoNio DECIMAL(12,2) = 0,
    @TasaCambioUsd DECIMAL(12,4) = 36.60,
    @ItemsJson NVARCHAR(MAX),
    @PaymentsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IdTurno INT;
    DECLARE @IdVenta INT;
    DECLARE @FechaActual DATETIME = GETDATE();

    -- 1. Validar Turno Activo
    SELECT TOP 1 @IdTurno = ID_TURNO 
    FROM CAJA.TURNOS 
    WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL 
    ORDER BY FECHA_APERTURA DESC;

    IF @IdTurno IS NULL THROW 50002, 'Debe abrir un turno de caja antes de facturar.', 1;

    BEGIN TRANSACTION;

    -- 2. Calcular Totales desde JSON
    DECLARE @SubtotalNio DECIMAL(12,2);
    SELECT @SubtotalNio = SUM(CAST(JSON_VALUE(item.[value], '$.SubTotal') AS DECIMAL(12,2)))
    FROM OPENJSON(@ItemsJson) AS item;

    DECLARE @TotalVentaNio DECIMAL(12,2) = @SubtotalNio - @DescuentoNio;

    -- 3. Insertar Encabezado de Venta (NUMERO_FACTURA es computado)
    INSERT INTO VEN.VENTAS (ID_TURNO, ID_USUARIO, ID_PERSONA, FECHA_VENTA, TASA_CAMBIO_USD, SUBTOTAL_NIO, DESCUENTO_NIO, TOTAL_NIO, ANULADA)
    VALUES (@IdTurno, @IdUsuario, @IdPersona, @FechaActual, @TasaCambioUsd, @SubtotalNio, @DescuentoNio, @TotalVentaNio, 0);
    
    SET @IdVenta = SCOPE_IDENTITY();

    -- Obtener el número de factura generado
    DECLARE @NumFactura NVARCHAR(50);
    SELECT @NumFactura = NUMERO_FACTURA FROM VEN.VENTAS WHERE ID_VENTA = @IdVenta;

    -- 4. Procesar Pagos y Actualizar Turno
    INSERT INTO VEN.PAGOS (ID_VENTA, ID_METODO_PAGO, MONTO_PAGADO, TASA_APLICADA, MONTO_EN_NIO, COD_REFERENCIA, FECHA_PAGO)
    SELECT @IdVenta, 
           CAST(JSON_VALUE(p.[value], '$.IdMetodoPago') AS INT),
           CAST(JSON_VALUE(p.[value], '$.Monto') AS DECIMAL(12,2)),
           CAST(JSON_VALUE(p.[value], '$.TasaCambio') AS DECIMAL(12,4)),
           CAST(JSON_VALUE(p.[value], '$.MontoEnNio') AS DECIMAL(12,2)),
           JSON_VALUE(p.[value], '$.Referencia'),
           @FechaActual
    FROM OPENJSON(@PaymentsJson) AS p;

    -- Actualizar balances del turno
    UPDATE T SET 
        T.TOTAL_EFECTIVO_NIO += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) 
                                       FROM OPENJSON(@PaymentsJson) pj 
                                       JOIN VEN.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT)
                                       JOIN ADM.MONEDAS m ON m.ID_MONEDA = mp.ID_MONEDA
                                       WHERE mp.NOMBRE LIKE '%EFECTIVO%' AND m.CODIGO = 'NIO'), 0),
        T.TOTAL_EFECTIVO_USD += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.Monto') AS DECIMAL(12,2))) 
                                       FROM OPENJSON(@PaymentsJson) pj 
                                       JOIN VEN.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT)
                                       JOIN ADM.MONEDAS m ON m.ID_MONEDA = mp.ID_MONEDA
                                       WHERE mp.NOMBRE LIKE '%EFECTIVO%' AND m.CODIGO = 'USD'), 0),
        T.TOTAL_TARJETA += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) 
                                   FROM OPENJSON(@PaymentsJson) pj 
                                   JOIN VEN.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT)
                                   WHERE mp.NOMBRE LIKE '%TARJETA%'), 0),
        T.TOTAL_TRANSFERENCIA += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) 
                                         FROM OPENJSON(@PaymentsJson) pj 
                                         JOIN VEN.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT)
                                         WHERE mp.NOMBRE LIKE '%TRANSFERENCIA%'), 0),
        T.TOTAL_VENTAS_NIO += @TotalVentaNio,
        T.TOTAL_VENTAS_USD += (@TotalVentaNio / @TasaCambioUsd)
    FROM CAJA.TURNOS T
    WHERE T.ID_TURNO = @IdTurno;

    -- 5. Procesar Detalles, Garantías e IMEIs
    DECLARE @ItemCursor CURSOR;
    DECLARE @IdProducto INT, @Qty INT, @UnitPrice DECIMAL(12,2), @SubTotalItem DECIMAL(12,2), @DescItem NVARCHAR(150), @ReqImei BIT;
    DECLARE @DetailsJson NVARCHAR(MAX);

    SET @ItemCursor = CURSOR FOR 
        SELECT CAST(JSON_VALUE(item.[value], '$.IdProducto') AS INT),
               CAST(JSON_VALUE(item.[value], '$.Quantity') AS INT),
               CAST(JSON_VALUE(item.[value], '$.UnitPrice') AS DECIMAL(12,2)),
               CAST(JSON_VALUE(item.[value], '$.SubTotal') AS DECIMAL(12,2)),
               JSON_VALUE(item.[value], '$.Description'),
               CAST(JSON_VALUE(item.[value], '$.RequiresImei') AS BIT),
               JSON_QUERY(item.[value], '$.Details')
        FROM OPENJSON(@ItemsJson) AS item;

    OPEN @ItemCursor;
    FETCH NEXT FROM @ItemCursor INTO @IdProducto, @Qty, @UnitPrice, @SubTotalItem, @DescItem, @ReqImei, @DetailsJson;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @DetCursor CURSOR;
        DECLARE @IdPeriodo INT, @ImeiStr NVARCHAR(100);
        
        SET @DetCursor = CURSOR FOR 
            SELECT CAST(JSON_VALUE(d.[value], '$.IdPeriodoGarantia') AS INT),
                   JSON_VALUE(d.[value], '$.Imei')
            FROM OPENJSON(@DetailsJson) AS d;
        
        OPEN @DetCursor;
        FETCH NEXT FROM @DetCursor INTO @IdPeriodo, @ImeiStr;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            DECLARE @Meses INT = 0;
            SELECT @Meses = MESES FROM INV.PERIODOS_GARANTIA WHERE ID_PERIODO = @IdPeriodo;
            DECLARE @FechaVence DATE = CASE WHEN @Meses > 0 THEN DATEADD(MONTH, @Meses, @FechaActual) ELSE NULL END;

            INSERT INTO VEN.VENTA_DETALLE (ID_VENTA, ID_PRODUCTO, DESCRIPCION_SNAP, CANTIDAD, PRECIO_UNITARIO_NIO, SUBTOTAL_NIO, ID_PERIODO_GARANTIA, FECHA_VENCE_GARANTIA)
            VALUES (@IdVenta, @IdProducto, @DescItem, 1, @UnitPrice, @UnitPrice, @IdPeriodo, @FechaVence);
            
            DECLARE @IdDetalle INT = SCOPE_IDENTITY();

            DECLARE @IdImei INT = NULL;
            IF @ReqImei = 1
            BEGIN
                IF @ImeiStr IS NULL THROW 50003, 'Se requiere IMEI para un producto configurado.', 1;
                SELECT @IdImei = ID_IMEI FROM INV.EQUIPOS_IMEI WHERE ID_PRODUCTO = @IdProducto AND IMEI = @ImeiStr;
                IF @IdImei IS NULL
                BEGIN
                    INSERT INTO INV.EQUIPOS_IMEI (ID_PRODUCTO, IMEI, ESTADO_IMEI, FECHA_INGRESO, INGRESADO_POR)
                    VALUES (@IdProducto, @ImeiStr, 'VENDIDO', @FechaActual, @IdUsuario);
                    SET @IdImei = SCOPE_IDENTITY();
                END
                ELSE
                BEGIN
                    IF EXISTS (SELECT 1 FROM INV.EQUIPOS_IMEI WHERE ID_IMEI = @IdImei AND ESTADO_IMEI = 'VENDIDO')
                        THROW 50004, 'Uno de los IMEIs ya fue vendido.', 1;
                    UPDATE INV.EQUIPOS_IMEI SET ESTADO_IMEI = 'VENDIDO' WHERE ID_IMEI = @IdImei;
                END
                INSERT INTO VEN.VENTA_DETALLE_IMEI (ID_DETALLE, ID_EQUIPO_IMEI, IMEI_SNAP)
                VALUES (@IdDetalle, @IdImei, @ImeiStr);
            END

            IF @FechaVence IS NOT NULL AND @IdPersona IS NOT NULL
            BEGIN
                INSERT INTO INV.GARANTIAS (ID_DETALLE_VENTA, ID_PERSONA, ID_PRODUCTO, ID_EQUIPO_IMEI, MESES_GARANTIA, FECHA_INICIO, FECHA_VENCIMIENTO, ESTADO_GARANTIA)
                VALUES (@IdDetalle, @IdPersona, @IdProducto, @IdImei, @Meses, CAST(@FechaActual AS DATE), @FechaVence, 'ACTIVA');
            END

            FETCH NEXT FROM @DetCursor INTO @IdPeriodo, @ImeiStr;
        END
        CLOSE @DetCursor;
        DEALLOCATE @DetCursor;

        UPDATE INV.PRODUCTOS SET STOCK_ACTUAL -= @Qty WHERE ID_PRODUCTO = @IdProducto;
        INSERT INTO INV.MOVIMIENTOS (ID_PRODUCTO, ID_TIPO_MOV, CANTIDAD, ID_REFERENCIA, TABLA_REFERENCIA, OBSERVACION, FECHA_MOV, REGISTRADO_POR)
        VALUES (@IdProducto, 2, -@Qty, @IdVenta, 'VEN.VENTAS', 'Venta ' + @NumFactura, @FechaActual, @IdUsuario);

        FETCH NEXT FROM @ItemCursor INTO @IdProducto, @Qty, @UnitPrice, @SubTotalItem, @DescItem, @ReqImei, @DetailsJson;
    END
    CLOSE @ItemCursor;
    DEALLOCATE @ItemCursor;

    COMMIT TRANSACTION;

    SELECT * FROM VEN.V_HISTORIAL_VENTAS WHERE ID_VENTA = @IdVenta;
END;
GO

PRINT '>>> Refactorización Fase 2 corregida completada con éxito. <<<';
GO
