# Especificación de Simulación Electrónica en Proteus 8.17 (Definitiva)

Este documento detalla la lista de componentes electrónicos en Proteus 8.17, las bibliotecas y las directivas de montaje para la **Fase 2**.

## 1. Lista Formal de Componentes Electrónicos

| Cant. | Componente | Nombre en Biblioteca Proteus | Especificación y Rol en el Circuito |
| :---: | :--- | :--- | :--- |
| **1** | **Microcontrolador** | `ARDUINO UNO R3` | Microcontrolador ATmega328P para control digital y comunicación serial. |
| **1** | **Sensor de Clima** | `DHT22` (o `AM2302`) | Sensor digital calibrado de temperatura y humedad relativa mediante bus serie propietario de un hilo (*1-Wire*). |
| **1** | **Resistencia Pull-up** | `RES` (4.7 kΩ o 10 kΩ) | Resistencia obligatoria conectada entre el pin DATA del DHT22 y el riel +5V para mantener la línea en reposo. |
| **2** | **Relés de Potencia** | `RELAY` (o módulo de relé 5V) | Relés electromecánicos de 5V para aislamiento galvánico y conmutación de cargas: <br>• Relé 1 en **Pin D7** (Ventilador)<br>• Relé 2 en **Pin D8** (Lámpara). |
| **1** | **Ventilador DC** | `MOTOR-DC` (o `FAN`) | Actuador de refrigeración conectado a los contactos NO (Normally Open) del Relé 1. |
| **1** | **Lámpara / Calefactor** | `LAMP` (12V o foco resistivo) | Actuador de calefacción conectado a los contactos NO (Normally Open) del Relé 2. |
| **1** | **Display LCD I2C** | `LM016L` + `PCF8574` (o `LCD16X2-I2C`) | Pantalla alfanumérica de 16 caracteres por 2 líneas gobernada por el bus serie I2C (2 hilos: SDA en A4, SCL en A5). |
| **1** | **Módulo Serial COMPIM** | `COMPIM` | Bloque transceptor RS-232 / UART para vincular Proteus con el puerto serie virtual Windows (COM2). |
| **1** | **Terminal Power** | `POWER` (+5V) | Rail de alimentación continua regulada para Arduino, DHT22, relés y LCD. |
| **1** | **Terminal Ground** | `GROUND` (0V) | Masa de referencia común para todos los circuitos. |

---

## 2. Configuración Esencial del Componente COMPIM

Para enlazar la simulación de Proteus 8.17 con el software C# ASP.NET Core:

1. Haga doble clic sobre el componente `COMPIM` en Proteus.
2. Configure las siguientes propiedades:
   - **Physical Port:** `COM2` (puerto virtual asignado a Proteus en com0com / VSPE).
   - **Physical Baud Rate:** `9600`.
   - **Physical Data Bits:** `8`.
   - **Physical Parity:** `NONE`.
   - **Physical Stop Bits:** `1`.
   - **Virtual Baud Rate:** `9600`.
   - **Virtual Data Bits:** `8`.
   - **Virtual Parity:** `NONE`.
   - **Virtual Stop Bits:** `1`.
3. Verifique el cruce de líneas seriales:
   - **TXD del COMPIM** (Pin 3) $\longrightarrow$ **RX (Pin D0)** de Arduino UNO.
   - **RXD del COMPIM** (Pin 2) $\longrightarrow$ **TX (Pin D1)** de Arduino UNO.

---

## 3. Consideraciones Técnicas del Sensor DHT22

- A diferencia de sensores analógicos de tensión (como el LM35 que requería conversión ADC en pasos de voltaje), el **DHT22** utiliza un transductor capacitivo de polímero para la humedad y un termistor de coeficiente negativo de temperatura (NTC) para la temperatura, integrando su propio convertidor A/D de 14 bits y un microprocesador interno.
- La lectura se realiza a través de un único pin de datos (**Pin D2** de Arduino) enviando un pulso de sincronismo inicial de al menos $18\text{ ms}$, seguido de una trama de 40 bits con verificación de *checksum*:
  - 16 bits de humedad (en décimas de %).
  - 16 bits de temperatura (en décimas de °C).
  - 8 bits de suma de verificación.
- Frecuencia máxima de muestreo recomendada: Cada $1.5\text{ s} - 2\text{ s}$ para garantizar estabilidad térmica en el elemento sensor.
