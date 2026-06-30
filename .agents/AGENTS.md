# Reglas del Proyecto InnovaTecPOS

## Gestión de Ramas (Git / GitHub Actions)
- El proyecto ya está en producción con un VPS (ya está en internet).
- Se manejan GitHub Actions con 2 ramas principales:
  - `main`: Rama de producción.
  - `desarrollo`: Rama de desarrollo donde se agregan las nuevas actualizaciones.

## Base de Datos
- Existe un script principal de base de datos ubicado en: `E:\Programación\Antigravity\InnovaTecPOS\Documentos\InnovaTecBD.sql`.
- **REGLA ESTRICTA**: Este script contiene completamente la base de datos. Cada vez que se realice una actualización, migración o mejora a nivel de base de datos (tablas, procedimientos, etc.), **se debe actualizar este script agregando la mejora**.

## Scripts de Migración (Despliegue a Producción)
- **Flujo de Trabajo (Best Practice)**: Para proteger los datos vivos de producción en el VPS, NUNCA se debe subir un `.bak` desde local a producción.
- **Creación de Scripts**: Cada vez que se realicen cambios estructurales en la base de datos (nuevas columnas, tablas, procedimientos), además de actualizar `InnovaTecBD.sql`, se debe generar un archivo de migración en la carpeta `Documentos`. **Para mantener el orden, todos los cambios realizados en una misma sesión de trabajo o día deben irse acumulando (agregando) en un mismo archivo (ej. `actualizacionV1.sql`) en lugar de crear un archivo nuevo por cada pequeño cambio.**
- **Contenido del Script**: Este script debe contener únicamente las sentencias necesarias para aplicar la actualización (ej. `ALTER TABLE`, `CREATE/ALTER PROCEDURE`) de preferencia utilizando bloques `IF NOT EXISTS` para que el script sea seguro de ejecutar. De esta forma, el usuario puede conectarse por SSMS a su VPS y ejecutar únicamente este script para actualizar la base de datos en cuestión de 1 segundo sin perder información.
