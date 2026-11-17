# Especificación del Firmware para Arduino UNO (DHT22 + Relés + LCD I2C)

## 1. Muestreo Digital del Sensor DHT22

El sensor **DHT22** (AM2302) se comunica digitalmente a través del **Pin D2** de Arduino utilizando la biblioteca oficial `DHT`:

```cpp
#include <DHT.h>
#define DHTPIN 2
#define DHTTYPE DHT22

DHT dht(DHTPIN, DHTTYPE);
```

- **Lectura de Temperatura:** `float temperature = dht.readTemperature();` (en grados Celsius).
- **Lectura de Humedad:** `float humidity = dht.readHumidity();` (en porcentaje relativo %).
- **Rango de Operación:**
  - Temperatura: $-40^\circ\text{C}$ a $+80^\circ\text{C}$ (precisión $\pm 0.5^\circ\text{C}$).
  - Humedad: $0\%$ a $100\%$ HR (precisión $\pm 2\% - 5\%$).
- **Validación de Datos:** En caso de falla en la trama digital, la función `isnan()` detecta el error y descarta el paquete para evitar decisiones erróneas sobre los relés.

---

## 2. Lógica Inteligente de Climatización y Seguridad de Actuadores

El sistema dispone de dos actuadores de potencia gobernados por relés independientes:
- **Ventilador (Refrigeración):** Pin digital **D7**.
- **Lámpara (Calefacción):** Pin digital **D8**.

### A. Modo Automático (Banda de Confort con Histéresis de 1.0 °C)

Definición de umbrales nominales:
- $T_{\text{min}} = 18.0^\circ\text{C}$
- $T_{\text{max}} = 28.0^\circ\text{C}$
- $\Delta H = 1.0^\circ\text{C}$ (Banda de histéresis)

#### Reglas de Activación y Apagado:
1. **Zona de Frío Extremo ($T < T_{\text{min}}$):**
   - Activar Lámpara (Pin D8 = `HIGH`).
   - Apagar Ventilador (Pin D7 = `LOW`).
2. **Zona de Sobrecalentamiento ($T > T_{\text{max}}$):**
   - Activar Ventilador (Pin D7 = `HIGH`).
   - Apagar Lámpara (Pin D8 = `LOW`).
3. **Zona de Confort Térmico ($18.0^\circ\text{C} \le T \le 28.0^\circ\text{C}$):**
   - Apagar ambos actuadores (Pin D7 = `LOW`, Pin D8 = `LOW`).
4. **Histéresis para Conmutación Estable:**
   - La lámpara se apaga cuando la temperatura sube y supera $T_{\text{min}} + \Delta H = 19.0^\circ\text{C}$.
   - El ventilador se apaga cuando la temperatura desciende por debajo de $T_{\text{max}} - \Delta H = 27.0^\circ\text{C}$.

### B. Regla Fundamental de Seguridad (Enclavamiento de Exclusión Mutua)
> **REGLA DE SEGURIDAD CRÍTICA:**
> En modo automático es físicamente inadmisible activar simultáneamente la lámpara (calefacción) y el ventilador (refrigeración), ya que causaría desperdicio energético severo y esfuerzo térmico contradictorio.
> El algoritmo del microcontrolador implementa una comprobación de seguridad antes de escribir las salidas:
> ```cpp
> if (fanActive && lampActive) {
>     // Condición de falla: Apagar ambos por seguridad
>     digitalWrite(PIN_FAN, LOW);
>     digitalWrite(PIN_LAMP, LOW);
> }
> ```

### C. Modo Manual
Permite al operador web encender o apagar cualquiera de los dos relés de forma individual para tareas de mantenimiento o diagnóstico.

---

## 3. Protocolo de Comunicación Serial UART (9600 Baud, 8-N-1)

### A. Trama de Telemetría (Arduino UNO -> PC)
Emitida periódicamente cada $1000\text{ ms} - 1500\text{ ms}$ usando temporización no bloqueante (`millis()`):

```json
{"temp":25.4,"hum":55.0,"fan":0,"lamp":0,"mode":"AUTO","alarm":0}
```

- `temp`: Temperatura en °C calculada por el DHT22.
- `hum`: Humedad relativa en % calculada por el DHT22.
- `fan`: Estado del relé del ventilador (`0` = Apagado, `1` = Encendido).
- `lamp`: Estado del relé de la lámpara (`0` = Apagada, `1` = Encendida).
- `mode`: Modo de trabajo (`"AUTO"` o `"MANUAL"`).
- `alarm`: Bandera de alerta (`0` = Confort, `1` = Frío extremo, `2` = Calor extremo).

### B. Tramas de Comando (PC -> Arduino UNO)

| Comando Recibido | Parámetro | Acción del Microcontrolador | Respuesta ACK |
| :--- | :--- | :--- | :--- |
| `{"cmd":"FAN_ON"}` | Ninguno | Activa Relé D7 (sólo en modo MANUAL) | `{"ack":"FAN_ON","status":"OK"}` |
| `{"cmd":"FAN_OFF"}` | Ninguno | Desactiva Relé D7 (sólo en modo MANUAL) | `{"ack":"FAN_OFF","status":"OK"}` |
| `{"cmd":"LAMP_ON"}` | Ninguno | Activa Relé D8 (sólo en modo MANUAL) | `{"ack":"LAMP_ON","status":"OK"}` |
| `{"cmd":"LAMP_OFF"}` | Ninguno | Desactiva Relé D8 (sólo en modo MANUAL) | `{"ack":"LAMP_OFF","status":"OK"}` |
| `{"cmd":"SET_MODE","val":"AUTO"}` | `"AUTO"` | Regresa a control autónomo por DHT22 | `{"ack":"SET_MODE","val":"AUTO"}` |
| `{"cmd":"SET_MODE","val":"MANUAL"}` | `"MANUAL"` | Habilita control manual desde navegador | `{"ack":"SET_MODE","val":"MANUAL"}` |
| `{"cmd":"SET_LIMITS","min":18,"max":28}` | Numérico | Actualiza umbrales $T_{\text{min}}$ y $T_{\text{max}}$ | `{"ack":"SET_LIMITS","status":"OK"}` |
| `{"cmd":"PING"}` | Ninguno | Prueba de enlace y latencia | `{"pong":true}` |

---

## 4. Visualización en Pantalla LCD 16x2 I2C (Pines A4 / A5)

El display alfanumérico muestra el estado del microclima localmente en Proteus:

- **Fila 1:** `T:24.5C  H:55.0%`
- **Fila 2:** `V:OFF L:OFF [AUTO]`
