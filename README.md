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

    <speed>: Range from 80 to 255

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
<!DOCTYPE html>
<html>
<head>
    <title>Scribby Pro Controller</title>
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
    <style>
        :root {
            --bg-color: #36393f;
            --button-bg: #5865f2;
            --button-inactive-bg: #4f545c;
            --stop-button-bg: #ed4245;
            --text-color: #ffffff;
            --font: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
        }

        body {
            font-family: var(--font);
            background-color: var(--bg-color);
            color: var(--text-color);
            text-align: center;
            margin: 0;
            padding: 15px;
            overscroll-behavior: none; /* Prevents pull-to-refresh */
            user-select: none; /* Disables text selection on buttons */
        }

        .container { max-width: 400px; margin: 0 auto; }
        h1 { margin-bottom: 20px; }

        .remote-grid {
            display: grid;
            grid-template-columns: 1fr 1fr 1fr;
            grid-template-rows: 1fr 1fr 1fr;
            gap: 10px;
        }

        /* A flex container for a row of buttons like [-, W, +] */
        .control-group { display: flex; align-items: center; justify-content: center; gap: 12px; }

        /* A stack for the control-group AND its speed display below it */
        .button-stack { display: flex; flex-direction: column; align-items: center; gap: 8px; }

        .main-button {
            width: 75px;
            height: 75px;
            background-color: var(--button-inactive-bg);
            border-radius: 18px;
            display: flex;
            justify-content: center;
            align-items: center;
            font-size: 32px;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.05s ease-out;
        }

        .main-button:active {
            transform: scale(0.95); /* Visual feedback on press */
            background-color: var(--button-bg);
        }
        
        .speed-button {
            width: 45px;
            height: 45px;
            background-color: #2f3136;
            border-radius: 50%;
            display: flex;
            justify-content: center;
            align-items: center;
            font-size: 30px;
            font-weight: bold;
            cursor: pointer;
        }
        
        /* This class makes the +/- buttons disappear when speed is at min/max */
        .speed-button.hidden { visibility: hidden; }

        .speed-display {
            font-size: 14px;
            font-weight: bold;
            font-family: monospace;
            color: #b9bbbe;
            background-color: rgba(0,0,0,0.3);
            padding: 5px 12px;
            border-radius: 8px;
            min-width: 30px;
        }
        
        /* Placing each button stack in its correct grid position */
        #forward-stack  { grid-area: 1 / 2 / 2 / 3; }
        #left-stack     { grid-area: 2 / 1 / 3 / 2; }
        #stop-button    { grid-area: 2 / 2 / 3 / 3; background-color: var(--stop-button-bg); }
        #right-stack    { grid-area: 2 / 3 / 3 / 4; }
        #backward-stack { grid-area: 3 / 2 / 4 / 3; }
    </style>
    
    <script>
        // --- Configuration ---
        const MIN_SPEED = 80;
        const MAX_SPEED = 255;
        const SPEED_ADJUSTMENT = 30; // How much speed changes with each +/- press

        // --- Data Store ---
        // An object to hold the current speed for each movement direction.
        const speeds = {
            forward: MAX_SPEED,
            backward: MAX_SPEED,
            left: MAX_SPEED,
            right: MAX_SPEED
        };

        // --- Core Communication Function ---
        function sendToScribby(command) {
            window.location.href = `scribby://send?command=${command}`;
        }
        
        // --- UI and Data Logic ---
        function adjustSpeed(movement, direction) {
            let currentSpeed = speeds[movement];
            currentSpeed += direction * SPEED_ADJUSTMENT; // direction is -1 or 1
            
            // Clamp the speed to stay within the MIN_SPEED and MAX_SPEED limits.
            speeds[movement] = Math.max(MIN_SPEED, Math.min(MAX_SPEED, currentSpeed));
            
            updateUIForMovement(movement);
        }

        // Updates the speed display number and hides/shows the +/- buttons.
        function updateUIForMovement(movement) {
            const currentSpeed = speeds[movement];
            document.getElementById(`${movement}-speed-display`).innerText = currentSpeed;
            document.getElementById(`${movement}-minus`).classList.toggle('hidden', currentSpeed <= MIN_SPEED);
            document.getElementById(`${movement}-plus`).classList.toggle('hidden', currentSpeed >= MAX_SPEED);
        }
        
        // Called when the page loads to set the initial UI state.
        function initializeUI() {
            for (const movement in speeds) {
                updateUIForMovement(movement);
            }
        }

        // --- Robot Movement Functions ---
        function moveForward()  { sendToScribby(`fgallk-${speeds.forward}`); }
        function moveBackward() { sendToScribby(`bgallk-${speeds.backward}`); }
        function stop()         { sendToScribby('s'); }

        // To turn left on the spot, run left-side motors backward and right-side forward.
        function turnLeft() {
            const speed = speeds.left;
            sendToScribby(`bgflk-${speed}`); // Back-Go Front-Left
            sendToScribby(`bgblk-${speed}`); // Back-Go Back-Left
            sendToScribby(`fgfrk-${speed}`); // Fwd-Go Front-Right
            sendToScribby(`fgbrk-${speed}`); // Fwd-Go Back-Right
        }

        // To turn right, do the opposite. Run left-side motors forward and right-side backward.
        function turnRight() {
            const speed = speeds.right;
            sendToScribby(`fgflk-${speed}`); // Fwd-Go Front-Left
            sendToScribby(`fgblk-${speed}`); // Fwd-Go Back-Left
            sendToScribby(`bgfrk-${speed}`); // Back-Go Front-Right
            sendToScribby(`bgbrk-${speed}`); // Back-Go Back-Right
        }
    </script>
