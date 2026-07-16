
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_ProcesarVenta' AND schema_id = SCHEMA_ID('VEN'))
    DROP PROCEDURE VEN.sp_ProcesarVenta;
GO
SET QUOTED_IDENTIFIER ON;
GO
SET ANSI_NULLS ON;
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
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @IdTurno INT, @IdVenta INT, @FechaActual DATETIME = GETDATE();
    
    -- 1. Verificar turno abierto
    SELECT TOP 1 @IdTurno = ID_TURNO FROM CAJA.TURNOS 
    WHERE ID_USUARIO = @IdUsuario AND FECHA_CIERRE IS NULL 
    ORDER BY FECHA_APERTURA DESC;
    
    IF @IdTurno IS NULL THROW 50002, 'Debe abrir un turno de caja antes de facturar.', 1;

    BEGIN TRANSACTION;
    
    -- 2. Calcular totales
    DECLARE @SubtotalNio DECIMAL(12,2);
    SELECT @SubtotalNio = SUM(CAST(JSON_VALUE(item.[value], '$.SubTotal') AS DECIMAL(12,2))) FROM OPENJSON(@ItemsJson) AS item;
    DECLARE @TotalVentaNio DECIMAL(12,2) = @SubtotalNio - @DescuentoNio;

    -- 3. Insertar Venta
    INSERT INTO VEN.VENTAS (ID_TURNO, ID_USUARIO, ID_PERSONA, FECHA_VENTA, TASA_CAMBIO_USD, SUBTOTAL_NIO, DESCUENTO_NIO, TOTAL_NIO, ANULADA)
    VALUES (@IdTurno, @IdUsuario, @IdPersona, @FechaActual, @TasaCambioUsd, @SubtotalNio, @DescuentoNio, @TotalVentaNio, 0);
    SET @IdVenta = SCOPE_IDENTITY();

    -- 4. Insertar Pagos y calcular Vuelto Total
    DECLARE @TotalPagadoNio DECIMAL(12,2) = 0;
    SELECT @TotalPagadoNio = SUM(CAST(JSON_VALUE(p.[value], '$.MontoEnNio') AS DECIMAL(12,2))) FROM OPENJSON(@PaymentsJson) AS p;
    
    DECLARE @VueltoTotalNio DECIMAL(12,2) = CASE WHEN @TotalPagadoNio > @TotalVentaNio THEN @TotalPagadoNio - @TotalVentaNio ELSE 0 END;

    -- Lógica de Auditoría: ¿El vuelto proviene de un pago electrónico?
    IF @VueltoTotalNio > 0
    BEGIN
        IF EXISTS (
            SELECT 1 FROM OPENJSON(@PaymentsJson) pj 
            JOIN CAT.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT)
            WHERE mp.NOMBRE NOT LIKE '%EFECTIVO%'
        )
        BEGIN
            DECLARE @NotaAuto NVARCHAR(200) = 'Vuelto de C$ ' + CAST(@VueltoTotalNio AS NVARCHAR(20)) + ' entregado en efectivo por sobrepago en método no-efectivo.';
            UPDATE VEN.VENTAS SET OBSERVACION = ISNULL(OBSERVACION + ' | ', '') + @NotaAuto WHERE ID_VENTA = @IdVenta;
        END
    END

    -- Insertar registros de pagos con detalle de recibido/vuelto
    -- Para simplificar, asignamos el vuelto al último pago en efectivo, o al último pago si no hay efectivo
    DECLARE @UltimoIdPago INT;
    
    INSERT INTO VEN.PAGOS (ID_VENTA, ID_METODO_PAGO, MONTO_PAGADO, TASA_APLICADA, MONTO_EN_NIO, MONTO_RECIBIDO, VUELTO_NIO, COD_REFERENCIA, FECHA_PAGO)
    SELECT @IdVenta, 
           CAST(JSON_VALUE(p.[value], '$.IdMetodoPago') AS INT), 
           CAST(JSON_VALUE(p.[value], '$.Monto') AS DECIMAL(12,2)), 
           CAST(JSON_VALUE(p.[value], '$.TasaCambio') AS DECIMAL(12,4)), 
           CAST(JSON_VALUE(p.[value], '$.MontoEnNio') AS DECIMAL(12,2)),
           CAST(JSON_VALUE(p.[value], '$.MontoEnNio') AS DECIMAL(12,2)), -- Monto recibido en NIO
           0, -- Vuelto se asignará después al pago que corresponda
           JSON_VALUE(p.[value], '$.Referencia'), 
           @FechaActual
    FROM OPENJSON(@PaymentsJson) AS p;

    -- Si hay vuelto, se lo asignamos al pago que lo generó (el último que se procesó)
    IF @VueltoTotalNio > 0
    BEGIN
        SELECT TOP 1 @UltimoIdPago = ID_PAGO FROM VEN.PAGOS WHERE ID_VENTA = @IdVenta ORDER BY ID_PAGO DESC;
        UPDATE VEN.PAGOS SET VUELTO_NIO = @VueltoTotalNio WHERE ID_PAGO = @UltimoIdPago;
    END

    -- 5. Actualizar Saldos de Caja (Turno)
    UPDATE T SET 
        T.TOTAL_EFECTIVO_NIO += (ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) FROM OPENJSON(@PaymentsJson) pj JOIN CAT.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT) JOIN CAT.MONEDAS m ON m.ID_MONEDA = mp.ID_MONEDA WHERE mp.NOMBRE LIKE '%EFECTIVO%' AND m.CODIGO = 'NIO'), 0) - @VueltoTotalNio),
        T.TOTAL_EFECTIVO_USD += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.Monto') AS DECIMAL(12,2))) FROM OPENJSON(@PaymentsJson) pj JOIN CAT.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT) JOIN CAT.MONEDAS m ON m.ID_MONEDA = mp.ID_MONEDA WHERE mp.NOMBRE LIKE '%EFECTIVO%' AND m.CODIGO = 'USD'), 0),
        T.TOTAL_TARJETA += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) FROM OPENJSON(@PaymentsJson) pj JOIN CAT.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT) WHERE mp.NOMBRE LIKE '%TARJETA%'), 0),
        T.TOTAL_TRANSFERENCIA += ISNULL((SELECT SUM(CAST(JSON_VALUE(pj.[value], '$.MontoEnNio') AS DECIMAL(12,2))) FROM OPENJSON(@PaymentsJson) pj JOIN CAT.METODOS_PAGO mp ON mp.ID_METODO = CAST(JSON_VALUE(pj.[value], '$.IdMetodoPago') AS INT) WHERE mp.NOMBRE LIKE '%TRANSFERENCIA%'), 0),
        T.TOTAL_VENTAS_NIO += @TotalVentaNio,
        T.TOTAL_VENTAS_USD += (@TotalVentaNio / @TasaCambioUsd)
    FROM CAJA.TURNOS T WHERE T.ID_TURNO = @IdTurno;

    -- 6. Procesar Items y Garantías (Iteramos por unidad para precisión total)
    DECLARE @IdProducto INT, @DescSnap NVARCHAR(200), @UnitPrice DECIMAL(12,2), @Imei NVARCHAR(20), @IdPeriodo INT, @Meses INT, @IsRegalia BIT;
    DECLARE @IdDetalle INT, @IdImei INT;

    DECLARE detail_cursor CURSOR FOR
    SELECT 
        CAST(JSON_VALUE(i.[value], '$.IdProducto') AS INT),
        JSON_VALUE(i.[value], '$.Description'),
        CAST(JSON_VALUE(i.[value], '$.UnitPrice') AS DECIMAL(12,2)),
        JSON_VALUE(d.[value], '$.Imei'),
        CAST(JSON_VALUE(d.[value], '$.IdPeriodoGarantia') AS INT),
        PG.MESES,
        CAST(ISNULL(JSON_VALUE(i.[value], '$.IsRegalia'), 'false') AS BIT)
    FROM OPENJSON(@ItemsJson) AS i
    CROSS APPLY OPENJSON(i.[value], '$.Details') AS d
    JOIN CAT.PERIODOS_GARANTIA PG ON PG.ID_PERIODO = CAST(JSON_VALUE(d.[value], '$.IdPeriodoGarantia') AS INT);

    OPEN detail_cursor;
    FETCH NEXT FROM detail_cursor INTO @IdProducto, @DescSnap, @UnitPrice, @Imei, @IdPeriodo, @Meses, @IsRegalia;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- A. Validar Stock (1 unidad)
        DECLARE @StockActual INT;
        SELECT @StockActual = STOCK_ACTUAL FROM INV.PRODUCTOS WHERE ID_PRODUCTO = @IdProducto;
        
        IF @StockActual < 1
        BEGIN
            DECLARE @ErrorMsg NVARCHAR(300) = 'Stock agotado para ' + @DescSnap + '. No se puede completar la venta.';
            ROLLBACK TRANSACTION;
            CLOSE detail_cursor;
            DEALLOCATE detail_cursor;
            THROW 50003, @ErrorMsg, 1;
        END

        -- B. Reducir Stock
        UPDATE INV.PRODUCTOS SET STOCK_ACTUAL = STOCK_ACTUAL - 1 WHERE ID_PRODUCTO = @IdProducto;

        -- C. Calcular fecha vencimiento
        DECLARE @FechaVence DATE = NULL;
        IF @Meses > 0 SET @FechaVence = DATEADD(MONTH, @Meses, @FechaActual);

        -- D. Insertar VENTA_DETALLE (Cantidad = 1 por fila)
        INSERT INTO VEN.VENTA_DETALLE (ID_VENTA, ID_PRODUCTO, DESCRIPCION_SNAP, CANTIDAD, PRECIO_UNITARIO_NIO, SUBTOTAL_NIO, ID_PERIODO_GARANTIA, FECHA_VENCE_GARANTIA, ES_REGALIA)
        VALUES (@IdVenta, @IdProducto, @DescSnap, 1, @UnitPrice, @UnitPrice, @IdPeriodo, @FechaVence, @IsRegalia);
        SET @IdDetalle = SCOPE_IDENTITY();

        -- E. Manejar IMEI si existe
        SET @IdImei = NULL;
        IF @Imei IS NOT NULL AND @Imei <> ''
        BEGIN
            SELECT @IdImei = ID_IMEI FROM INV.EQUIPOS_IMEI WHERE IMEI = @Imei AND ID_PRODUCTO = @IdProducto;
            
            IF @IdImei IS NOT NULL
            BEGIN
                INSERT INTO VEN.VENTA_DETALLE_IMEI (ID_DETALLE, ID_EQUIPO_IMEI, IMEI_SNAP)
                VALUES (@IdDetalle, @IdImei, @Imei);
                
                UPDATE INV.EQUIPOS_IMEI SET ESTADO_IMEI = 'VENDIDO' WHERE ID_IMEI = @IdImei;
            END
        END

        -- F. Registrar Garantía formal en GAR.GARANTIAS si hay cliente y meses > 0
        IF @IdPersona IS NOT NULL AND @Meses > 0
        BEGIN
            INSERT INTO GAR.GARANTIAS (ID_DETALLE_VENTA, ID_EQUIPO_IMEI, ID_PERSONA, ID_PRODUCTO, MESES_GARANTIA, FECHA_INICIO, FECHA_VENCIMIENTO, ESTADO_GARANTIA)
            VALUES (@IdDetalle, @IdImei, @IdPersona, @IdProducto, @Meses, CAST(@FechaActual AS DATE), @FechaVence, 'ACTIVA');
        END

        FETCH NEXT FROM detail_cursor INTO @IdProducto, @DescSnap, @UnitPrice, @Imei, @IdPeriodo, @Meses, @IsRegalia;
    END

    CLOSE detail_cursor;
    DEALLOCATE detail_cursor;

    COMMIT TRANSACTION;
    SELECT * FROM VEN.VENTAS WHERE ID_VENTA = @IdVenta;
END;
GO
