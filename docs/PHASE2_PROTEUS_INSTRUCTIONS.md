# Fase 2 — Etapa 1: Circuito Electrónico en Proteus 8.17
## ThermalMaster SCADA — Esquema Definitivo con Drivers de Relé

> Estado: **APROBADA PARA IMPLEMENTACIÓN** (aprobación del usuario 2026-11-16)

---

## 1. Lista Completa de Componentes

| Ref. | Componente | Nombre de búsqueda en Proteus | Cant. | Valor / Notas |
|:---:|:---|:---|:---:|:---|
| U1 | Arduino UNO | `ARDUINO UNO R3` | 1 | Microcontrolador principal |
| U2 | Sensor DHT22 | `DHT22` | 1 | Temperatura + Humedad digital |
| U3 | Expansor I2C | `PCF8574` | 1 | Interface I2C → LCD paralela |
| LCD1 | Pantalla LCD 16×2 | `LM016L` | 1 | Display alfanumérico |
| K1 | Relé SPDT 5V | `RELAY-SPDT` | 1 | Control ventilador (D7) |
| K2 | Relé SPDT 5V | `RELAY-SPDT` | 1 | Control lámpara (D8) |
| Q1 | Transistor NPN | `BC547` o `2N2222` | 1 | Driver del relé K1 |
| Q2 | Transistor NPN | `BC547` o `2N2222` | 1 | Driver del relé K2 |
| D1 | Diodo flyback | `1N4007` | 1 | Protección contra EMF inversa K1 |
| D2 | Diodo flyback | `1N4007` | 1 | Protección contra EMF inversa K2 |
| M1 | Motor DC | `MOTOR` o `DC MOTOR` | 1 | Representa el ventilador |
| LA1 | Lámpara 5V | `LAMP` | 1 | Representa la calefacción |
| COM1 | Puerto COM virtual | `COMPIM` | 1 | Puente Proteus ↔ Windows COM2 |
| R1 | Resistencia pull-up | `RES` 4.7 kΩ | 1 | Pull-up DATA del DHT22 |
| R2 | Resistencia base Q1 | `RES` 1 kΩ | 1 | Limitadora de corriente de base |
| R3 | Resistencia base Q2 | `RES` 1 kΩ | 1 | Limitadora de corriente de base |
| PWR | Terminal +5V | `POWER` | — | Bus de alimentación +5V |
| GND | Terminal GND | `GROUND` | — | Bus de tierra común |

---

## 2. Tabla Definitiva de Pines Arduino UNO (Sin Conflictos)

| Pin Arduino | Señal | Destino | Componente |
|:---:|:---|:---|:---|
| **D0 (RX)** | UART RX | COMPIM TXD (pin 3) | COM1 |
| **D1 (TX)** | UART TX | COMPIM RXD (pin 2) | COM1 |
| **D2** | DHT22 DATA | Nodo R1 / DHT22 Pin 2 | U2 |
| **D7** | Fan Relay IN | R2 → Base Q1 | Q1→K1 |
| **D8** | Lamp Relay IN | R3 → Base Q2 | Q2→K2 |
| **A4** | I2C SDA | SDA del PCF8574 | U3 |
| **A5** | I2C SCL | SCL del PCF8574 | U3 |
| **5V** | Alimentación | Bus VCC | Todos |
| **GND** | Tierra | Bus GND | Todos |
| D3–D6, D9–D13, A0–A3 | — | Libres | — |

---

## 3. Circuito del Driver de Relé — Análisis y Protección

### ¿Por qué se necesita un transistor NPN?

El pin de salida digital del Arduino UNO puede suministrar como máximo **40 mA**.
Una bobina de relé de 5V típica consume entre **70 mA y 100 mA**.
Conectar la bobina directamente al pin destruiría el ATmega328P.

La solución es un **transistor NPN en configuración de emisor común** que actúa como interruptor:

```
                              +5V
                               │
                           [Bobina]
                               │
                    1N4007 ──►│← (Cátodo → +5V, Ánodo → Colector)
                               │
Arduino D7 ─── [1 kΩ] ─── Base (Q1 BC547)
                           Colector ─── Bobina (terminal −)
                           Emisor  ─── GND
```

