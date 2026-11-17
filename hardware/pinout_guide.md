# Guía Definitiva de Conexión y Asignación de Pines (Pinout)

## Microcontrolador: Arduino UNO (ATmega328P) en Proteus 8.17

Esta tabla detalla la correspondencia estricta de cada pin entre el Arduino UNO, el sensor digital DHT22, los módulos de relé independientes y la pantalla LCD I2C.

| Pin Arduino | Dirección | Componente Conectado | Identificador en Proteus | Función / Descripción |
| :--- | :--- | :--- | :--- | :--- |
| **D0 (RX)** | Entrada Digital | COMPIM (DB9 / UART) | Pin 3 (TXD del COMPIM) | Recepción de órdenes de control enviadas desde C# ASP.NET Core |
| **D1 (TX)** | Salida Digital | COMPIM (DB9 / UART) | Pin 2 (RXD del COMPIM) | Transmisión de telemetría (JSON) hacia C# ASP.NET Core |
| **D2** | Bidireccional Digital | Sensor Digital DHT22 | Pin 2 (DATA / OUT del DHT22) | Protocolo 1-Wire: Lectura digital de Temperatura (°C) y Humedad (%) |
| **D7** | Salida Digital | Módulo de Relé 1 (Ventilador) | Pin IN1 del Relé | Control del ventilador: HIGH = Relé cierra contacto (Enfriamiento ON) |
| **D8** | Salida Digital | Módulo de Relé 2 (Lámpara) | Pin IN2 del Relé | Control de la lámpara: HIGH = Relé cierra contacto (Calefacción ON) |
| **A4 (SDA)** | Bidireccional I2C | Pantalla LCD 16x2 I2C | Pin SDA del adaptador PCF8574 | Línea de datos serie del bus I2C (Serial Data) |
| **A5 (SCL)** | Salida Reloj I2C | Pantalla LCD 16x2 I2C | Pin SCL del adaptador PCF8574 | Línea de reloj del bus I2C (Serial Clock) |
| **5V** | Alimentación | DHT22, Relés, LCD I2C | VCC (+5V) | Suministro de voltaje continuo de 5V para la lógica |
| **GND** | Referencia | Todos los módulos | GND (0V Común) | Masa de referencia común para todos los subcircuitos |

---

## Verificación de Ausencia de Conflictos de Pines

- **Bus I2C Libre:** El display LCD utiliza los pines analógicos A4 y A5 en su función alternativa de hardware I2C (`Wire`). Los pines D3, D4, D5, D6, D11 y D12 quedan **totalmente libres**, eliminando el conexionado paralelo anterior.
- **Sensor Digital DHT22:** Conectado en exclusiva al **Pin D2**. Requiere un resistor de *pull-up* de $4.7\text{ k}\Omega$ a $10\text{ k}\Omega$ entre el pin DATA y +5V.
- **Relé del Ventilador:** Pin **D7**.
- **Relé de la Lámpara:** Pin **D8**.
- **UART / COMPIM:** Pines **D0 y D1**.

---

## Esquema Eléctrico de los Actuadores por Relé

Ambos actuadores (ventilador y lámpara) se gobiernan mediante módulos de relé de 5V con aislamiento optoacoplado:

### 1. Actuador de Refrigeración (Ventilador - Pin D7)
- **Entrada de Control (IN):** Pin D7 de Arduino UNO.
- **Alimentación del Módulo:** VCC a +5V, GND a GND.
- **Circuito de Carga:**
  - Contacto Común (COM) del relé: A la fuente de alimentación del ventilador (+12V o +5V).
  - Contacto Normalmente Abierto (NO): Al borne positivo (+) del ventilador / motor DC.
  - Borne negativo (-) del ventilador: A tierra (GND).
  - *Comportamiento:* Cuando D7 se pone en nivel ALTO (`HIGH`), el relé conmutará y el ventilador comenzará a girar.

### 2. Actuador de Calefacción (Lámpara - Pin D8)
- **Entrada de Control (IN):** Pin D8 de Arduino UNO.
- **Alimentación del Módulo:** VCC a +5V, GND a GND.
- **Circuito de Carga:**
  - Contacto Común (COM) del relé: A la fase o positivo de la alimentación de la lámpara.
  - Contacto Normalmente Abierto (NO): Al borne positivo de la lámpara (simulada como resistencia calefactora / foco).
  - Borne negativo / neutro: Al neutro o tierra de retorno.
  - *Comportamiento:* Cuando D8 se pone en nivel ALTO (`HIGH`), el relé cerrará el circuito y la lámpara encenderá generando calor.

---

## Esquema de Conexión del Display LCD 16x2 I2C (PCF8574)

El módulo adaptador I2C reduce de 16 cables a solo 4 cables:
1. **GND:** Al bus GND común de Arduino.
2. **VCC:** Al bus +5V de Arduino.
3. **SDA:** Conectado al pin **A4** de Arduino UNO.
4. **SCL:** Conectado al pin **A5** de Arduino UNO.
- Dirección I2C típica: `0x27` o `0x3F` (gestionada mediante la biblioteca `LiquidCrystal_I2C.h`).
