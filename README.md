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
