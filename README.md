# Scribby: The Scriptable Robot Control



**Scribby** is an innovative robotics project that combines a versatile Arduino-powered bot with a modern, cross-platform control application built with .NET MAUI.

---





## 🎥 Demos

<table align="center">
  <tr>
    <td align="center">
      <h3>💻 Windows Demo</h3>
      <video src="https://github.com/user-attachments/assets/63bf4ca0-120c-4bbb-9e21-a3d4fbfc2d2e"
             controls
             muted
             loop
             style="max-width:100%; border-radius: 8px;">
        Your browser does not support the video tag.
      </video>
    </td>
  </tr>
  <tr>
    <td align="center">
      <h3>📱 Android Demo</h3>
      <video src="https://github.com/user-attachments/assets/f65cb6ee-6162-45e9-9662-74593921ce70"
             controls
             muted
             loop
             style="max-width:100%; border-radius: 8px;">
        Your browser does not support the video tag.
      </video>
    </td>
  </tr>
</table>


---

## 💡 Philosophy

Our core philosophy is built on three pillars: accessibility, customization, and control.

*   **📱 Cross-Platform Control:** The `ScribbyApp` is built with .NET MAUI, allowing you to connect to and control your robot from a single codebase on **Android, iOS, and Windows**.

*   **📜 Limitless Scripting:** Orchestrate complex robotic behaviors. The app features a powerful scripting engine that uses **HTML and JavaScript** to go beyond simple remote controls. Automate sequences of movements, create dynamic responses to user input, or design sophisticated control interfaces. Your web development skills are the key to unlocking Scribby's full potential.

*   **⚡ Real-Time Responsiveness:** The communication protocol is designed for immediate action. Send commands from your custom JavaScript interface directly to the bot. The application includes critical safety features like an **emergency stop and resume** button that gives you application-level control to instantly pause and un-pause the robot's operation, overriding any running script.

---

## 🏗️ How to Build

