-- ============================================================
-- REFACTORIZACIÓN FASE 1: INVENTARIO (INV)
-- OBJETIVO: Migrar lógica de productos a la BD
-- ============================================================

USE InnovaTecBD;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. VISTA MAESTRA DE PRODUCTOS
IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'V_PRODUCTOS_DETALLE' AND schema_id = SCHEMA_ID('INV'))
    DROP VIEW INV.V_PRODUCTOS_DETALLE;
GO

CREATE VIEW INV.V_PRODUCTOS_DETALLE AS
SELECT 
    P.ID_PRODUCTO,
    P.CODIGO_BARRAS,
    P.NOMBRE,
    P.MARCA,
    P.MODELO,
    P.ALMACENAMIENTO,
    P.COLOR,
    P.ID_CATEGORIA,
    C.NOMBRE AS NOMBRE_CATEGORIA,
    P.TIPO_PRODUCTO,
    P.PRECIO_COMPRA,
    P.PRECIO_VENTA,
    P.STOCK_ACTUAL,
    P.STOCK_MINIMO,
    P.ESTADO_STOCK,
    P.ACTIVO,
    P.FECHA_CREACION,
    (P.STOCK_ACTUAL * P.PRECIO_VENTA) AS VALOR_TOTAL_VENTA,
    (SELECT COUNT(1) FROM INV.EQUIPOS_IMEI I WHERE I.ID_PRODUCTO = P.ID_PRODUCTO AND I.ESTADO_IMEI = 'DISPONIBLE') AS CANTIDAD_IMEI_DISP
FROM INV.PRODUCTOS P
LEFT JOIN INV.CATEGORIAS C ON P.ID_CATEGORIA = C.ID_CATEGORIA;
GO

-- 2. PROCEDIMIENTO PARA LISTAR Y BUSCAR PRODUCTOS
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_ListarProductos' AND schema_id = SCHEMA_ID('INV'))
    DROP PROCEDURE INV.sp_ListarProductos;
GO

CREATE PROCEDURE INV.sp_ListarProductos
    @Busqueda NVARCHAR(100) = NULL,
    @IdCategoria INT = NULL,
    @IncluirInactivos BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET QUOTED_IDENTIFIER ON;

    SELECT * 
    FROM INV.V_PRODUCTOS_DETALLE
    WHERE (@IncluirInactivos = 1 OR ACTIVO = 1)
      AND (@IdCategoria IS NULL OR ID_CATEGORIA = @IdCategoria OR @IdCategoria = 0)
      AND (@Busqueda IS NULL OR @Busqueda = '' OR
           NOMBRE LIKE '%' + @Busqueda + '%' OR 
           CODIGO_BARRAS LIKE '%' + @Busqueda + '%' OR 
           MARCA LIKE '%' + @Busqueda + '%' OR 
           MODELO LIKE '%' + @Busqueda + '%')
    ORDER BY ID_PRODUCTO DESC;
END;
GO

-- 3. PROCEDIMIENTO PARA MANTENIMIENTO DE PRODUCTOS (UPSERT)
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_MantenerProducto' AND schema_id = SCHEMA_ID('INV'))
    DROP PROCEDURE INV.sp_MantenerProducto;
GO

CREATE PROCEDURE INV.sp_MantenerProducto
    @IdProducto INT = NULL, -- NULL para insertar
    @CodigoBarras NVARCHAR(100) = NULL,
    @Nombre NVARCHAR(150),
    @Marca NVARCHAR(100) = NULL,
    @Modelo NVARCHAR(100) = NULL,
    @Almacenamiento NVARCHAR(50) = NULL,
    @Color NVARCHAR(50) = NULL,
    @IdCategoria INT = NULL,
    @PrecioCompra DECIMAL(12,2) = NULL,
    @PrecioVenta DECIMAL(12,2),
    @StockActual INT = 0,
    @StockMinimo INT = 0,
    @Activo BIT = 1,
    @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET QUOTED_IDENTIFIER ON;

    BEGIN TRANSACTION;

    -- Normalizar datos
    SET @CodigoBarras = NULLIF(LTRIM(RTRIM(@CodigoBarras)), '');
    SET @Nombre = LTRIM(RTRIM(@Nombre));

    -- Determinar TipoProducto basado en categoría
    DECLARE @TipoProducto VARCHAR(20) = 'ARTICULO';
    DECLARE @ManejaImei BIT = 0;
    DECLARE @CatNombre NVARCHAR(100);

    SELECT @ManejaImei = MANEJA_IMEI, @CatNombre = NOMBRE 
    FROM INV.CATEGORIAS WHERE ID_CATEGORIA = @IdCategoria;

    IF @ManejaImei = 1 SET @TipoProducto = 'TELEFONO';
    ELSE IF @CatNombre LIKE '%Accesorio%' SET @TipoProducto = 'ACCESORIO';

    -- Validar código de barras duplicado
    IF @CodigoBarras IS NOT NULL 
    BEGIN
        IF EXISTS (SELECT 1 FROM INV.PRODUCTOS WHERE CODIGO_BARRAS = @CodigoBarras AND (@IdProducto IS NULL OR ID_PRODUCTO <> @IdProducto))
        BEGIN
            THROW 50001, 'El código de barras ya pertenece a otro producto.', 1;
        END
    END

    IF @IdProducto IS NULL OR @IdProducto = 0
    BEGIN
        -- INSERT
        INSERT INTO INV.PRODUCTOS (
            CODIGO_BARRAS, NOMBRE, MARCA, MODELO, ALMACENAMIENTO, COLOR, 
            ID_CATEGORIA, TIPO_PRODUCTO, PRECIO_COMPRA, PRECIO_VENTA, 
            STOCK_ACTUAL, STOCK_MINIMO, ACTIVO, CREADO_POR
        )
        VALUES (
            @CodigoBarras, @Nombre, @Marca, @Modelo, @Almacenamiento, @Color,
            @IdCategoria, @TipoProducto, @PrecioCompra, @PrecioVenta,
            @StockActual, @StockMinimo, @Activo, @UsuarioId
        );
        SET @IdProducto = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        -- UPDATE
        UPDATE INV.PRODUCTOS SET
            CODIGO_BARRAS = @CodigoBarras,
            NOMBRE = @Nombre,
            MARCA = @Marca,
            MODELO = @Modelo,
            ALMACENAMIENTO = @Almacenamiento,
            COLOR = @Color,
            ID_CATEGORIA = @IdCategoria,
            TIPO_PRODUCTO = @TipoProducto,
            PRECIO_COMPRA = @PrecioCompra,
            PRECIO_VENTA = @PrecioVenta,
            STOCK_ACTUAL = @StockActual,
            STOCK_MINIMO = @StockMinimo,
            ACTIVO = @Activo
        WHERE ID_PRODUCTO = @IdProducto;
    END

    COMMIT TRANSACTION;
    
    -- Devolver el registro actualizado usando la vista
    SELECT * FROM INV.V_PRODUCTOS_DETALLE WHERE ID_PRODUCTO = @IdProducto;
END;
GO

PRINT '>>> Refactorización Fase 1 completada con éxito. <<<';
GO
