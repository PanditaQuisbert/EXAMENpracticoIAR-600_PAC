/**
 * ThermalMaster SCADA - Dashboard Client Controller
 * Integración: SignalR + API REST + visualización en tiempo real (DHT22, relés D7/D8, LCD I2C)
 */
document.addEventListener('DOMContentLoaded', () => {
    // Referencias DOM
    const currentTimeEl = document.getElementById('currentTime');
    const currentDateEl = document.getElementById('currentDate');
    const pulseIndicator = document.getElementById('pulseIndicator');
    const sidebarComStatus = document.getElementById('sidebarComStatus');
    const connectionPill = document.getElementById('connectionPill');
    const connectionPillText = document.getElementById('connectionPillText');
    const serialConsole = document.getElementById('serialConsole');
    const topModeBadge = document.getElementById('topModeBadge');

    const kpiTemp = document.getElementById('kpiTemp');
    const tempBarFill = document.getElementById('tempBarFill');
    const tempAlertChip = document.getElementById('tempAlertChip');
    const kpiHumidity = document.getElementById('kpiHumidity');
    const humBarFill = document.getElementById('humBarFill');
    const humStatusChip = document.getElementById('humStatusChip');

    const fanWrapper = document.getElementById('fanWrapper');
    const fanStatusText = document.getElementById('fanStatusText');
    const fanStateChip = document.getElementById('fanStateChip');
    const btnFanOn = document.getElementById('btnFanOn');
    const btnFanOff = document.getElementById('btnFanOff');

    const lampWrapper = document.getElementById('lampWrapper');
    const lampStatusText = document.getElementById('lampStatusText');
    const lampStateChip = document.getElementById('lampStateChip');
    const btnLampOn = document.getElementById('btnLampOn');
    const btnLampOff = document.getElementById('btnLampOff');

    const lcdTemp = document.getElementById('lcdTemp');
    const lcdHum = document.getElementById('lcdHum');
    const lcdFan = document.getElementById('lcdFan');
    const lcdLamp = document.getElementById('lcdLamp');
    const lcdMode = document.getElementById('lcdMode');

    const btnModeAuto = document.getElementById('btnModeAuto');
    const btnModeManual = document.getElementById('btnModeManual');
    const manualNotice = document.getElementById('manualNotice');
    const inputMinThreshold = document.getElementById('inputMinThreshold');
    const inputMaxThreshold = document.getElementById('inputMaxThreshold');
    const btnSaveThresholds = document.getElementById('btnSaveThresholds');
    const limitsHint = document.getElementById('limitsHint');

    const minTempDisplay = document.getElementById('minTempDisplay');
    const maxTempDisplay = document.getElementById('maxTempDisplay');
    const minTempTime = document.getElementById('minTempTime');
    const maxTempTime = document.getElementById('maxTempTime');
    const btnResetMinMax = document.getElementById('btnResetMinMax');
    const btnCheckPorts = document.getElementById('btnCheckPorts');
    const btnConnectSerial = document.getElementById('btnConnectSerial');
    const btnDisconnectSerial = document.getElementById('btnDisconnectSerial');
    const serialPortSelect = document.getElementById('serialPortSelect');
    const linkStatusChip = document.getElementById('linkStatusChip');
    const csharpPortBadge = document.getElementById('csharpPortBadge');
    const chartEmptyState = document.getElementById('chartEmptyState');
    const chartCanvas = document.getElementById('thermalChart');
    const simTempInput = document.getElementById('simTemp');
    const simHumInput = document.getElementById('simHum');
    const btnSimulateTelemetry = document.getElementById('btnSimulateTelemetry');

    const navItems = document.querySelectorAll('.nav-item');
    const contentSections = document.querySelectorAll('.content-body');
    const pageTitle = document.getElementById('pageTitle');

    let currentMode = 'AUTO';
    let serialConnected = false;
    let minRecordedTemp = null;
    let maxRecordedTemp = null;
    let minLimit = 18.0;
    let maxLimit = 28.0;
    const chartData = { labels: [], temp: [], hum: [] };
    const maxChartPoints = 60;

    function updateClock() {
        const now = new Date();
        if (currentDateEl) currentDateEl.textContent = now.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
        if (currentTimeEl) currentTimeEl.textContent = now.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }
    setInterval(updateClock, 1000);
    updateClock();

    function logToTerminal(message, type = 'info') {
        if (!serialConsole) return;
        const line = document.createElement('div');
        line.className = `term-line ${type}`;
        const time = new Date().toLocaleTimeString('es-ES');
        line.textContent = `[${time}] ${message}`;
        serialConsole.appendChild(line);
        while (serialConsole.children.length > 200) serialConsole.removeChild(serialConsole.firstChild);
        serialConsole.scrollTop = serialConsole.scrollHeight;
    }

    navItems.forEach(item => {
        item.addEventListener('click', (e) => {
            e.preventDefault();
            const targetSection = item.getAttribute('data-section');
            navItems.forEach(n => n.classList.remove('active'));
            item.classList.add('active');
            contentSections.forEach(section => {
                section.classList.toggle('hidden', section.id !== `section-${targetSection}`);
            });
            if (pageTitle) pageTitle.textContent = item.querySelector('.nav-label')?.textContent || 'Panel de Monitoreo';
        });
    });

    function setManualControlsEnabled(enabled) {
        const allow = enabled && serialConnected;
        if (btnFanOn) btnFanOn.disabled = !allow;
        if (btnFanOff) btnFanOff.disabled = !allow;
        if (btnLampOn) btnLampOn.disabled = !allow;
        if (btnLampOff) btnLampOff.disabled = !allow;
    }

    function applyModeUI(mode) {
        const isManual = (mode === 'MANUAL' || mode === 'Manual');
        currentMode = isManual ? 'MANUAL' : 'AUTO';
        if (isManual) {
            btnModeManual?.classList.add('active');
            btnModeAuto?.classList.remove('active');
            if (topModeBadge) { topModeBadge.textContent = 'MODO: MANUAL'; topModeBadge.className = 'system-mode-badge mode-manual'; }
            if (lcdMode) lcdMode.textContent = '[MANU]';
            if (manualNotice) manualNotice.style.display = 'none';
            setManualControlsEnabled(true);
        } else {
            btnModeAuto?.classList.add('active');
            btnModeManual?.classList.remove('active');
            if (topModeBadge) { topModeBadge.textContent = 'MODO: AUTOMÁTICO'; topModeBadge.className = 'system-mode-badge mode-auto'; }
            if (lcdMode) lcdMode.textContent = '[AUTO]';
            if (manualNotice) manualNotice.style.display = 'block';
            setManualControlsEnabled(false);
        }
    }

    function updateConnectionUI(status, portName) {
        serialConnected = (status === 'Connected');
        if (serialConnected) {
            pulseIndicator && (pulseIndicator.className = 'status-pulse online');
            if (sidebarComStatus) { sidebarComStatus.textContent = portName ? `CONECTADO (${portName})` : 'CONECTADO'; sidebarComStatus.style.color = '#10b981'; }
            if (connectionPill) connectionPill.className = 'connection-pill connected';
            if (connectionPillText) connectionPillText.textContent = `Arduino: En línea (${portName || 'COM'})`;
            if (linkStatusChip) { linkStatusChip.textContent = 'ENLACE SERIAL ACTIVO'; linkStatusChip.className = 'status-chip normal'; }
            if (csharpPortBadge && portName) csharpPortBadge.textContent = portName;
            logToTerminal(`Enlace serial activo con ${portName || 'COM'}.`, 'success');
        } else {
            pulseIndicator && (pulseIndicator.className = 'status-pulse offline');
            if (sidebarComStatus) { sidebarComStatus.textContent = 'DESCONECTADO'; sidebarComStatus.style.color = '#ef4444'; }
            if (connectionPill) connectionPill.className = 'connection-pill disconnected';
            if (connectionPillText) connectionPillText.textContent = 'Arduino: Desconectado';
            if (linkStatusChip) { linkStatusChip.textContent = (status === 'Error') ? 'ERROR SERIAL' : 'SIN ENLACE SERIAL'; linkStatusChip.className = 'status-chip offline'; }
            if (status && status !== 'Disconnected') logToTerminal(`Estado de conexión serial: ${status}`, status === 'Error' ? 'error' : 'warning');
        }
        applyModeUI(currentMode);
    }

    function updateActuatorUI(kind, isOn) {
        if (kind === 'fan') {
            if (isOn) {
                fanWrapper?.classList.add('spinning');
                if (fanStatusText) { fanStatusText.textContent = 'ACTIVO (ON)'; fanStatusText.style.color = '#38bdf8'; }
                if (fanStateChip) { fanStateChip.textContent = 'ENFRIANDO'; fanStateChip.className = 'status-chip active-fan'; }
                if (lcdFan) lcdFan.textContent = 'ON ';
            } else {
                fanWrapper?.classList.remove('spinning');
                if (fanStatusText) { fanStatusText.textContent = 'APAGADO'; fanStatusText.style.color = '#f1f5f9'; }
                if (fanStateChip) { fanStateChip.textContent = 'STANDBY'; fanStateChip.className = 'status-chip offline'; }
                if (lcdFan) lcdFan.textContent = 'OFF';
            }
        } else if (kind === 'lamp') {
            if (isOn) {
                lampWrapper?.classList.add('glowing');
                if (lampStatusText) { lampStatusText.textContent = 'ACTIVA (ON)'; lampStatusText.style.color = '#fbbf24'; }
                if (lampStateChip) { lampStateChip.textContent = 'CALENTANDO'; lampStateChip.className = 'status-chip active-lamp'; }
                if (lcdLamp) lcdLamp.textContent = 'ON ';
            } else {
                lampWrapper?.classList.remove('glowing');
                if (lampStatusText) { lampStatusText.textContent = 'APAGADA'; lampStatusText.style.color = '#f1f5f9'; }
                if (lampStateChip) { lampStateChip.textContent = 'STANDBY'; lampStateChip.className = 'status-chip offline'; }
                if (lcdLamp) lcdLamp.textContent = 'OFF';
            }
        }
    }

    function updateTelemetryUI(data) {
        if (!data) return;
        const temp = parseFloat(data.temperatureC);
        const hum = parseFloat(data.humidityPct);

        if (!isNaN(temp)) {
            if (kpiTemp) kpiTemp.textContent = temp.toFixed(1);
            if (lcdTemp) lcdTemp.textContent = temp.toFixed(1);
            const pctTemp = Math.min(Math.max(((temp - 10) / 40.0) * 100, 0), 100);
            if (tempBarFill) tempBarFill.style.width = `${pctTemp}%`;
            const nowTime = new Date().toLocaleTimeString('es-ES');
            if (minRecordedTemp === null || temp < minRecordedTemp) { minRecordedTemp = temp; if (minTempDisplay) minTempDisplay.textContent = `${minRecordedTemp.toFixed(1)} °C`; if (minTempTime) minTempTime.textContent = nowTime; }
            if (maxRecordedTemp === null || temp > maxRecordedTemp) { maxRecordedTemp = temp; if (maxTempDisplay) maxTempDisplay.textContent = `${maxRecordedTemp.toFixed(1)} °C`; if (maxTempTime) maxTempTime.textContent = nowTime; }
            pushChartPoint(temp, hum);
        }

        if (!isNaN(hum)) {
            if (kpiHumidity) kpiHumidity.textContent = hum.toFixed(1);
            if (lcdHum) lcdHum.textContent = hum.toFixed(1);
            if (humBarFill) humBarFill.style.width = `${Math.min(Math.max(hum, 0), 100)}%`;
            if (humStatusChip) {
                if (hum < 30) { humStatusChip.textContent = 'BAJA HUMEDAD'; humStatusChip.className = 'status-chip alert'; }
                else if (hum > 70) { humStatusChip.textContent = 'ALTA HUMEDAD'; humStatusChip.className = 'status-chip alert'; }
                else { humStatusChip.textContent = 'RANGO ÓPTIMO'; humStatusChip.className = 'status-chip normal'; }
            }
        }

        const isFanOn = (data.fanStatus === 'On' || data.fanStatus === 1 || data.fanStatus === true || data.fanStatus === 'ON');
        const isLampOn = (data.lampStatus === 'On' || data.lampStatus === 1 || data.lampStatus === true || data.lampStatus === 'ON');
        updateActuatorUI('fan', isFanOn);
        updateActuatorUI('lamp', isLampOn);

        if (typeof data.minThreshold === 'number') minLimit = data.minThreshold;
        if (typeof data.maxThreshold === 'number') maxLimit = data.maxThreshold;
        if (limitsHint) limitsHint.textContent = `FRÍO < ${minLimit.toFixed(1)}°C | NORMAL ${minLimit.toFixed(1)}-${maxLimit.toFixed(1)}°C | CALOR > ${maxLimit.toFixed(1)}°C | ALARMA >= 36.0°C`;

        if (!isNaN(temp) && tempAlertChip) {
            if (temp >= 36.0) { tempAlertChip.textContent = 'ALARMA (>=36�C)'; tempAlertChip.className = 'status-chip alert'; }
            else if (temp > maxLimit) { tempAlertChip.textContent = 'CALOR (>28�C)'; tempAlertChip.className = 'status-chip alert'; }
            else if (temp < minLimit) { tempAlertChip.textContent = 'FR�O (<18�C)'; tempAlertChip.className = 'status-chip alert'; }
            else { tempAlertChip.textContent = 'NORMAL (18�C-28�C)'; tempAlertChip.className = 'status-chip normal'; }
        }

        if (data.mode) applyModeUI(data.mode.toUpperCase().startsWith('MAN') ? 'MANUAL' : 'AUTO');
        if (typeof data.isConnected === 'boolean') updateConnectionUI(data.isConnected ? 'Connected' : (serialConnected ? 'Connected' : 'Disconnected'), csharpPortBadge?.textContent || null);
    }

    function pushChartPoint(temp, hum) {
        if (isNaN(temp)) return;
        const t = new Date().toLocaleTimeString('es-ES', { hour12: false });
        chartData.labels.push(t);
        chartData.temp.push(temp);
        chartData.hum.push(isNaN(hum) ? null : hum);
        if (chartData.labels.length > maxChartPoints) {
            chartData.labels.shift(); chartData.temp.shift(); chartData.hum.shift();
        }
        if (chartEmptyState) chartEmptyState.style.display = 'none';
        drawChart();
    }

    function drawChart() {
        if (!chartCanvas) return;
        const ctx = chartCanvas.getContext('2d');
        if (!ctx) return;
        const w = chartCanvas.clientWidth || chartCanvas.width;
        const h = chartCanvas.height;
        chartCanvas.width = w; chartCanvas.height = h;
        ctx.clearRect(0, 0, w, h);
        if (chartData.temp.length < 2) return;
        const padL = 40, padR = 10, padT = 10, padB = 20;
        const allVals = [...chartData.temp, ...chartData.hum.filter(v => v != null)];
        let minV = Math.min(...allVals, minLimit - 2, 0);
        let maxV = Math.max(...allVals, maxLimit + 2, 40);
        if (maxV - minV < 5) maxV = minV + 5;
        const x = i => padL + (i / (chartData.temp.length - 1)) * (w - padL - padR);
        const y = v => padT + (1 - (v - minV) / (maxV - minV)) * (h - padT - padB);
        // grid
        ctx.strokeStyle = '#334155'; ctx.lineWidth = 1;
        for (let i = 0; i <= 4; i++) { const gy = padT + (i / 4) * (h - padT - padB); ctx.beginPath(); ctx.moveTo(padL, gy); ctx.lineTo(w - padR, gy); ctx.stroke(); }
        // limit lines
        ctx.setLineDash([4,4]);
        ctx.strokeStyle = '#ef4444'; ctx.beginPath(); ctx.moveTo(padL, y(minLimit)); ctx.lineTo(w - padR, y(minLimit)); ctx.stroke();
        ctx.strokeStyle = '#3b82f6'; ctx.beginPath(); ctx.moveTo(padL, y(maxLimit)); ctx.lineTo(w - padR, y(maxLimit)); ctx.stroke();
        ctx.setLineDash([]);
        // temp line
        ctx.strokeStyle = '#22d3ee'; ctx.lineWidth = 2; ctx.beginPath();
        chartData.temp.forEach((v,i)=>{ const px=x(i), py=y(v); if(i===0) ctx.moveTo(px,py); else ctx.lineTo(px,py); }); ctx.stroke();
        // hum line
        ctx.strokeStyle = '#a78bfa'; ctx.lineWidth = 2; ctx.beginPath();
        let started=false; chartData.hum.forEach((v,i)=>{ if(v==null) return; const px=x(i), py=y(v); if(!started){ctx.moveTo(px,py); started=true;} else ctx.lineTo(px,py); }); ctx.stroke();
        ctx.fillStyle = '#94a3b8'; ctx.font = '11px sans-serif';
        ctx.fillText(maxV.toFixed(0), 4, padT+8); ctx.fillText(minV.toFixed(0), 4, h-padB);
    }

    // SignalR
    let connection = null;
    if (typeof signalR !== 'undefined') {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/telemetry')
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        connection.on('ReceiveTelemetry', (telemetry) => updateTelemetryUI(telemetry));
        connection.on('ReceiveConnectionStatus', (status, portName) => updateConnectionUI(status, portName));
        connection.on('ReceiveAlert', (message, severity) => logToTerminal(`ALERTA (${severity}): ${message}`, (severity === 'Critical' || severity === 'CRITICAL') ? 'error' : 'warning'));
        connection.on('ReceiveCommandAck', (cmdType, success, msg) => {
            logToTerminal(`ACK [${cmdType}]: ${msg}`, success ? 'success' : 'warning');
            if (!success) return;
            const t = (cmdType || '').toUpperCase();
            if (t === 'SET_MODE') {
                // el parámetro puede venir en msg; el modo definitivo llega por telemetría, pero damos feedback
            } else if (t === 'FAN_ON') {
                updateActuatorUI('fan', true);
            } else if (t === 'FAN_OFF') {
                updateActuatorUI('fan', false);
            } else if (t === 'LAMP_ON') {
                updateActuatorUI('lamp', true);
            } else if (t === 'LAMP_OFF') {
                updateActuatorUI('lamp', false);
            }
        });

        connection.onreconnecting(() => logToTerminal('Reconectando con el servidor SignalR...', 'warning'));
        connection.onreconnected(() => logToTerminal('Reconexión con SignalR reestablecida.', 'success'));
        connection.onclose(() => logToTerminal('Conexión SignalR cerrada.', 'warning'));

        connection.start()
            .then(() => logToTerminal('Conectado al Hub SignalR (/hubs/telemetry).', 'success'))
            .catch(err => logToTerminal(`Error al conectar con SignalR Hub: ${err.message}`, 'error'));
    } else {
        logToTerminal('SignalR no disponible en el cliente (CDN no cargado).', 'error');
    }

    async function sendCommand(commandType, parameter) {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            logToTerminal(`No se puede enviar [${commandType}]: SignalR no conectado.`, 'error');
            return;
        }
        try {
            await connection.invoke('SendControlCommand', { commandType, parameter: parameter == null ? null : String(parameter) });
        } catch (err) {
            logToTerminal(`Fallo al emitir comando [${commandType}]: ${err.message}`, 'error');
        }
    }

    btnModeAuto?.addEventListener('click', () => { applyModeUI('AUTO'); logToTerminal('Solicitando modo AUTOMÁTICO.', 'info'); sendCommand('SET_MODE', 'AUTO'); });
    btnModeManual?.addEventListener('click', () => { applyModeUI('MANUAL'); logToTerminal('Solicitando modo MANUAL.', 'warning'); sendCommand('SET_MODE', 'MANUAL'); });

    btnFanOn?.addEventListener('click', () => { logToTerminal('Orden: ENCENDER VENTILADOR (D7).', 'info'); sendCommand('FAN_ON', '1'); });
    btnFanOff?.addEventListener('click', () => { logToTerminal('Orden: APAGAR VENTILADOR (D7).', 'info'); sendCommand('FAN_OFF', '0'); });
    btnLampOn?.addEventListener('click', () => { logToTerminal('Orden: ENCENDER LÁMPARA (D8).', 'info'); sendCommand('LAMP_ON', '1'); });
    btnLampOff?.addEventListener('click', () => { logToTerminal('Orden: APAGAR LÁMPARA (D8).', 'info'); sendCommand('LAMP_OFF', '0'); });

    btnSaveThresholds?.addEventListener('click', () => {
        const minVal = parseFloat(inputMinThreshold?.value);
        const maxVal = parseFloat(inputMaxThreshold?.value);
        if (isNaN(minVal) || isNaN(maxVal) || minVal >= maxVal) {
            alert('Umbrales inválidos: el mínimo debe ser menor que el máximo.');
            return;
        }
        minLimit = minVal; maxLimit = maxVal;
        if (limitsHint) limitsHint.textContent = `FRÍO < ${minLimit.toFixed(1)}°C | NORMAL ${minLimit.toFixed(1)}-${maxLimit.toFixed(1)}°C | CALOR > ${maxLimit.toFixed(1)}°C | ALARMA >= 36.0°C`;
        logToTerminal(`Aplicando límites: Lámpara ON <${minVal}°C | Ventilador ON >${maxVal}°C.`, 'info');
        sendCommand('SET_LIMITS', `${minVal},${maxVal}`);
    });

    btnResetMinMax?.addEventListener('click', () => {
        minRecordedTemp = null; maxRecordedTemp = null;
        if (minTempDisplay) minTempDisplay.textContent = '--.- °C';
        if (maxTempDisplay) maxTempDisplay.textContent = '--.- °C';
        if (minTempTime) minTempTime.textContent = '--:--:--';
        if (maxTempTime) maxTempTime.textContent = '--:--:--';
        logToTerminal('Mín/Máx de sesión reiniciados.', 'info');
    });

    async function refreshPorts(selectFirst = false) {
        try {
            const res = await fetch('/api/ports');
            const data = await res.json();
            const ports = data.availablePorts || [];
            if (serialPortSelect) {
                const prev = serialPortSelect.value;
                serialPortSelect.innerHTML = '';
                if (ports.length === 0) {
                    const opt = document.createElement('option'); opt.value = ''; opt.textContent = 'Sin puertos'; serialPortSelect.appendChild(opt);
                } else {
                    ports.forEach(p => { const opt = document.createElement('option'); opt.value = p; opt.textContent = p; serialPortSelect.appendChild(opt); });
                    if (prev && ports.includes(prev)) serialPortSelect.value = prev;
                    else if (selectFirst) serialPortSelect.value = ports[0];
                }
            }
            if (ports.length > 0) logToTerminal(`Puertos COM detectados: ${ports.join(', ')}`, 'info');
            else logToTerminal('No se detectaron puertos COM activos.', 'warning');
            return ports;
        } catch (e) {
            logToTerminal(`Error al consultar puertos: ${e.message}`, 'error');
            return [];
        }
    }

    btnCheckPorts?.addEventListener('click', () => refreshPorts(false));

    btnConnectSerial?.addEventListener('click', async () => {
        const port = serialPortSelect?.value || 'COM1';
        if (!port) { logToTerminal('Seleccione un puerto COM válido.', 'warning'); return; }
        logToTerminal(`Solicitando conexión serial a ${port} (9600 8-N-1)...`, 'info');
        try {
            const res = await fetch('/api/serial/connect', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ port, baudRate: 9600 }) });
            const data = await res.json();
            if (data.success) {
                if (csharpPortBadge) csharpPortBadge.textContent = data.port || port;
                updateConnectionUI(data.state || 'Connected', data.port || port);
            } else {
                logToTerminal(`No se pudo conectar a ${port}. Estado: ${data.state || 'Error'}`, 'error');
                updateConnectionUI(data.state || 'Error', data.port || port);
            }
        } catch (e) {
            logToTerminal(`Error al conectar: ${e.message}`, 'error');
        }
    });

    btnDisconnectSerial?.addEventListener('click', async () => {
        try {
            const res = await fetch('/api/serial/disconnect', { method: 'POST' });
            const data = await res.json();
            updateConnectionUI(data.state || 'Disconnected', null);
            logToTerminal('Puerto serial desconectado.', 'info');
        } catch (e) {
            logToTerminal(`Error al desconectar: ${e.message}`, 'error');
        }
    });

    btnSimulateTelemetry?.addEventListener('click', async () => {
        const temp = parseFloat(simTempInput?.value);
        const hum = parseFloat(simHumInput?.value);
        if (isNaN(temp)) {
            logToTerminal('Simulación: ingrese una temperatura válida.', 'warning');
            return;
        }
        const humidity = isNaN(hum) ? 50.0 : hum;
        try {
            const res = await fetch('/api/simulation/telemetry', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ temperatureC: temp, humidityPct: humidity })
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok || !data.success) {
                logToTerminal(`Simulación no aplicada: ${data.message || res.status}`, 'error');
                return;
            }
            logToTerminal(`Simulación (navegador, no lectura DHT22): ${temp.toFixed(1)}°C / ${humidity.toFixed(1)}% HR aplicada.`, 'success');
            // La actualización visual llega por SignalR (ReceiveTelemetry); por robustez aplicamos si viene en respuesta
            if (data.telemetry) updateTelemetryUI(data.telemetry);
        } catch (e) {
            logToTerminal(`Error al enviar simulación: ${e.message}`, 'error');
        }
    });

    async function loadInitialState() {
        try {
            const cfg = await (await fetch('/api/config')).json();
            if (cfg) {
                if (typeof cfg.MinTemp === 'number') { minLimit = cfg.MinTemp; if (inputMinThreshold) inputMinThreshold.value = cfg.MinTemp; }
                if (typeof cfg.MaxTemp === 'number') { maxLimit = cfg.MaxTemp; if (inputMaxThreshold) inputMaxThreshold.value = cfg.MaxTemp; }
                if (limitsHint) limitsHint.textContent = `FRÍO < ${minLimit.toFixed(1)}°C | NORMAL ${minLimit.toFixed(1)}-${maxLimit.toFixed(1)}°C | CALOR > ${maxLimit.toFixed(1)}°C | ALARMA >= 36.0°C`;
                if (cfg.Port && csharpPortBadge) csharpPortBadge.textContent = cfg.Port;
            }
        } catch { }
        try {
            const st = await (await fetch('/api/status')).json();
            if (st) {
                updateConnectionUI(st.serialState || st.SerialState || 'Disconnected', st.currentPort || st.CurrentPort || null);
                if (st.mode || st.Mode) applyModeUI(String(st.mode || st.Mode).toUpperCase().startsWith('MAN') ? 'MANUAL' : 'AUTO');
            }
        } catch { }
        await refreshPorts(true);
    }

    applyModeUI('AUTO');
    loadInitialState();
});