### Comportamiento del Driver

| Señal D7 | Estado Q1 | Corriente por bobina | Estado del relé |
|:---:|:---:|:---:|:---:|
| LOW (0V) | Corte (OFF) | 0 mA | Desactivado (NC cerrado) |
| HIGH (5V) | Saturación (ON) | ~70–100 mA | Activado (NO cerrado) |

### El Diodo Flyback (1N4007) — Protección Obligatoria

Cuando Q1 pasa de saturación a corte, la bobina del relé genera un **pico de tensión inversa** (efecto de autoinducción, L × dI/dt) que puede superar los 50V. Sin el diodo, este pico destruye el transistor.

El **1N4007** se coloca en antiparalelo con la bobina:
- **Cátodo** → Terminal (+) de la bobina (lado +5V)
- **Ánodo** → Terminal (−) de la bobina (Colector de Q1)

Durante operación normal, el diodo está en inversa y no conduce. Al apagar el relé, conduce el pico inverso y lo disipa de forma segura.

---

## 4. Circuito Completo — Conexión Pin a Pin

### 4.1 DHT22 — Sensor Digital

```
         +5V
          │
         [R1: 4.7 kΩ]            Resistencia pull-up OBLIGATORIA
          │
          ├────────────────────── Arduino D2 (DATA)
          │
     ┌────┴────┐
     │  DHT22  │
     │  U2     │
     │ Pin1=VCC├─── +5V
     │ Pin2=DAT├─── (ya conectado arriba al nodo con R1 y D2)
     │ Pin3=N/C├─── Sin conexión
     │ Pin4=GND├─── GND
     └─────────┘
```

**Pasos en Proteus:**
1. Buscar `DHT22` en el picker de componentes (Libraries → Microcontroller ICs o buscar "DHT22").
2. Colocar el componente en la parte superior izquierda del esquemático.
3. Colocar resistencia `RES` de **4.7 kΩ** (R1).
4. Conectar un extremo de R1 a **+5V** y el otro extremo al **Pin 2 (DATA)** del DHT22.
5. Conectar ese mismo nodo (Pin2 / extremo de R1) a **Arduino D2**.
6. Conectar **Pin 1** del DHT22 a **+5V**.
7. Conectar **Pin 4** del DHT22 a **GND**.
8. Dejar **Pin 3** sin conexión (N/C).

> ⚠️ **Nota sobre Proteus y DHT22:** Si el componente DHT22 no aparece en la biblioteca estándar,
> buscarlo como `AM2302`. Si tampoco aparece, buscar en la librería "SENSORS" o instalar
> la librería de Proteus desde el menú **Library → Library Manager**.
> En Proteus 8.17 está disponible en la mayoría de instalaciones.

---

### 4.2 Driver del Relé K1 — Ventilador (D7)

```
+5V ────────────────────────────────────── K1 Bobina (+)
                                            │
                                       [1N4007 D1]  Cátodo → +5V
                                            │        Ánodo  → Colector Q1
                                       K1 Bobina (−)
                                            │
Arduino D7 ── [R2: 1 kΩ] ── Base (Q1) ── Colector Q1
                              Emisor Q1 ── GND

Contactos del Relé K1 (SPDT):
  COM  ────────────────────────────────── +5V  (alimentación de carga)
  NO   ────────────────────────────────── Motor M1 terminal (+)
  NC   ────────────────────────────────── Sin conexión (o GND como seguridad)
  Motor M1 terminal (−) ─────────────── GND
```

**Pasos en Proteus:**
1. Buscar y colocar transistor `BC547` (Q1) en el esquemático.
2. Colocar resistencia `RES` de **1 kΩ** (R2).
3. Conectar **Arduino D7** → R2 → **Base de Q1**.
4. Conectar **Emisor de Q1** → **GND**.
5. Colocar relé `RELAY-SPDT` (K1) en el esquemático.
6. Conectar **Terminal (+) de la bobina K1** → **+5V**.
7. Conectar **Terminal (−) de la bobina K1** → **Colector de Q1**.
8. Colocar diodo `1N4007` (D1):
   - **Cátodo** → Terminal (+) de la bobina K1 (nodo +5V).
   - **Ánodo** → Terminal (−) de la bobina K1 (nodo del Colector).
