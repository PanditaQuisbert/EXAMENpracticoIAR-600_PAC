/*
=========================================================
       THERMALMASTER - SISTEMA INTELIGENTE
       MONITOREO Y CONTROL DE TEMPERATURA AMBIENTAL

       Simulacion: Proteus 8.17
       Microcontrolador: Arduino UNO R3

       HARDWARE:
       - Sensor DHT22 (D2)
       - Relé 1: Ventilador (D7)
       - Relé 2: Lámpara (D8)
       - LCD LM044L 20x4 (paralelo)
       - COMPIM (comunicación serial)

       COMUNICACION:
       UART 9600 baudios, 8-N-1, JSON por línea (\n)

 PINES:
  D0  RX  <- COMPIM TXD
  D1  TX  -> COMPIM RXD
  D2      <- DHT22 DATA
  D3      -> LCD D7
  D4      -> LCD D6
  D5      -> LCD D5
  D6      -> LCD D4
  D7      -> RELE VENTILADOR
  D8      -> RELE LAMPARA
  D9      -> LCD E
  D10     -> LCD RS
  LCD RW  -> GND

 REGLAS AUTOMÁTICAS (umbrales directos):
  - T < 18.0°C                  : FRÍO   -> Ventilador OFF, Lámpara ON
  - 18.0°C <= T <= 28.0°C       : NORMAL -> Ventilador OFF, Lámpara OFF
  - 28.0°C <  T <  36.0°C       : CALOR  -> Ventilador ON,  Lámpara OFF
  - T >= 36.0°C                 : ALARMA -> Ventilador ON,  Lámpara OFF
  Humedad se lee y muestra siempre. En fallo de sensor: protección (actuadores OFF).

 NOTA: Firmware preparado para simulación con Proteus/COMPIM. No probado en hardware físico en este entorno.
=========================================================
*/

#include <Arduino.h>
#include <DHT.h>
#include <LiquidCrystal.h>
#include <math.h>

// -------------------- Hardware --------------------
#define DHTPIN   2
#define DHTTYPE  DHT22

#define PIN_FAN  7
#define PIN_LAMP 8

const int LCD_RS = 10;
const int LCD_E  = 9;
const int LCD_D4 = 6;
const int LCD_D5 = 5;
const int LCD_D6 = 4;
const int LCD_D7 = 3;

#define LCD_COLS 20
#define LCD_ROWS 4

DHT dht(DHTPIN, DHTTYPE);
LiquidCrystal lcd(LCD_RS, LCD_E, LCD_D4, LCD_D5, LCD_D6, LCD_D7);

// -------------------- Configuración --------------------
enum Mode { MODE_AUTO, MODE_MANUAL };
Mode currentMode = MODE_AUTO;

float minThreshold = 18.0;   // límite inferior NORMAL
float maxThreshold = 28.0;   // límite superior NORMAL
const float alarmThreshold = 36.0; // ALARMA >= 36°C (fijo según especificación)

bool fanOn = false;
bool lampOn = false;

float lastTemp = NAN;
float lastHum = NAN;
bool sensorOk = false;

unsigned long lastSensorReadMs = 0;
unsigned long lastTelemetryMs = 0;
unsigned long lastLcdUpdateMs = 0;

const unsigned long SENSOR_INTERVAL_MS = 2000;
const unsigned long TELEMETRY_INTERVAL_MS = 1500;
const unsigned long LCD_INTERVAL_MS = 500;

const unsigned long SERIAL_BAUD = 9600;
String rxBuffer = "";

// Pantallas de bienvenida
const char* welcomeScreens[][4] = {
  {"THERMALMASTER SCADA", "Arduino UNO + DHT22", "Proteus 8.17", ""},
  {"MONITOREO Y CONTROL", "TEMPERATURA/HUMEDAD", "Ventilador D7", "Lampara D8"},
  {"REGLAS AUTO", "FRIO <18C  NORMAL", "18-28C  CALOR >28C", "ALARMA >=36C"},
  {"INICIANDO SISTEMA", "Serial 9600 8N1", "COMPIM / Web", "ESPERE..."}
};
const int TOTAL_SCREENS = 4;
int welcomeIndex = 0;
bool welcomeActive = true;
unsigned long welcomeTimer = 0;
const unsigned long WELCOME_INTERVAL_MS = 2500;

// -------------------- LCD --------------------
void printLine(byte row, String message) {
  if (message.length() > LCD_COLS) message = message.substring(0, LCD_COLS);
  lcd.setCursor(0, row);
  lcd.print(message);
  for (int i = message.length(); i < LCD_COLS; i++) lcd.print(' ');
}

void showWelcomeScreen(int index) {
  lcd.clear();
  for (int row = 0; row < LCD_ROWS; row++) {
    printLine(row, String(welcomeScreens[index][row]));
  }
}

