USE InnovaTecBD;
GO

-- 1. Agregar columna INTENTOS_FALLIDOS a ADM.USUARIOS
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ADM.USUARIOS') AND name = 'INTENTOS_FALLIDOS')
BEGIN
    ALTER TABLE ADM.USUARIOS ADD INTENTOS_FALLIDOS INT NOT NULL DEFAULT 0;
END
GO

-- 2. Insertar estado BLOQUEADO si no existe
IF NOT EXISTS (SELECT 1 FROM CAT.ESTADOS WHERE CODIGO = 'BLOQUEADO')
BEGIN
    INSERT INTO CAT.ESTADOS (CODIGO, DESC_ESTADO) VALUES ('BLOQUEADO', 'Bloqueado');
END
GO

-- 3. Actualizar Procedimiento Almacenado ADM.sp_IniciarSesion
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
ALTER PROCEDURE [ADM].[sp_IniciarSesion]
    @Username NVARCHAR(80),
    @Password NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    -- Normalizar el Username (quitar guiones y espacios)
    SET @Username = REPLACE(REPLACE(@Username, '-', ''), ' ', '');

    DECLARE @UserId INT, @StoredHash VARBINARY(64), @StoredSalt VARBINARY(32), @Estado INT, @Intentos INT;

    SELECT @UserId = ID_USUARIO, @StoredHash = PASSWORD_HASH, @StoredSalt = PASSWORD_SALT, 
           @Estado = ID_ESTADO, @Intentos = ISNULL(INTENTOS_FALLIDOS, 0)
    FROM ADM.USUARIOS WHERE USERNAME = @Username;

    IF @UserId IS NULL
    BEGIN
        SELECT 0 AS Success, 'la cedula no es correcta' AS Message, NULL AS UserId;
        RETURN;
    END

    -- Verificar si está bloqueado
    -- Buscamos el ID del estado 'BLOQUEADO' para ser dinámicos
    DECLARE @IdBloqueado INT = (SELECT ID_ESTADO FROM CAT.ESTADOS WHERE CODIGO = 'BLOQUEADO');

    IF @Estado = @IdBloqueado
    BEGIN
        SELECT 0 AS Success, 'Su cuenta ha sido bloqueada' AS Message, NULL AS UserId;
        RETURN;
    END

    IF @Estado <> 1 -- 1 suele ser ACTIVO
    BEGIN
        SELECT 0 AS Success, 'El usuario se encuentra inactivo' AS Message, NULL AS UserId;
        RETURN;
    END

    IF @StoredHash = HASHBYTES('SHA2_512', @Password + CAST(@StoredSalt AS NVARCHAR(MAX)))
    BEGIN
        -- Éxito: Reiniciar intentos y actualizar último acceso
        UPDATE ADM.USUARIOS SET ULTIMO_ACCESO = SYSDATETIME(), INTENTOS_FALLIDOS = 0 WHERE ID_USUARIO = @UserId;
        SELECT 1 AS Success, 'Acceso concedido' AS Message, @UserId AS UserId;
    END
    ELSE
    BEGIN
        -- Fallo: Incrementar intentos
        SET @Intentos = @Intentos + 1;
        
        IF @Intentos >= 5
        BEGIN
            UPDATE ADM.USUARIOS SET INTENTOS_FALLIDOS = @Intentos, ID_ESTADO = @IdBloqueado WHERE ID_USUARIO = @UserId;
            SELECT 0 AS Success, 'Su cuenta ha sido bloqueada' AS Message, NULL AS UserId;
        END
        ELSE
        BEGIN
            UPDATE ADM.USUARIOS SET INTENTOS_FALLIDOS = @Intentos WHERE ID_USUARIO = @UserId;
            SELECT 0 AS Success, 'contraseña incorrecta' AS Message, NULL AS UserId;
        END
    END
END
GO