9. Conectar **COM del relé K1** → **+5V**.
10. Conectar **NO del relé K1** → **Terminal (+) del Motor DC (M1)**.
11. Conectar **Terminal (−) del Motor DC** → **GND**.
12. Dejar **NC del relé K1** sin conectar.

---

### 4.3 Driver del Relé K2 — Lámpara (D8)

Circuito **idéntico** al driver K1 pero usando **D8**, **Q2**, **R3**, **D2** y **LA1** (lámpara):

```
+5V ────────────────────────────────────── K2 Bobina (+)
                                            │
                                       [1N4007 D2]  Cátodo → +5V
                                            │        Ánodo  → Colector Q2
                                       K2 Bobina (−)
                                            │
Arduino D8 ── [R3: 1 kΩ] ── Base (Q2) ── Colector Q2
                              Emisor Q2 ── GND

Contactos del Relé K2 (SPDT):
  COM  ────────────────────────────────── +5V
  NO   ────────────────────────────────── Lámpara LA1 terminal (+)
  NC   ────────────────────────────────── Sin conexión
  Lámpara LA1 terminal (−) ──────────── GND
```

**Pasos en Proteus:**
1. Repetir exactamente los pasos 1–12 del apartado 4.2, sustituyendo:
   - D7 → **D8**
   - Q1 → **Q2** (nuevo componente BC547)
   - R2 → **R3** (nuevo componente 1 kΩ)
   - D1 → **D2** (nuevo componente 1N4007)
   - K1 → **K2** (nuevo componente RELAY-SPDT)
   - Motor M1 → **Lámpara LA1** (`LAMP` en Proteus)

---

### 4.4 LCD 16×2 con Interface I2C (PCF8574 + LM016L)

```
Arduino A4 (SDA) ──────────── PCF8574 SDA
Arduino A5 (SCL) ──────────── PCF8574 SCL
+5V              ──────────── PCF8574 VCC
GND              ──────────── PCF8574 GND
GND              ──────────── PCF8574 A0  ┐
GND              ──────────── PCF8574 A1  ├─ Dirección I2C = 0x27
GND              ──────────── PCF8574 A2  ┘

PCF8574 P0  ──── LM016L RS
PCF8574 P1  ──── LM016L RW
PCF8574 P2  ──── LM016L E
PCF8574 P3  ──── LM016L BLA (retroiluminación, ánodo)
PCF8574 P4  ──── LM016L D4
PCF8574 P5  ──── LM016L D5
PCF8574 P6  ──── LM016L D6
PCF8574 P7  ──── LM016L D7

LM016L VDD  ──── +5V
LM016L VSS  ──── GND
LM016L VEE  ──── GND  (contraste máximo, ajustable con potenciómetro)
LM016L BLK  ──── GND  (retroiluminación, cátodo)
LM016L RW   ──── GND  (modo escritura permanente, o a P1 del PCF8574)
```

**Pasos en Proteus:**
1. Buscar y colocar `LM016L` en el esquemático.
2. Buscar y colocar `PCF8574` en el esquemático.
3. Conectar **PCF8574 SDA** → **Arduino A4**.
4. Conectar **PCF8574 SCL** → **Arduino A5**.
5. Conectar **PCF8574 VCC** → **+5V** y **PCF8574 GND** → **GND**.
6. Conectar **A0, A1, A2** del PCF8574 → **GND** (dirección 0x27).
7. Conectar P0–P7 del PCF8574 a RS, RW, E, BLA, D4, D5, D6, D7 del LM016L según la tabla.
8. Conectar **LM016L VDD** → **+5V**.
9. Conectar **LM016L VSS** → **GND**.
10. Conectar **LM016L VEE** → **GND** (contraste fijo al máximo en simulación).
11. Conectar **LM016L BLK** → **GND**.