void updateWelcomeScreen() {
  if (!welcomeActive) return;
  unsigned long now = millis();
  if (now - welcomeTimer >= WELCOME_INTERVAL_MS) {
    welcomeTimer = now;
    welcomeIndex++;
    if (welcomeIndex < TOTAL_SCREENS) {
      showWelcomeScreen(welcomeIndex);
    } else {
      welcomeActive = false;
      lcd.clear();
    }
  }
}

// -------------------- Actuadores / Estados --------------------
void setActuators(bool fan, bool lamp) {
  // Protección: exclusión mutua
  if (fan && lamp) { fan = false; lamp = false; }
  fanOn = fan;
  lampOn = lamp;
  digitalWrite(PIN_FAN, fanOn ? HIGH : LOW);
  digitalWrite(PIN_LAMP, lampOn ? HIGH : LOW);
}

void applySafetyInterlock() {
  if (fanOn && lampOn) setActuators(false, false);
}

// 0=NORMAL, 1=FRIO, 2=CALOR, 3=ALARMA, 4=SENSOR_ERROR
int computeStateCode() {
  if (!sensorOk) return 4;
  if (lastTemp < minThreshold) return 1;                  // FRÍO
  if (lastTemp <= maxThreshold) return 0;                 // NORMAL
  if (lastTemp < alarmThreshold) return 2;                // CALOR
  return 3;                                               // ALARMA
}

const char* stateLabel() {
  switch (computeStateCode()) {
    case 1: return "FRIO";
    case 0: return "NORMAL";
    case 2: return "CALOR";
    case 3: return "ALARMA";
    default: return "SENSOR";
  }
}

void evaluateAutoControl(float temperature) {
  if (!sensorOk || isnan(temperature)) {
    setActuators(false, false);
    return;
  }

  if (temperature < minThreshold) {
    // FRÍO
    setActuators(false, true);
  } else if (temperature <= maxThreshold) {
    // NORMAL
    setActuators(false, false);
  } else if (temperature < alarmThreshold) {
    // CALOR
    setActuators(true, false);
  } else {
    // ALARMA >= 36
    setActuators(true, false);
  }
  applySafetyInterlock();
}

// -------------------- Sensor --------------------
void readSensor() {
  float humidity = dht.readHumidity();
  float temperature = dht.readTemperature();

  if (isnan(humidity) || isnan(temperature)) {
    sensorOk = false;
    // Protección ante fallo del sensor
    setActuators(false, false);
    return;
  }

  lastTemp = temperature;
  lastHum = humidity;
  sensorOk = true;

  if (currentMode == MODE_AUTO) {
    evaluateAutoControl(lastTemp);
  }
  applySafetyInterlock();
}

// -------------------- LCD --------------------
void updateStatusLCD() {
  if (welcomeActive) return;

  if (sensorOk) {
    printLine(0, "TEMP: " + String(lastTemp, 1) + " C");
    printLine(1, "HUM : " + String(lastHum, 1) + " %");
  } else {
    printLine(0, "TEMP: --.- C");
    printLine(1, "HUM : --.- %");
  }

  String modeStr = (currentMode == MODE_AUTO) ? "AUTO" : "MANUAL";
  printLine(2, "MODO:" + modeStr + " EST:" + String(stateLabel()));

  String status = "VENT:";
  status += fanOn ? "ON " : "OFF";
  status += " LAMP:";
  status += lampOn ? "ON" : "OFF";
  printLine(3, status);
}

// -------------------- Comunicación JSON --------------------
void sendAck(const String &command, const String &status = "OK") {
  Serial.print("{\"ack\":\"");
  Serial.print(command);
  Serial.print("\",\"status\":\"");
  Serial.print(status);
  Serial.println("\"}");
}

void sendTelemetry() {
  Serial.print("{\"temp\":");
  if (sensorOk) Serial.print(lastTemp, 1); else Serial.print("null");
  Serial.print(",\"hum\":");
  if (sensorOk) Serial.print(lastHum, 1); else Serial.print("null");
  Serial.print(",\"fan\":");
  Serial.print(fanOn ? 1 : 0);
  Serial.print(",\"lamp\":");
  Serial.print(lampOn ? 1 : 0);
  Serial.print(",\"mode\":\"");
  Serial.print(currentMode == MODE_AUTO ? "AUTO" : "MANUAL");
  Serial.print("\",\"state\":\"");
  Serial.print(stateLabel());
  Serial.print("\",\"alarm\":");
  Serial.print(computeStateCode());
  Serial.print(",\"sensor_ok\":");
  Serial.print(sensorOk ? "true" : "false");
  Serial.println("}");
}

bool jsonGetString(const String &json, const char *key, String &result) {
  String pattern = String("\"") + key + "\"";
  int start = json.indexOf(pattern);
  if (start < 0) return false;
  start += pattern.length();
  while (start < (int)json.length() && (json[start] == ' ' || json[start] == ':')) start++;
  if (start >= (int)json.length() || json[start] != '"') return false;
  start++;
  int end = json.indexOf('"', start);
  if (end < 0) return false;
  result = json.substring(start, end);
  return true;
}