</head>
<body onload="initializeUI()"> <!-- Initialize the UI when the page finishes loading -->
    <div class="container">
        <h1>Pro Controller</h1>
        
        <div class="remote-grid">
            <!-- Forward Button Group -->
            <div id="forward-stack" class="button-stack">
                <div class="control-group">
                    <div class="speed-button" id="forward-minus" onclick="adjustSpeed('forward', -1)">-</div>
                    <div class="main-button" onpointerdown="moveForward()" onpointerup="stop()">W</div>
                    <div class="speed-button" id="forward-plus" onclick="adjustSpeed('forward', 1)">+</div>
                </div>
                <div class="speed-display" id="forward-speed-display"></div>
            </div>

            <!-- Left Button Group -->
            <div id="left-stack" class="button-stack">
                <div class="control-group">
                    <div class="speed-button" id="left-minus" onclick="adjustSpeed('left', -1)">-</div>
                    <div class="main-button" onpointerdown="turnLeft()" onpointerup="stop()">A</div>
                    <div class="speed-button" id="left-plus" onclick="adjustSpeed('left', 1)">+</div>
                </div>
                <div class="speed-display" id="left-speed-display"></div>
            </div>

            <!-- Stop Button -->
            <div id="stop-button" class="main-button" onclick="stop()">S</div>

            <!-- Right Button Group -->
            <div id="right-stack" class="button-stack">
                 <div class="control-group">
                    <div class="speed-button" id="right-minus" onclick="adjustSpeed('right', -1)">-</div>
                    <div class="main-button" onpointerdown="turnRight()" onpointerup="stop()">D</div>
                    <div class="speed-button" id="right-plus" onclick="adjustSpeed('right', 1)">+</div>
                </div>
                <div class="speed-display" id="right-speed-display"></div>
            </div>

            <!-- Backward Button Group -->
            <div id="backward-stack" class="button-stack">
                <div class="control-group">
                    <div class="speed-button" id="backward-minus" onclick="adjustSpeed('backward', -1)">-</div>
                    <div class="main-button" onpointerdown="moveBackward()" onpointerup="stop()">X</div>
                    <div class="speed-button" id="backward-plus" onclick="adjustSpeed('backward', 1)">+</div>
                </div>
                <div class="speed-display" id="backward-speed-display"></div>
            </div>
        </div>
    </div>
</body>
</html>
```
---

## 🔍 Breakdown of `fgflk-100`

    fgfl  = Forward Go Front Left motor
    k     = Kickstart
    100   = Speed (range: 80–255)

Full meaning:

    "Move forward using the front-left motor, with kickstart, at speed 100."
