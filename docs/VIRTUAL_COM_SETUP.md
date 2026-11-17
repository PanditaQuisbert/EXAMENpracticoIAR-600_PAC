# Guía de Configuración de Puertos COM Virtuales

## ¿Por qué se necesitan Puertos COM Virtuales?

En una máquina real:
- El software en C# se conecta a un puerto físico (ej. `COM3` por USB).
- El microcontrolador físico está conectado físicamente a dicho puerto.

Sin embargo, en un entorno de simulación donde tanto el software SCADA (C#) como el circuito simulado (Proteus 8.17) se ejecutan en la **misma computadora**, Windows no permite que dos programas abran el mismo puerto COM de forma simultánea.

Para resolver esto, se utiliza un **emulador de cable módem nulo virtual** (como **com0com** o **VSPE - Virtual Serial Ports Emulator**), el cual crea una pareja de puertos COM virtuales interconectados punto a punto:

```
[ Software C# ASP.NET Core ] ⇄ (COM1) ======= CABLE VIRTUAL ======= (COM2) ⇄ [ Proteus COMPIM (Arduino UNO) ]
```

Todo lo que el software C# transmite por `COM1` aparece instantáneamente en `COM2` (donde escucha el COMPIM de Proteus). Y todo lo que el Arduino transmite por el COMPIM hacia `COM2` llega directamente a `COM1` para ser leído por C#.

---

## Opción Recomendada: com0com (Null-modem Emulator)

**com0com** es un controlador de código abierto para Windows firmado y ampliamente utilizado en ingeniería:

### Pasos de Instalación y Configuración:
1. Descargue el instalador firmado de `com0com` (versión x64 para Windows).
2. Durante la instalación, permita la instalación del controlador virtual.
3. Abra la utilidad **Setup Command Prompt** o la interfaz gráfica **Setup** de com0com (ejecutar como Administrador).
4. Cree una pareja de puertos virtuales:
   - Primer puerto: renombrar a `COM1` (o marcar *enable buffer overrun*).
   - Segundo puerto: renombrar a `COM2`.
5. Haga clic en **Apply**.
6. Abra el Administrador de Dispositivos de Windows (`devmgmt.msc`) y verifique que en la sección **Puertos (COM y LPT)** aparezcan listados los nuevos puertos.

---

## Opción Alternativa: VSPE (Virtual Serial Ports Emulator)

Si prefiere una herramienta con asistente visual de un solo clic:
1. Instale **VSPE** (32-bit o 64-bit).
2. Vaya a *Device* -> *Create*.
3. Seleccione el tipo de dispositivo: **Pair** (Pareja).
4. Asigne:
   - **Virtual port 1:** `COM1`
   - **Virtual port 2:** `COM2`
5. Haga clic en *Finish* y luego en el botón de reproducción (*Start Emulation*).

---

## Prueba Rápida de Verificación de Puertos

Para verificar que la pareja funciona antes de encender Proteus o el software:
1. Puede abrir dos instancias de un terminal serial (como PuTTY o Tera Term).
2. Conecte una terminal a `COM1` (9600 baudios) y otra a `COM2` (9600 baudios).
3. Escriba texto en una ventana y observe cómo se replica en la otra en tiempo real.
