DECLARE @ItemsJson NVARCHAR(MAX) = '[{"IdProducto":1,"Description":"Test","UnitPrice":10.0,"Details":[{"Imei":"123456","IdPeriodoGarantia":1}]}]';
SELECT 
    CAST(JSON_VALUE(i.[value], '$.IdProducto') AS INT) AS IdProducto,
    JSON_VALUE(d.[value], '$.Imei') AS Imei,
    CAST(JSON_VALUE(d.[value], '$.IdPeriodoGarantia') AS INT) AS IdPeriodoGarantia
FROM OPENJSON(@ItemsJson) AS i
CROSS APPLY OPENJSON(i.[value], '$.Details') AS d;
