-- ===================================================================
-- PROYECTO: SISTEMA INTELIGENTE DE MONITOREO Y CONTROL AMBIENTAL (SCADA)
-- SENSOR: DHT22 (TEMPERATURA Y HUMEDAD)
-- ACTUADORES: VENTILADOR (RELÉ D7) Y LÁMPARA (RELÉ D8)
-- DISPLAY: LCD 16X2 I2C (A4/A5)
-- BASE DE DATOS: SQL SERVER
-- SCRIPT: 01_Create_Database_And_Tables.sql
-- ===================================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ThermalMasterDB')
BEGIN
    CREATE DATABASE ThermalMasterDB;
    PRINT 'Base de datos ThermalMasterDB creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'Base de datos ThermalMasterDB ya existe.';
END
GO

USE ThermalMasterDB;
GO

-- 1. Tabla de Lecturas Ambientales (SensorReadings con DHT22)
IF OBJECT_ID('dbo.SensorReadings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SensorReadings (
        ReadingId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SensorReadings PRIMARY KEY CLUSTERED,
        Timestamp DATETIME2(3) NOT NULL CONSTRAINT DF_SensorReadings_Timestamp DEFAULT SYSUTCDATETIME(),
        TemperatureC DECIMAL(5, 2) NOT NULL,
        HumidityPct DECIMAL(5, 2) NOT NULL, -- Medición digital de humedad DHT22
        FanStatus BIT NOT NULL,             -- 0 = Ventilador Apagado, 1 = Ventilador Encendido (Relé D7)
        LampStatus BIT NOT NULL,            -- 0 = Lámpara Apagada, 1 = Lámpara Encendida (Relé D8)
        OperatingMode VARCHAR(20) NOT NULL CONSTRAINT CK_SensorReadings_Mode CHECK (OperatingMode IN ('MANUAL', 'AUTOMATIC')),
        Severity VARCHAR(20) NOT NULL CONSTRAINT CK_SensorReadings_Severity CHECK (Severity IN ('NORMAL', 'WARNING', 'CRITICAL')),
        MinThreshold DECIMAL(5, 2) NOT NULL,
        MaxThreshold DECIMAL(5, 2) NOT NULL,
        ProteusAck BIT NOT NULL CONSTRAINT DF_SensorReadings_ProteusAck DEFAULT 1
    );

    CREATE NONCLUSTERED INDEX IX_SensorReadings_Timestamp 
    ON dbo.SensorReadings (Timestamp DESC)
    INCLUDE (TemperatureC, HumidityPct, FanStatus, LampStatus, Severity);

    PRINT 'Tabla SensorReadings creada con soporte de DHT22 (Temp + Humedad) y doble actuador.';
END
GO

-- 2. Tabla de Comandos hacia el Microcontrolador (DeviceCommands)
IF OBJECT_ID('dbo.DeviceCommands', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeviceCommands (
        CommandId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DeviceCommands PRIMARY KEY NONCLUSTERED,
        CommandType VARCHAR(50) NOT NULL, -- 'FAN_ON', 'FAN_OFF', 'LAMP_ON', 'LAMP_OFF', 'SET_MODE', 'SET_LIMITS'
        Parameter VARCHAR(100) NULL,
        IssuedAt DATETIME2(3) NOT NULL CONSTRAINT DF_DeviceCommands_IssuedAt DEFAULT SYSUTCDATETIME(),
        Acknowledged BIT NOT NULL CONSTRAINT DF_DeviceCommands_Acknowledged DEFAULT 0,
        AcknowledgedAt DATETIME2(3) NULL,
        ResponsePayload NVARCHAR(255) NULL
    );

    CREATE CLUSTERED INDEX CIX_DeviceCommands_IssuedAt 
    ON dbo.DeviceCommands (IssuedAt DESC);

    PRINT 'Tabla DeviceCommands creada para auditoría de órdenes.';
END
GO

-- 3. Tabla de Registro Histórico de Alertas (AlertLogs)
IF OBJECT_ID('dbo.AlertLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertLogs (
        AlertId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AlertLogs PRIMARY KEY CLUSTERED,
        Timestamp DATETIME2(3) NOT NULL CONSTRAINT DF_AlertLogs_Timestamp DEFAULT SYSUTCDATETIME(),
        Severity VARCHAR(20) NOT NULL,
        Message NVARCHAR(255) NOT NULL,
        TriggerTemperature DECIMAL(5, 2) NOT NULL,
        TriggerHumidity DECIMAL(5, 2) NULL,
        IsResolved BIT NOT NULL CONSTRAINT DF_AlertLogs_IsResolved DEFAULT 0,
        ResolvedAt DATETIME2(3) NULL
    );

    CREATE NONCLUSTERED INDEX IX_AlertLogs_Pending 
    ON dbo.AlertLogs (IsResolved, Timestamp DESC);

    PRINT 'Tabla AlertLogs creada.';
END
GO

-- 4. Tabla de Parámetros de Configuración del Sistema (SystemConfigurations)
IF OBJECT_ID('dbo.SystemConfigurations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemConfigurations (
        ConfigId INT NOT NULL CONSTRAINT PK_SystemConfigurations PRIMARY KEY CLUSTERED,
        SerialPortName VARCHAR(20) NOT NULL DEFAULT 'COM1',
        BaudRate INT NOT NULL DEFAULT 9600,
        MinTempThreshold DECIMAL(5, 2) NOT NULL DEFAULT 18.00,  -- <18°C activa Lámpara
        MaxTempThreshold DECIMAL(5, 2) NOT NULL DEFAULT 28.00,  -- >28°C activa Ventilador
        HysteresisOffset DECIMAL(5, 2) NOT NULL DEFAULT 1.00,
        TelemetryIntervalMs INT NOT NULL DEFAULT 1000,
        AudibleAlertEnabled BIT NOT NULL DEFAULT 1,
        LastUpdated DATETIME2(3) NOT NULL CONSTRAINT DF_SystemConfigurations_LastUpdated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_ConfigId_Singleton CHECK (ConfigId = 1)
    );

    -- Insertar configuración inicial por defecto
    INSERT INTO dbo.SystemConfigurations (ConfigId, SerialPortName, BaudRate, MinTempThreshold, MaxTempThreshold, HysteresisOffset, TelemetryIntervalMs, AudibleAlertEnabled)
    VALUES (1, 'COM1', 9600, 18.00, 28.00, 1.00, 1000, 1);

    PRINT 'Tabla SystemConfigurations creada con fila inicial por defecto.';
END
GO
