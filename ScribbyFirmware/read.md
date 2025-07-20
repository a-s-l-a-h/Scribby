##Scribby Control Command Documentation

This document details the final, highly intuitive command set for your robot.

---

## 1. Robot Configuration

All tuning is done in the **ROBOT CONFIGURATION** section at the top of the Arduino code.  
This includes hardware mapping and performance settings like `PRIMITIVE_MOVEMENT_SPEED`.

---

## 2. Control Modes Explained

### Primitive Mode

- **Commands**: `w`, `a`, `s`, `d`, `x`  
- **Speed**: Uses the single `PRIMITIVE_MOVEMENT_SPEED`  
- **Behavior**: Simple, direct commands for basic driving

### Advanced Mode

- **Commands**: All commands containing a hyphen (`-`)  
- **Speed**: Uses the speed value you provide in the command  
- **Behavior**: Absolute control over individual motors

---

## 3. Command Reference

### 3.1: Primitive Commands

Simple, direct commands that use the `PRIMITIVE_MOVEMENT_SPEED`.  
They do **not** use a kickstart.

| Command | Action              |
|---------|---------------------|
| `w`     | Move Forward        |
| `x`     | Move Backward       |
| `d`     | Turn Right          |
| `a`     | Turn Left           |
| `s`     | Stop (`scribbystop`) |

---

### 3.2: Advanced "Go" Commands

These are the only advanced commands. They provide absolute control over motor speed and direction.

**Command Structure:**  
`<action>[k]-<speed>`

- `<action>`: A word describing the command (e.g., `fgfl`, `bgbr`, `fgall`)
- `[k]`: *(Optional)* Add a `k` to the end of the action to force a **kickstart**
- `<speed>`: A number from your configured `MINIMUM_MOTOR_SPEED` to `MAXIMUM_MOTOR_SPEED`

> 💡 These commands always set both speed and direction, overriding the motor’s previous state.

#### Advanced Action Commands

| Action   | Breakdown               | Target         | Example        |
|----------|-------------------------|----------------|----------------|
| `fgfl`   | Fwd Go Front Left       | Front Left     | `fgfl-100`     |
| `bgfl`   | Bwd Go Front Left       | Front Left     | `bgfl-100`     |
| `fgfr`   | Fwd Go Front Right      | Front Right    | `fgfr-150`     |
| `bgfr`   | Bwd Go Front Right      | Front Right    | `bgfr-150`     |
| `fgbl`   | Fwd Go Back Left        | Back Left      | `fgbl-75`      |
| `bgbl`   | Bwd Go Back Left        | Back Left      | `bgbl-75`      |
| `fgbr`   | Fwd Go Back Right       | Back Right     | `fgbr-200`     |
| `bgbr`   | Bwd Go Back Right       | Back Right     | `bgbr-200`     |
| `fgall`  | Fwd Go All Motors       | All Motors     | `fgall-120`    |
| `bgall`  | Bwd Go All Motors       | All Motors     | `bgall-120`    |
| `...k`   | Any of the above + `k`  | Respective     | `fgflk-100`    |

---

### Examples

- `fgfl-100` → Move front left motor forward at speed 100  
- `bgbrk-150` → Kickstart and move back right motor backward at speed 150  
- `fgall-120` → Move all motors forward at speed 120

---

## Notes

- All advanced commands **require** a hyphen (`-`) and a speed.
- The optional `k` enables kickstart behavior — use it when motors need a push to overcome inertia.
- Primitive commands are ideal for quick testing or simple movements.
