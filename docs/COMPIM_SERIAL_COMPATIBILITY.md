# Compatibilidad Eléctrica COMPIM ↔ Arduino UNO (UART TTL)

## 1. El Problema: RS-232 vs TTL — ¿Son Compatibles?

Cuando se conecta Arduino UNO (UART TTL) con un puerto COM de Windows a través de Proteus,
surge la pregunta crítica: **¿se necesita un convertidor de niveles MAX232?**

La respuesta depende del contexto. Este documento lo aclara definitivamente.

---

## 2. Niveles Lógicos: RS-232 Estándar vs UART TTL

| Parámetro | RS-232 Estándar (EIA-232) | UART TTL (Arduino UNO) |
|:---|:---:|:---:|
| **Tensión lógica "1" (MARK)** | −3V a −15V | +5V |
| **Tensión lógica "0" (SPACE)** | +3V a +15V | 0V |
| **Lógica** | Invertida y bipolar | Positiva y unipolar |
| **Nivel típico de trabajo** | ±12V | 0V / 5V |
| **Compatible sin adaptador** | ❌ No directamente | — |

> Si se conectara un Arduino UNO directamente al puerto COM físico de un PC (RS-232 real),
> los niveles −12V destruirían el microcontrolador ATmega328P.
> En ese caso, **sí sería necesario el MAX232**.

---

## 3. COMPIM en Proteus 8.17 — Comportamiento Real del Componente

El componente **COMPIM** (COMponent Port Interface Model) de Proteus es un **modelo de software**
que actúa como puente entre el circuito simulado y un puerto COM virtual de Windows.

### ¿Cómo maneja COMPIM los niveles de tensión?

```
┌─────────────────────────────────────────────────────────────┐
│                    PROTEUS 8.17 (Motor VSM)                 │
│                                                             │
│  Arduino UNO (simulado)          COMPIM (simulado)          │
│  ┌────────────────┐              ┌──────────────────┐       │
│  │ ATmega328P     │              │                  │       │
│  │                │              │  Mapeo interno   │       │
│  │ D1 (TX) 0–5V ──┼──────────────┼► RXD (Pin 2)    │       │
│  │ D0 (RX) 0–5V ◄─┼──────────────┼─ TXD (Pin 3)    │       │
│  │                │   TTL directo │                  │       │
│  └────────────────┘              │  Windows COM API │       │
│                                  └────────┬─────────┘       │
└───────────────────────────────────────────┼─────────────────┘
                                            │ Virtual COM Port
                                     ┌──────▼──────┐
                                     │  COM2 (VSP) │
                                     │  com0com    │
                                     └──────┬──────┘
                                            │ Par virtual
                                     ┌──────▼──────┐
                                     │  COM1 (VSP) │
                                     │  Usado por  │
                                     │  C# Backend │
                                     └─────────────┘
```

**COMPIM realiza la conversión de niveles internamente dentro del simulador.**
No genera tensiones RS-232 reales (±12V). Trabaja directamente con los niveles TTL
que el Arduino simulado produce (0V y 5V) y los mapea a la API de puertos COM de Windows.

---

## 4. Conclusión Definitiva

> ### ✅ NO SE NECESITA MAX232 EN LA SIMULACIÓN DE PROTEUS
>
> El componente COMPIM de Proteus 8.17 se conecta **directamente** a los pines
> UART TTL del Arduino UNO (D0 RX, D1 TX) sin ningún componente adicional
> de conversión de niveles.

---

## 5. Conexión Pin a Pin: COMPIM ↔ Arduino UNO en Proteus

### Tabla de Conexiones

| Pin COMPIM | Señal | Conectar a | Pin Arduino UNO |
|:---:|:---:|:---|:---:|
| **Pin 2 (RXD)** | Datos entrantes al COMPIM | TX del Arduino | **D1** |
| **Pin 3 (TXD)** | Datos salientes del COMPIM | RX del Arduino | **D0** |
| **Pin 5 (GND)** | Referencia común de tierra | GND del circuito | **GND** |