> **Dirección I2C:** A0=A1=A2=GND → `0x27`. El firmware usa `LiquidCrystal_I2C lcd(0x27, 16, 2)`.

---

### 4.5 COMPIM — Interfaz COM Virtual

```
Arduino D1 (TX) ───────────────────── COMPIM RXD  (datos Arduino → PC)
Arduino D0 (RX) ───────────────────── COMPIM TXD  (datos PC → Arduino)
GND             ───────────────────── COMPIM GND  (pin 5)
```

> ✅ **Conexión TTL directa, sin MAX232.** COMPIM opera internamente a niveles TTL.
> Ver `docs/COMPIM_SERIAL_COMPATIBILITY.md` para justificación técnica completa.

**Pasos en Proteus:**
1. Buscar `COMPIM` en el picker y colocarlo a la derecha del Arduino.
2. Conectar **COMPIM RXD** → **Arduino D1 (TX)**.
3. Conectar **COMPIM TXD** → **Arduino D0 (RX)**.
4. Conectar **COMPIM GND (pin 5)** → **GND** del circuito.
5. Doble clic en COMPIM → configurar:
   - **Physical Port:** `COM2`
   - **Baud Rate:** `9600`
   - **Data Bits:** `8`
   - **Parity:** `None`
   - **Stop Bits:** `1`
   - **Physical Baud Rate:** `9600`

---

## 5. Diagrama Completo del Circuito

```
+5V ──────┬──────────────────────────────────────────────────────────────
          │             ┌────────────────────────────────────────────┐
          │             │             ARDUINO UNO (U1)               │
          │             │                                            │
          │   ┌──R1─────┤ D2 ◄─── DATA                              │
          │   │  4.7kΩ  │                                            │
   ┌───── │ ──┤         │ D7 ───► R2 (1kΩ) ──► Base Q1              │
   │      │   │         │ D8 ───► R3 (1kΩ) ──► Base Q2              │
   │  DHT22   │         │                                            │
   │  Pin1=5V │         │ A4 ◄──► SDA ──── PCF8574 ──── LM016L      │
   │  Pin2=DAT┘         │ A5 ◄──► SCL ──── PCF8574                  │
   │  Pin4=GND          │                                            │
   │                    │ D1 (TX) ───────────────────► COMPIM RXD   │
   │                    │ D0 (RX) ◄─────────────────── COMPIM TXD   │
   │                    └────────────────────────────────────────────┘
   │
   │   DRIVER RELÉ K1 (VENTILADOR)        DRIVER RELÉ K2 (LÁMPARA)
   │   ─────────────────────────          ─────────────────────────
   │   +5V ─── [K1 Bobina(+)]            +5V ─── [K2 Bobina(+)]
   │            │                                  │
   │       [D1 1N4007]                       [D2 1N4007]
   │            │                                  │
   │       [K1 Bobina(−)] ─── Colector Q1    [K2 Bobina(−)] ─── Colector Q2
   │   D7──R2──Base Q1                       D8──R3──Base Q2
   │           Emisor Q1 ─── GND                   Emisor Q2 ─── GND
   │
   │   K1 COM ── +5V                       K2 COM ── +5V
   │   K1 NO  ── Motor(+) ── Motor(−)─GND  K2 NO  ── Lámpara(+) ─ Lámpara(−)─GND
   │
   └─────────────────────────────── GND ────────────────────────────────────────
```

---

## 6. Verificación Eléctrica — Lista de Comprobación

### Antes de iniciar la simulación en Proteus

