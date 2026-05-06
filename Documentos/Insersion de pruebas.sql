USE InnovaTecBD;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================
-- SCRIPT: Insersion de pruebas
-- Descripción: Carga masiva de datos para pruebas de rendimiento y UI.
-- ============================================================

SET NOCOUNT ON;

DECLARE @UsuarioID INT = (SELECT TOP 1 ID_USUARIO FROM ADM.USUARIOS WHERE ID_ESTADO = 1);
DECLARE @AdminPersonaID INT = (SELECT ID_PERSONA FROM ADM.EMPLEADOS WHERE ID_EMPLEADO = (SELECT ID_EMPLEADO FROM ADM.USUARIOS WHERE ID_USUARIO = @UsuarioID));

IF @UsuarioID IS NULL
BEGIN
    PRINT 'ERROR: No se encontró un usuario activo para realizar la carga.';
    RETURN;
END

-- 1. OBTENER CATEGORIAS
DECLARE @CatCelulares INT = (SELECT ID_CATEGORIA FROM INV.CATEGORIAS WHERE NOMBRE = 'CELULARES');
DECLARE @CatAccesorios INT = (SELECT ID_CATEGORIA FROM INV.CATEGORIAS WHERE NOMBRE = 'ACCESORIOS');
DECLARE @CatVarios INT = (SELECT ID_CATEGORIA FROM INV.CATEGORIAS WHERE NOMBRE = 'ARTICULOS VARIOS');

-- 2. INSERTAR 100 PRODUCTOS
PRINT 'Insertando 100 productos...';
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    DECLARE @NombreProd NVARCHAR(150);
    DECLARE @ID_Cat INT;
    DECLARE @Tipo VARCHAR(20);
    DECLARE @PrecioVenta DECIMAL(12,2);
    DECLARE @PrecioCompra DECIMAL(12,2);
    
    IF @i <= 35
    BEGIN
        SET @ID_Cat = @CatCelulares;
        SET @Tipo = 'TELEFONO';
        SET @NombreProd = 'Celular Modelo ' + CAST(@i AS NVARCHAR(5));
        SET @PrecioCompra = 100 + (@i * 5);
        SET @PrecioVenta = @PrecioCompra + 50;
    END
    ELSE IF @i <= 70
    BEGIN
        SET @ID_Cat = @CatAccesorios;
        SET @Tipo = 'ACCESORIO';
        SET @NombreProd = 'Accesorio Tipo ' + CAST(@i - 35 AS NVARCHAR(5));
        SET @PrecioCompra = 5 + (@i * 0.5);
        SET @PrecioVenta = @PrecioCompra + 10;
    END
    ELSE
    BEGIN
        SET @ID_Cat = @CatVarios;
        SET @Tipo = 'ARTICULO';
        SET @NombreProd = 'Artículo Vario ' + CAST(@i - 70 AS NVARCHAR(5));
        SET @PrecioCompra = 2 + (@i * 0.2);
        SET @PrecioVenta = @PrecioCompra + 5;
    END

    INSERT INTO INV.PRODUCTOS (NOMBRE, MARCA, MODELO, ID_CATEGORIA, TIPO_PRODUCTO, PRECIO_COMPRA, PRECIO_VENTA, STOCK_ACTUAL, STOCK_MINIMO, ACTIVO, CREADO_POR)
    VALUES (@NombreProd, 'MarcaGen', 'Mod-' + CAST(@i AS NVARCHAR(10)), @ID_Cat, @Tipo, @PrecioCompra, @PrecioVenta, 50, 5, 1, @UsuarioID);

    -- Si es teléfono, insertar algunos IMEIs
    IF @Tipo = 'TELEFONO'
    BEGIN
        DECLARE @ProdID INT = SCOPE_IDENTITY();
        DECLARE @j INT = 1;
        WHILE @j <= 5
        BEGIN
            INSERT INTO INV.EQUIPOS_IMEI (ID_PRODUCTO, IMEI, ESTADO_IMEI, INGRESADO_POR)
            VALUES (@ProdID, 'IMEI-' + CAST(@ProdID AS VARCHAR(10)) + '-' + CAST(@j AS VARCHAR(5)), 'DISPONIBLE', @UsuarioID);
            SET @j = @j + 1;
        END
    END

    SET @i = @i + 1;
