-- ============================================================
-- PROCEDIMIENTOS, VISTAS, TRIGGERS Y DEMÁS ACTUALIZACIONES
-- PROYECTO: InnovaTecPOS
-- FECHA: 2026-04-10
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE InnovaTecBD;
GO

-- ============================================================
-- 1. SEGURIDAD: INICIO DE SESIÓN
-- ============================================================

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ADM].[sp_IniciarSesion]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [ADM].[sp_IniciarSesion];
GO

CREATE PROCEDURE [ADM].[sp_IniciarSesion]
    @Username NVARCHAR(80), -- Cédula
    @Password NVARCHAR(MAX) -- Contraseña en texto plano para validación interna
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserId INT;
    DECLARE @StoredHash VARBINARY(64);
    DECLARE @StoredSalt VARBINARY(32);
    DECLARE @Estado INT;

    -- 1. Verificar si la cédula existe
    SELECT 
        @UserId = ID_USUARIO,
        @StoredHash = PASSWORD_HASH,
        @StoredSalt = PASSWORD_SALT,
        @Estado = ID_ESTADO
    FROM ADM.USUARIOS
    WHERE USERNAME = @Username;

    IF @UserId IS NULL
    BEGIN
        SELECT 0 AS Success, 'la cedula no es correcta' AS Message, NULL AS UserId;
        RETURN;
    END

    -- 2. Verificar si el usuario está activo
    IF @Estado <> 1 -- 1 = ACTIVO según CAT.ESTADOS
    BEGIN
        SELECT 0 AS Success, 'El usuario se encuentra inactivo' AS Message, NULL AS UserId;
        RETURN;
    END

    -- 3. Verificar contraseña (Usando HASHBYTES para seguridad básica)
    -- NOTA: Se asume que el hash se generó con la misma lógica
    IF @StoredHash = HASHBYTES('SHA2_512', @Password + CAST(@StoredSalt AS NVARCHAR(MAX)))
    BEGIN
        -- Éxito
        UPDATE ADM.USUARIOS SET ULTIMO_ACCESO = SYSDATETIME() WHERE ID_USUARIO = @UserId;
        
        SELECT 1 AS Success, 'Acceso concedido' AS Message, @UserId AS UserId;
    END
    ELSE
    BEGIN
        -- Contraseña incorrecta
        SELECT 0 AS Success, 'contraseña incorrecta' AS Message, NULL AS UserId;
    END
END
GO

-- Fin del script de procedimientos.
GO