| # | Verificación | ¿OK? |
|:---:|:---|:---:|
| 1 | Pull-up 4.7 kΩ entre DATA del DHT22 y +5V | ☐ |
| 2 | DHT22 Pin 1 → +5V, Pin 4 → GND | ☐ |
| 3 | Resistencias de base (1 kΩ cada una) entre D7→Q1 y D8→Q2 | ☐ |
| 4 | Emisores de Q1 y Q2 conectados a GND | ☐ |
| 5 | Diodos 1N4007 en antiparalelo sobre cada bobina de relé (cátodo hacia +5V) | ☐ |
| 6 | Terminal (+) de cada bobina de relé conectado a +5V | ☐ |
| 7 | Contactos NO de K1 alimentan el Motor M1 | ☐ |
| 8 | Contactos NO de K2 alimentan la Lámpara LA1 | ☐ |
| 9 | PCF8574 SDA → A4, SCL → A5, A0=A1=A2=GND | ☐ |
| 10 | LM016L VEE → GND (contraste), BLK → GND | ☐ |
| 11 | COMPIM RXD ← Arduino D1(TX), COMPIM TXD → Arduino D0(RX) | ☐ |
| 12 | COMPIM configurado en COM2, 9600, 8-N-1 | ☐ |
| 13 | No hay nodos flotantes (todos los pines tienen referencia) | ☐ |
| 14 | Bus GND único y continuo para todos los componentes | ☐ |

---

## 7. Orden de Ensamblaje en Proteus (Paso a Paso)

```
PASO 1 → Crear nuevo proyecto en Proteus 8.17
         File → New Project → Schematic Only → A4 Landscape

PASO 2 → Colocar terminales de alimentación
         Agregar terminales VCC (+5V) y GND en los bordes del esquema

PASO 3 → Colocar ARDUINO UNO R3 en el centro

PASO 4 → Colocar y cablear DHT22 + R1 (4.7kΩ) en esquina superior izquierda

PASO 5 → Colocar y cablear Driver K1: Q1 + R2 + D1 + K1 + Motor M1
         Posición: área inferior izquierda

PASO 6 → Colocar y cablear Driver K2: Q2 + R3 + D2 + K2 + Lámpara LA1
         Posición: área inferior derecha

PASO 7 → Colocar y cablear PCF8574 + LM016L
         Posición: área superior derecha

PASO 8 → Colocar y cablear COMPIM
         Posición: extremo derecho del esquema

PASO 9 → Trazar el bus GND común a todos los componentes

PASO 10 → Ejecutar Design → Electrical Rules Check (ERC)
          Corregir todos los errores antes de continuar

PASO 11 → Cargar el firmware .hex en Arduino UNO
          (disponible después de la Etapa 2 — Firmware)

PASO 12 → Iniciar simulación y verificar comportamiento
```

---

## 8. Carga del Firmware en el Arduino UNO (Proteus)

Una vez compilado en Arduino IDE (Etapa 2):

1. **Arduino IDE:** Sketch → **Export Compiled Binary** (Ctrl+Alt+S).
2. El archivo `.hex` se guarda en `hardware/firmware/`.
3. **Proteus:** doble clic sobre Arduino UNO → campo **"Program File"** → seleccionar el `.hex`.
4. Configurar **Clock Frequency: 16 MHz** (valor por defecto del UNO).
5. Iniciar simulación con **▶ Play**.

### Comportamiento esperado al iniciar la simulación:
- LCD muestra: `T:--.-C  H:--.-%` durante los primeros ~2 segundos.
- Tras la primera lectura exitosa del DHT22: `T:25.4C  H:55.0%`.
- El puerto COM2 (COMPIM) comienza a enviar tramas JSON cada ~2 segundos.
- Los relés K1 y K2 responden a los umbrales de temperatura configurados.

---

## 9. Notas de Simulación en Proteus

| Aspecto | Comportamiento en Proteus |
|:---|:---|
| **DHT22** | El sensor simula valores configurables desde sus propiedades (doble clic) |
| **Relés K1/K2** | El interruptor cambia de NC a NO cuando la bobina se energiza |
| **Motor DC** | Gira visualmente cuando el relé K1 lo alimenta |
| **Lámpara LA1** | Se ilumina visualmente cuando el relé K2 la alimenta |
| **COMPIM** | Requiere que com0com esté instalado y COM2 esté disponible en Windows |
| **Timing DHT22** | La simulación respeta el delay de 2 segundos entre lecturas |
| **LCD I2C** | Requiere que el PCF8574 esté correctamente cableado; sin eso la pantalla no muestra nada |