END

-- 3. INSERTAR 100 CLIENTES
PRINT 'Insertando 100 clientes...';
SET @i = 1;
WHILE @i <= 100
BEGIN
    INSERT INTO ADM.PERSONAS (PRIMER_NOMBRE, PRIMER_APELLIDO, ID_TIPO_ID, NUM_IDENTIFICACION, TELEFONO, DIRECCION, ES_CLIENTE, ID_ESTADO)
    VALUES ('Cliente' + CAST(@i AS VARCHAR(5)), 'Apellido' + CAST(@i AS VARCHAR(5)), 1, 'ID-' + CAST(10000 + @i AS VARCHAR(10)), '8888-00' + CAST(@i AS VARCHAR(3)), 'Dirección Prueba ' + CAST(@i AS VARCHAR(5)), 1, 1);
    SET @i = @i + 1;
END

-- 4. ABRIR UN TURNO SI NO HAY UNO
PRINT 'Verificando turno de caja...';
DECLARE @TurnoID INT = (SELECT TOP 1 ID_TURNO FROM CAJA.TURNOS WHERE ID_USUARIO = @UsuarioID AND ID_ESTADO = 1);
IF @TurnoID IS NULL
BEGIN
    INSERT INTO CAJA.TURNOS (ID_USUARIO, MONTO_INICIAL_NIO, MONTO_INICIAL_USD, ID_ESTADO)
    VALUES (@UsuarioID, 1000, 100, 1);
    SET @TurnoID = SCOPE_IDENTITY();
END

-- 5. INSERTAR 100 VENTAS (Desde Mayo 1 hasta hoy)
PRINT 'Insertando 100 ventas con garantías y facturas...';
SET @i = 1;
DECLARE @FechaBase DATETIME = '2026-05-01 09:00:00';
DECLARE @TasaCambio DECIMAL(18,6) = 36.50;

-- Obtener lista de clientes y productos insertados para referenciar
DECLARE @ClientesTable TABLE (RowID INT IDENTITY(1,1), ID_PERSONA INT);
INSERT INTO @ClientesTable (ID_PERSONA) SELECT ID_PERSONA FROM ADM.PERSONAS WHERE ES_CLIENTE = 1 AND PRIMER_NOMBRE LIKE 'Cliente%';

DECLARE @ProductosTable TABLE (RowID INT IDENTITY(1,1), ID_PRODUCTO INT, NOMBRE NVARCHAR(150), PRECIO DECIMAL(12,2), TIPO VARCHAR(20));
INSERT INTO @ProductosTable (ID_PRODUCTO, NOMBRE, PRECIO, TIPO) SELECT ID_PRODUCTO, NOMBRE, PRECIO_VENTA, TIPO_PRODUCTO FROM INV.PRODUCTOS WHERE CREADO_POR = @UsuarioID;