bool jsonGetFloat(const String &json, const char *key, float &result) {
  String pattern = String("\"") + key + "\"";
  int start = json.indexOf(pattern);
  if (start < 0) return false;
  start += pattern.length();
  while (start < (int)json.length() && (json[start] == ' ' || json[start] == ':')) start++;
  int end = start;
  while (end < (int)json.length()) {
    char c = json[end];
    if (c == ',' || c == '}' || c == ' ' || c == '\r' || c == '\n') break;
    end++;
  }
  if (end == start) return false;
  result = json.substring(start, end).toFloat();
  return true;
}

void handleCommand(const String &line) {
  String cmd;
  if (!jsonGetString(line, "cmd", cmd)) return;
  cmd.toUpperCase();

  if (cmd == "PING") {
    Serial.println("{\"pong\":true}");
    return;
  }

  if (cmd == "SET_MODE") {
    String value;
    if (!jsonGetString(line, "val", value)) { sendAck("SET_MODE", "INVALID_VALUE"); return; }
    value.toUpperCase();
    if (value == "AUTO") {
      currentMode = MODE_AUTO;
      setActuators(false, false);
      if (sensorOk) evaluateAutoControl(lastTemp);
      sendAck("SET_MODE", "OK");
    } else if (value == "MANUAL") {
      currentMode = MODE_MANUAL;
      sendAck("SET_MODE", "OK");
    } else {
      sendAck("SET_MODE", "INVALID_VALUE");
    }
    return;
  }

  if (cmd == "SET_LIMITS") {
    float newMin, newMax;
    bool hasMin = jsonGetFloat(line, "min", newMin);
    bool hasMax = jsonGetFloat(line, "max", newMax);
    if (hasMin && hasMax && isfinite(newMin) && isfinite(newMax) && newMin < newMax && newMax < alarmThreshold) {
      minThreshold = newMin;
      maxThreshold = newMax;
      if (currentMode == MODE_AUTO && sensorOk) evaluateAutoControl(lastTemp);
      sendAck("SET_LIMITS", "OK");
    } else {
      sendAck("SET_LIMITS", "INVALID_LIMITS");
    }
    return;
  }

  if (cmd == "FAN_ON") {
    if (currentMode != MODE_MANUAL) { sendAck("FAN_ON", "AUTO_MODE"); }
    else { setActuators(true, false); sendAck("FAN_ON", "OK"); }
    return;
  }
  if (cmd == "FAN_OFF") {
    if (currentMode != MODE_MANUAL) { sendAck("FAN_OFF", "AUTO_MODE"); }
    else { setActuators(false, lampOn); sendAck("FAN_OFF", "OK"); }
    return;
  }
  if (cmd == "LAMP_ON") {
    if (currentMode != MODE_MANUAL) { sendAck("LAMP_ON", "AUTO_MODE"); }
    else { setActuators(false, true); sendAck("LAMP_ON", "OK"); }
    return;
  }
  if (cmd == "LAMP_OFF") {
    if (currentMode != MODE_MANUAL) { sendAck("LAMP_OFF", "AUTO_MODE"); }
    else { setActuators(fanOn, false); sendAck("LAMP_OFF", "OK"); }
    return;
  }
  if (cmd == "STOP_ALL") {
    setActuators(false, false);
    sendAck("STOP_ALL", "OK");
    return;
  }

  sendAck(cmd, "UNKNOWN");
}

void readSerialCommands() {
  while (Serial.available() > 0) {
    char received = (char)Serial.read();
    if (received == '\n') {
      String line = rxBuffer;
      rxBuffer = "";
      line.trim();
      if (line.length() > 0) handleCommand(line);
    } else if (received != '\r') {
      if (rxBuffer.length() < 250) rxBuffer += received;
      else rxBuffer = "";
    }
  }
}

// -------------------- Setup / Loop --------------------
void setup() {
  pinMode(PIN_FAN, OUTPUT);
  pinMode(PIN_LAMP, OUTPUT);
  setActuators(false, false);

  Serial.begin(SERIAL_BAUD);
  lcd.begin(LCD_COLS, LCD_ROWS);
  lcd.clear();
  dht.begin();

  welcomeIndex = 0;
  welcomeActive = true;
  welcomeTimer = millis();
  showWelcomeScreen(welcomeIndex);
}

void loop() {
  unsigned long now = millis();

  readSerialCommands();
  updateWelcomeScreen();

  if (now - lastSensorReadMs >= SENSOR_INTERVAL_MS) {
    lastSensorReadMs = now;
    readSensor();
  }

  if (now - lastLcdUpdateMs >= LCD_INTERVAL_MS) {
    lastLcdUpdateMs = now;
    updateStatusLCD();
  }

  if (now - lastTelemetryMs >= TELEMETRY_INTERVAL_MS) {
    lastTelemetryMs = now;
    sendTelemetry();
  }

  applySafetyInterlock();
}
