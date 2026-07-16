USE InnovaTecBD;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'V_PRODUCTOS_DETALLE' AND schema_id = SCHEMA_ID('INV'))
    DROP VIEW INV.V_PRODUCTOS_DETALLE;
GO

CREATE VIEW INV.V_PRODUCTOS_DETALLE AS
SELECT 
    P.*,
    C.NOMBRE AS NOMBRE_CATEGORIA,
    (SELECT COUNT(1) FROM INV.EQUIPOS_IMEI I WHERE I.ID_PRODUCTO = P.ID_PRODUCTO AND I.ESTADO_IMEI = 'DISPONIBLE') AS CANTIDAD_IMEI_DISP
FROM INV.PRODUCTOS P
LEFT JOIN INV.CATEGORIAS C ON P.ID_CATEGORIA = C.ID_CATEGORIA;
GO

IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_MantenerProducto' AND schema_id = SCHEMA_ID('INV'))
    DROP PROCEDURE INV.sp_MantenerProducto;
GO
CREATE PROCEDURE INV.sp_MantenerProducto
    @IdProducto INT = NULL,
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
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    SET @CodigoBarras = NULLIF(LTRIM(RTRIM(@CodigoBarras)), '');
    SET @Nombre = LTRIM(RTRIM(@Nombre));

    DECLARE @TipoProducto VARCHAR(20) = 'ARTICULO', @ManejaImei BIT = 0, @CatNombre NVARCHAR(100);
    SELECT @ManejaImei = MANEJA_IMEI, @CatNombre = NOMBRE FROM INV.CATEGORIAS WHERE ID_CATEGORIA = @IdCategoria;
    IF @ManejaImei = 1 SET @TipoProducto = 'TELEFONO';
    ELSE IF @CatNombre LIKE '%Accesorio%' SET @TipoProducto = 'ACCESORIO';

    IF @CodigoBarras IS NOT NULL AND EXISTS (SELECT 1 FROM INV.PRODUCTOS WHERE CODIGO_BARRAS = @CodigoBarras AND (@IdProducto IS NULL OR ID_PRODUCTO <> @IdProducto))
        THROW 50001, 'El código de barras ya pertenece a otro producto.', 1;

    IF @IdProducto IS NULL OR @IdProducto = 0
    BEGIN
        INSERT INTO INV.PRODUCTOS (CODIGO_BARRAS, NOMBRE, MARCA, MODELO, ALMACENAMIENTO, COLOR, ID_CATEGORIA, TIPO_PRODUCTO, PRECIO_COMPRA, PRECIO_VENTA, STOCK_ACTUAL, STOCK_MINIMO, ACTIVO, CREADO_POR, FECHA_DESACTIVACION)
        VALUES (@CodigoBarras, @Nombre, @Marca, @Modelo, @Almacenamiento, @Color, @IdCategoria, @TipoProducto, @PrecioCompra, @PrecioVenta, @StockActual, @StockMinimo, @Activo, @UsuarioId, CASE WHEN @Activo = 0 THEN SYSDATETIME() ELSE NULL END);
        SET @IdProducto = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE INV.PRODUCTOS SET CODIGO_BARRAS = @CodigoBarras, NOMBRE = @Nombre, MARCA = @Marca, MODELO = @Modelo, ALMACENAMIENTO = @Almacenamiento, COLOR = @Color, ID_CATEGORIA = @IdCategoria, TIPO_PRODUCTO = @TipoProducto, PRECIO_COMPRA = @PrecioCompra, PRECIO_VENTA = @PrecioVenta, STOCK_ACTUAL = @StockActual, STOCK_MINIMO = @StockMinimo, ACTIVO = @Activo, FECHA_DESACTIVACION = CASE WHEN ACTIVO = 1 AND @Activo = 0 THEN SYSDATETIME() WHEN @Activo = 1 THEN NULL ELSE FECHA_DESACTIVACION END
        WHERE ID_PRODUCTO = @IdProducto;
    END
    COMMIT TRANSACTION;
    SELECT * FROM INV.V_PRODUCTOS_DETALLE WHERE ID_PRODUCTO = @IdProducto;
END;
GO

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
    SELECT * FROM INV.V_PRODUCTOS_DETALLE
    WHERE (@IncluirInactivos = 1 OR ACTIVO = 1) AND ARCHIVADO = 0
      AND (@IdCategoria IS NULL OR ID_CATEGORIA = @IdCategoria OR @IdCategoria = 0)
      AND (@Busqueda IS NULL OR @Busqueda = '' OR
           NOMBRE LIKE '%' + @Busqueda + '%' OR 
           CODIGO_BARRAS LIKE '%' + @Busqueda + '%' OR 
           MARCA LIKE '%' + @Busqueda + '%' OR 
           MODELO LIKE '%' + @Busqueda + '%')
    ORDER BY ID_PRODUCTO DESC;
END;
GO