WHILE @i <= 100
BEGIN
    -- Determinar fecha (distribuida en los primeros 6 días de Mayo)
    DECLARE @FechaVenta DATETIME = DATEADD(HOUR, @i * 1.2, @FechaBase);
    IF @FechaVenta > GETDATE() SET @FechaVenta = GETDATE();

    -- Seleccionar cliente
    DECLARE @ClienteID INT = (SELECT ID_PERSONA FROM @ClientesTable WHERE RowID = @i);
    
    -- Seleccionar producto aleatorio (aprox)
    DECLARE @ProdRowID INT = (@i % 100) + 1;
    DECLARE @ProdID_Venta INT, @ProdNombre_Venta NVARCHAR(150), @ProdPrecio_Venta DECIMAL(12,2), @ProdTipo_Venta VARCHAR(20);
    SELECT @ProdID_Venta = ID_PRODUCTO, @ProdNombre_Venta = NOMBRE, @ProdPrecio_Venta = PRECIO, @ProdTipo_Venta = TIPO FROM @ProductosTable WHERE RowID = @ProdRowID;

    -- Insertar Venta
    INSERT INTO VEN.VENTAS (ID_TURNO, ID_USUARIO, ID_PERSONA, TASA_CAMBIO_USD, SUBTOTAL_NIO, TOTAL_NIO, FECHA_VENTA)
    VALUES (@TurnoID, @UsuarioID, @ClienteID, @TasaCambio, @ProdPrecio_Venta, @ProdPrecio_Venta, @FechaVenta);
    
    DECLARE @VentaID INT = SCOPE_IDENTITY();

    -- Insertar Detalle
    -- Garantía: 40% de los casos (i % 5 < 2 o algo así para que sea 40%)
    DECLARE @ID_PeriodoGarantia INT = NULL;
    DECLARE @FechaVenceGarantia DATE = NULL;
    
    IF (@i % 10 < 4) -- 40%
    BEGIN
        SET @ID_PeriodoGarantia = 13; -- 12 Meses (según script base, ID 13 es 12 meses si se insertaron correlativos)
        -- Ajustar ID si es necesario. En el script base: 1=Sin, 2=1 mes... 13=12 meses.
        SET @FechaVenceGarantia = DATEADD(YEAR, 1, @FechaVenta);
    END

    INSERT INTO VEN.VENTA_DETALLE (ID_VENTA, ID_PRODUCTO, DESCRIPCION_SNAP, CANTIDAD, PRECIO_UNITARIO_NIO, SUBTOTAL_NIO, ID_PERIODO_GARANTIA, FECHA_VENCE_GARANTIA)
    VALUES (@VentaID, @ProdID_Venta, @ProdNombre_Venta, 1, @ProdPrecio_Venta, @ProdPrecio_Venta, @ID_PeriodoGarantia, @FechaVenceGarantia);
    
    DECLARE @DetalleID INT = SCOPE_IDENTITY();

    -- Si es teléfono, asociar un IMEI
    IF @ProdTipo_Venta = 'TELEFONO'
    BEGIN
        DECLARE @ImeiID INT = (SELECT TOP 1 ID_IMEI FROM INV.EQUIPOS_IMEI WHERE ID_PRODUCTO = @ProdID_Venta AND ESTADO_IMEI = 'DISPONIBLE');
        IF @ImeiID IS NOT NULL
        BEGIN
            INSERT INTO VEN.VENTA_DETALLE_IMEI (ID_DETALLE, ID_EQUIPO_IMEI, IMEI_SNAP)
            VALUES (@DetalleID, @ImeiID, (SELECT IMEI FROM INV.EQUIPOS_IMEI WHERE ID_IMEI = @ImeiID));
            
            UPDATE INV.EQUIPOS_IMEI SET ESTADO_IMEI = 'VENDIDO' WHERE ID_IMEI = @ImeiID;
        END
    END

    -- Insertar Pago
    INSERT INTO VEN.PAGOS (ID_VENTA, ID_METODO_PAGO, MONTO_PAGADO, MONTO_EN_NIO, FECHA_PAGO)
    VALUES (@VentaID, 1, @ProdPrecio_Venta, @ProdPrecio_Venta, @FechaVenta);

    -- Si hay garantía, insertarla en GAR.GARANTIAS
    IF @ID_PeriodoGarantia IS NOT NULL
    BEGIN
        INSERT INTO GAR.GARANTIAS (ID_DETALLE_VENTA, ID_EQUIPO_IMEI, ID_PERSONA, ID_PRODUCTO, MESES_GARANTIA, FECHA_INICIO, FECHA_VENCIMIENTO, ESTADO_GARANTIA)
        VALUES (@DetalleID, (SELECT ID_EQUIPO_IMEI FROM VEN.VENTA_DETALLE_IMEI WHERE ID_DETALLE = @DetalleID), @ClienteID, @ProdID_Venta, 12, @FechaVenta, @FechaVenceGarantia, 'ACTIVA');
    END

    -- Actualizar stock
    UPDATE INV.PRODUCTOS SET STOCK_ACTUAL = STOCK_ACTUAL - 1 WHERE ID_PRODUCTO = @ProdID_Venta;

    SET @i = @i + 1;
END

PRINT 'Carga de pruebas finalizada correctamente.';
GO