The project is divided into two main parts: `ScribbyApp` (the controller) and `ScribbyFirmware` (the robot's brain).

###  Building the ScribbyBot (Firmware)

The ScribbyBot is a 4-wheel drive robot powered by an Arduino and a motor shield. This project exclusively supports **Bluetooth Low Energy (BLE)** for communication.

#### **Hardware Required**
*   **Microcontroller:** An Arduino Uno board.
*   **Motor Shield:** An L293D-based motor driver shield for Arduino.
    *   *Features*: Must be capable of controlling four DC motors independently. It should support a motor voltage range of 4.5V to 25V and provide at least 0.6A of continuous current per motor.
*   **Chassis:** A 4-wheel drive (4WD) robot chassis platform.
*   **Motors:** Four plastic, dual-shaft DC gear motors.
    *   *Features*: Should operate within a 3V to 9V range, suitable for toy car applications.
*   **Bluetooth Module:** A BLE 4.0 module based on the CC2540 or CC2541 chipset (often sold as HM-10 compatible). **Classic Bluetooth modules like the HC-05/06 are not compatible.**
*   **Power Source:**
    *   Two 3.7V rechargeable batteries (e.g., Li-Ion 18650).
    *   A battery holder for the two cells.
*   **Cables:** A standard Arduino USB cable (Type A to Type B) for uploading the firmware.

#### **Assembly & Wiring**
1.  **Mount Components:** Assemble the 4WD robot chassis, attaching the four motors and wheels. Find a secure place on the chassis to mount the Arduino and the battery holder.
2.  **Attach Motor Shield:** Firmly stack the motor shield on top of the Arduino Uno, ensuring all pins are correctly seated.
3.  **Connect Motors, Power, and BLE:** Wire the components to the motor shield as shown in the diagram below.
    *   Connect the four motors to the `M1`, `M2`, `M3`, and `M4` terminals.
    *   Connect the battery pack to the external power input (`EXT_PWR`).
    *   Connect the BLE module to the 5V, GND, and Digital Pins 9 and 10.

    <img width="1166" height="700" alt="circuit" src="https://github.com/user-attachments/assets/12dbc6e8-38ba-46c6-af9c-de1b251a9571" />

4.  **Detailed BLE Wiring:**
    *   **BT VCC Pin** → **5V** on the shield
    *   **BT GND Pin** → **GND** on the shield
    *   **BT TXD Pin** → **Pin 10** on the shield (marked for SERVO 1)
    *   **BT RXD Pin** → **Pin 9** on the shield (marked for SERVO 2)
    > **Why these pins?** These pins are chosen for convenience, as they correspond to the **SERVO 1** and **SERVO 2** headers on the motor shield. This provides a neat 3-pin block with 5V, GND, and signal pins all in one place. The firmware uses `SoftwareSerial` on these pins to create a dedicated communication channel for the BLE module, leaving the main USB port (pins 0 and 1) free so you can upload code without ever needing to disconnect the module.

#### **Flashing the Firmware**
1.  **Install Arduino IDE:** Download and install the latest version of the [Arduino IDE](https://www.arduino.cc/en/software) from the official website.
2.  **Install Required Libraries:**
    *   Open the Arduino IDE.
    *   Go to `Sketch` -> `Include Library` -> `Manage Libraries...`.
    *   In the search box, type **"Adafruit Motor Shield library"** and install the V1 library by Adafruit. (This library is also compatible with most V2 and clone shields).
    *   The `SoftwareSerial` library is built-in and does not require installation.
3.  **Upload the Sketch:**
    *   Connect the Arduino Uno to your computer with the USB cable.
    *   Open the `ScribbyFirmware/Arduino/Scribby/Scribby.ino` sketch in the Arduino IDE.
    *   Go to `Tools` -> `Board` and select "Arduino Uno".
    *   Go to `Tools` -> `Port` and select the correct COM port for your Arduino.
    *   Click the "Upload" button (the right-arrow icon) to flash the firmware.

### 📱 Building the ScribbyApp (.NET MAUI)

The control application is a .NET MAUI project.

**Prerequisites:**
*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   [Visual Studio 2022](https://visualstudio.microsoft.com/) with the ".NET Multi-platform App UI development" workload installed.

**Instructions:**
1.  **Open the Solution:**
    *   Launch Visual Studio and open the `ScribbyApp/ScribbyApp.sln` solution file.
2.  **Restore Dependencies:**
    *   Visual Studio should automatically restore the required NuGet packages. If not, right-click the solution in the Solution Explorer and choose "Restore NuGet Packages."
3.  **Build and Run:**
    *   Select your target platform (e.g., a connected Android device, the Windows Machine, or an iOS simulator).
    *   Press the "Start" button to build and deploy the application.

---
## 📁 Project Structure

<details>
<summary>Click to view the directory tree</summary>

```Scribby
├── Examples/
│   ├── Particles.html
│   ├── ProController.html
│   └── clickmovement.html
├── README.md
├── ScribbyApp/
│   ├── Models/
│   ├── Platforms/
│   ├── Properties/
│   ├── Resources/
│   ├── Services/
│   ├── ViewModels/
│   └── Views/
└── ScribbyFirmware/
    ├── Arduino/
    │   └── Scribby/
    │       └── Scribby.ino
    └── README.md
```
</details>

---

# ScribbyApp Scripting Guide

Welcome to the most powerful feature of the ScribbyApp: **Scripts**. This feature allows you to create fully custom remote control interfaces for Scribby using standard web technologies: **HTML and JavaScript**.

---



## 📜 How to Use the Scripts Page

### Creating a New Script:

1. Navigate to the **"Scripts"** tab.
2. Tap the **"Add New Script"** button at the bottom.
3. Give your script a unique name.

### Editing & Running a Script:

- From the script list, tap **Edit** to open the code editor.
- Tap **Run** to launch your custom interface and control Scribby.

---

## 🔗 The JavaScript to C# Bridge

To send any command from your HTML page to the robot, you must use a special JavaScript function that communicates with the app.

### JavaScript:

    // This function is the ONLY way to send a command to Scribby.
    function sendToScribby(command) {
        // It works by navigating to a custom URL scheme that the app listens for.
        window.location.href = `scribby://send?command=${command}`;
    }

---

## 📘 Scribby Command Reference

You can send **two types** of commands:

### 1. Primitive Commands

Simple, single-character commands for basic movement.

    | Command | Action          |
    |---------|-----------------|
    | s       | Stop All Motors |
    | w       | Move Forward    |
    | x       | Move Backward   |
    | a       | Turn Left       |
    | d       | Turn Right      |

### 2. Advanced Commands

Give you precise control over motors, speed, and kickstart.

    Structure: <action>[k]-<speed>

    <action>: Mnemonic like:
      - fgfl = Forward Go Front Left

    [k]: Optional 'k' for a Kickstart (short power burst)

    <speed>: Range from 0 to 255

#### Example:

    fgflk-100

Means:

    "Forward Go Front Left motor with Kickstart at Speed 100"

---

## 🕹️ Example 1: Simple D-Pad (Primitive Commands)

    <!DOCTYPE html>
    <html>
    <head>
        <title>Simple D-Pad</title>
        <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
        <style>
            body { font-family: sans-serif; text-align: center; }
            .d-pad {
                display: grid;
                grid-template-columns: 1fr 1fr 1fr;
                gap: 10px;
                width: 240px;
                margin: 20px auto;
            }
            .button {
                padding: 20px;
                border: 2px solid #555;
                border-radius: 12px;
                font-size: 24px;
                font-weight: bold;
                cursor: pointer;
                user-select: none;
            }
            .forward { grid-column: 2; grid-row: 1; }
            .left    { grid-column: 1; grid-row: 2; }
            .stop    { grid-column: 2; grid-row: 2; background-color: #ff8a80; }
            .right   { grid-column: 3; grid-row: 2; }
            .backward{ grid-column: 2; grid-row: 3; }
        </style>
    </head>
    <body>
        <h1>Simple D-Pad</h1>
        <p>Press and hold to move. Release to stop.</p>
        <div class="d-pad">
            <div class="button forward" onpointerdown="sendToScribby('w')" onpointerup="sendToScribby('s')">W</div>
            <div class="button left"    onpointerdown="sendToScribby('a')" onpointerup="sendToScribby('s')">A</div>
            <div class="button stop"    onclick="sendToScribby('s')">S</div>
            <div class="button right"   onpointerdown="sendToScribby('d')" onpointerup="sendToScribby('s')">D</div>
            <div class="button backward"onpointerdown="sendToScribby('x')" onpointerup="sendToScribby('s')">X</div>
        </div>
        <script>
            function sendToScribby(command) {
                window.location.href = `scribby://send?command=${command}`;
            }
        </script>
    </body>
    </html>

---

## 🎮 Example 2: Pro Controller (Advanced Commands)

This version includes:

- Adjustable speed per direction
- Full kickstart support
- Uses `fgflk-<speed>` format

---
```
<style>
        body {
            margin: 0;
            padding: 0;
            overflow: hidden;
            user-select: none;
            -webkit-user-select: none;
            -moz-user-select: none;
            -ms-user-select: none;
        }
        table {
            width: 100vw;
            height: 100vh;
            border-collapse: collapse;
        }
        td {
            width: 33.33%;
            height: 33.33%;
            text-align: center;
            vertical-align: middle;
        }
        button {
            width: 80%;
            height: 80%;
            font-size: 2em;
            user-select: none;
            -webkit-user-select: none;
        }
    </style><!DOCTYPE html>
<html>
<head>
    <title>Scribby Controller</title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">

    
    <script>
        function sendToScribby(command) {
            window.location.href = `scribby://send?command=${command}`;
        }

        function moveForward()  { sendToScribby('fgallk-255'); }
        function moveBackward() { sendToScribby('bgallk-255'); }
        function stop()         { sendToScribby('s'); }

        function turnLeft() {
            sendToScribby('bgflk-255');
            sendToScribby('bgblk-255');
            sendToScribby('fgfrk-255');
            sendToScribby('fgbrk-255');
        }

        function turnRight() {
            sendToScribby('fgflk-255');
            sendToScribby('fgblk-255');
            sendToScribby('bgfrk-255');
            sendToScribby('bgbrk-255');
        }
    </script>
</head>
<body>
    <table>
        <tr>
            <td></td>
            <td><button onpointerdown="moveForward()" onpointerup="stop()">W</button></td>
            <td></td>
        </tr>
        <tr>
            <td><button onpointerdown="turnLeft()" onpointerup="stop()">A</button></td>
            <td><button onclick="stop()">S</button></td>
            <td><button onpointerdown="turnRight()" onpointerup="stop()">D</button></td>
        </tr>
        <tr>
            <td></td>
            <td><button onpointerdown="moveBackward()" onpointerup="stop()">X</button></td>
            <td></td>
        </tr>
    </table>
</body>
</html>
```
---

## 🔍 Breakdown of `fgflk-100`

    fgfl  = Forward Go Front Left motor
    k     = Kickstart
    100   = Speed (keep speed range is better: 80–255)

Full meaning:

    "Move forward using the front-left motor, with kickstart, at speed 100."