> ⚠️ **Cruce de señales (crossover):** El TXD del Arduino (D1) se conecta al RXD del COMPIM,
> y el TXD del COMPIM se conecta al RXD del Arduino (D0). Esto es el cruce estándar TX→RX.

### Diagrama de Conexión Directa

```
Arduino UNO (Proteus)          COMPIM (Proteus)
┌─────────────────┐           ┌────────────────┐
│                 │           │                │
│   D1 (TX) ──────┼───────────┼► Pin 2 (RXD)  │
│                 │           │                │
│   D0 (RX) ◄─────┼───────────┼─ Pin 3 (TXD)  │
│                 │           │                │
│   GND ──────────┼───────────┼── Pin 5 (GND) │
│                 │           │                │
└─────────────────┘           └────────────────┘
```

---

## 6. Configuración del Componente COMPIM en Proteus

Al hacer doble clic sobre el componente COMPIM en Proteus, configurar:

| Parámetro | Valor a Configurar |
|:---|:---|
| **Physical Port** | `COM2` (el extremo Proteus del par virtual) |
| **Baud Rate** | `9600` |
| **Data Bits** | `8` |
| **Parity** | `None` (Sin paridad) |
| **Stop Bits** | `1` |
| **Physical Baud Rate** | `9600` (igual que Baud Rate) |

---

## 7. Configuración del Par de Puertos COM Virtuales (com0com)

ThermalMaster utiliza un par de puertos COM virtuales para conectar Proteus con el backend C#:

```
Proteus (COMPIM) ◄──► COM2 ◄──────────────► COM1 ◄──► C# ASP.NET Core
                        [par virtual com0com]
```

### Instalar y Configurar com0com

1. Descargar **com0com** (Null-modem emulator) desde: https://sourceforge.net/projects/com0com/
2. Ejecutar el instalador como administrador.
3. En la consola de configuración de com0com, crear el par:
   ```
   install PortName=COM1 PortName=COM2
   ```
4. Verificar en el Administrador de Dispositivos de Windows que aparecen COM1 y COM2 bajo "Puertos (COM y LPT)".
5. En Proteus → COMPIM → Physical Port = `COM2`.
6. En C# → `appsettings.json` → `Hardware:DefaultPort = "COM1"`.

---

## 8. ¿Cuándo SÍ sería necesario el MAX232?

El convertidor MAX232 (o similar: MAX3232, SP3232) **sólo es necesario en hardware físico real**,
cuando el Arduino UNO físico se conecta directamente al puerto COM DB9 de un PC real
que opera con niveles RS-232 (±12V).

| Escenario | ¿Necesita MAX232? |
|:---|:---:|
| Proteus simulado + COMPIM + com0com (nuestro caso) | ❌ No |
| Arduino físico + cable USB a Arduino IDE | ❌ No (el chip CH340/FTDI hace la conversión) |
| Arduino físico + puerto COM DB9 real de PC | ✅ Sí |
| Arduino físico + adaptador USB-Serial (CH340, CP2102, FTDI) | ❌ No |

---

## 9. Verificación de la Comunicación

Para comprobar que el enlace Proteus ↔ C# funciona correctamente:

1. **Iniciar la simulación en Proteus** (el COMPIM abrirá COM2 automáticamente).
2. **Iniciar el backend C#** configurado en COM1.
3. **Monitor en Arduino IDE:** Tools → Serial Monitor → COM1 a 9600 baudios.
4. **Verificar:** El dashboard SCADA debe mostrar estado "CONECTADO" y comenzar a recibir telemetría JSON.

Si la conexión falla, verificar:
- Que com0com esté instalado y el par COM1↔COM2 esté activo.
- Que Proteus tenga la simulación corriendo (no pausada).
- Que el backend C# no esté ya usando COM1 desde otra instancia.
- Que la velocidad de baudios sea exactamente 9600 en ambos extremos.
