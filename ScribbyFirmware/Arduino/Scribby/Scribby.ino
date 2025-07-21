#include <AFMotor.h>
#include <SoftwareSerial.h>

// =================== ROBOT CONFIGURATION ===================
// --- 1. Hardware Mapping ---
const int FRONT_LEFT_MOTOR_PORT  = 1;
const int BACK_LEFT_MOTOR_PORT   = 2;
const int BACK_RIGHT_MOTOR_PORT  = 3;
const int FRONT_RIGHT_MOTOR_PORT = 4;
const int BLUETOOTH_RX_PIN = 9;
const int BLUETOOTH_TX_PIN = 10;

// --- 2. Performance Tuning ---
const int MINIMUM_MOTOR_SPEED  = 0;
const int MAXIMUM_MOTOR_SPEED  = 255;
const unsigned long KICKSTART_DURATION_MS = 200; // Renamed for clarity

// --- 3. Primitive Movement Speed ---
const int PRIMITIVE_MOVEMENT_SPEED = 255;
// ===========================================================


// --- System Internals ---
SoftwareSerial BLESerial(BLUETOOTH_RX_PIN, BLUETOOTH_TX_PIN);

const byte COMMAND_BUFFER_SIZE = 16;
char command_buffer[COMMAND_BUFFER_SIZE];
byte command_pos = 0;
bool command_ready = false;

// --- Non-Blocking Motor Control Class ---
// This class encapsulates the state for a single motor, including its kickstart timer.
class MotorManager {
public:
    MotorManager(AF_DCMotor& motor_ref) : motor(motor_ref), targetSpeed(0), kickstartEndTime(0) {}

    // The update() function MUST be called in every loop.
    // It checks if a kickstart period is over and reduces speed if needed.
    void update() {
        if (kickstartEndTime != 0 && millis() >= kickstartEndTime) {
            motor.setSpeed(targetSpeed);
            kickstartEndTime = 0; // Deactivate kickstart timer
        }
    }

    void set(int newSpeed, uint8_t newDirection, bool kickstart) {
        targetSpeed = constrain(newSpeed, 0, MAXIMUM_MOTOR_SPEED);
        
        motor.run(newDirection);

        if (kickstart && targetSpeed > 0) {
            motor.setSpeed(MAXIMUM_MOTOR_SPEED);
            // Set a timer for the future instead of using delay()
            kickstartEndTime = millis() + KICKSTART_DURATION_MS;
        } else {
            motor.setSpeed(targetSpeed);
            kickstartEndTime = 0; // Ensure no kickstart is active
        }
    }

    void release() {
        motor.run(RELEASE);
        targetSpeed = 0;
        kickstartEndTime = 0; // Cancel any ongoing kickstart
    }

private:
    AF_DCMotor& motor;
    int targetSpeed;
    unsigned long kickstartEndTime;
};

// Create motor objects and their corresponding managers
AF_DCMotor motor_fl_raw(FRONT_LEFT_MOTOR_PORT);
AF_DCMotor motor_fr_raw(FRONT_RIGHT_MOTOR_PORT);
AF_DCMotor motor_bl_raw(BACK_LEFT_MOTOR_PORT);
AF_DCMotor motor_br_raw(BACK_RIGHT_MOTOR_PORT);

MotorManager motor_front_left(motor_fl_raw);
MotorManager motor_front_right(motor_fr_raw);
MotorManager motor_back_left(motor_bl_raw);
MotorManager motor_back_right(motor_br_raw);
// --- End System Internals ---


void setup() {
  BLESerial.begin(9600);
  // No need to set initial speed here, the manager handles it.
}

void loop() {
  // 1. Check for and read any incoming commands (non-blocking).
  readSerialCommand();

  // 2. If a full command is ready, process it immediately.
  if (command_ready) {
    processCommand();
    command_pos = 0; // Reset for next command
    command_ready = false;
  }

  // 3. IMPORTANT: Update all motor states on every single loop.
  // This is the non-blocking magic. It checks timers and adjusts speeds.
  motor_front_left.update();
  motor_front_right.update();
  motor_back_left.update();
  motor_back_right.update();
}

void readSerialCommand() {
  while (BLESerial.available() > 0 && !command_ready) {
    char inChar = (char)BLESerial.read();
    if (inChar == '\n') {
      command_buffer[command_pos] = '\0';
      command_ready = true;
    } else {
      if (command_pos < COMMAND_BUFFER_SIZE - 1) {
        command_buffer[command_pos++] = inChar;
      }
    }
  }
}

void processCommand() {
  if (strchr(command_buffer, '-') != NULL) {
    handleAdvancedCommand(command_buffer);
  } else {
    handlePrimitiveCommand(command_buffer);
  }
}

void handlePrimitiveCommand(char* cmd) {
  if (strcmp(cmd, "w") == 0) go_forward(PRIMITIVE_MOVEMENT_SPEED, false);
  else if (strcmp(cmd, "a") == 0) go_left();
  else if (strcmp(cmd, "d") == 0) go_right();
  else if (strcmp(cmd, "s") == 0) stop_all();
  else if (strcmp(cmd, "x") == 0) go_back(PRIMITIVE_MOVEMENT_SPEED, false);
}

void handleAdvancedCommand(char* cmd) {
  char* hyphen = strchr(cmd, '-');
  if (hyphen == NULL) return;

  int speed = atoi(hyphen + 1);
  *hyphen = '\0'; // Split the string at the hyphen

  bool forceKickstart = false;
  int prefixLen = strlen(cmd);
  if (prefixLen > 0 && cmd[prefixLen - 1] == 'k') {
    forceKickstart = true;
    cmd[prefixLen - 1] = '\0';
  }

  uint8_t direction = FORWARD;
  if (cmd[0] == 'b') {
    direction = BACKWARD;
  }
  
  char* target = cmd + 1;

  if (strcmp(target, "gfl") == 0) motor_front_left.set(speed, direction, forceKickstart);
  else if (strcmp(target, "gfr") == 0) motor_front_right.set(speed, direction, forceKickstart);
  else if (strcmp(target, "gbl") == 0) motor_back_left.set(speed, direction, forceKickstart);
  else if (strcmp(target, "gbr") == 0) motor_back_right.set(speed, direction, forceKickstart);
  else if (strcmp(target, "gall") == 0) {
    if (direction == FORWARD) go_forward(speed, forceKickstart); else go_back(speed, forceKickstart);
  }
}

// --- Movement Functions ---
void go_forward(int speed, bool kick) {
  motor_front_left.set(speed, FORWARD, kick);
  motor_front_right.set(speed, FORWARD, kick);
  motor_back_left.set(speed, FORWARD, kick);
  motor_back_right.set(speed, FORWARD, kick);
}

void go_back(int speed, bool kick) {
  motor_front_left.set(speed, BACKWARD, kick);
  motor_front_right.set(speed, BACKWARD, kick);
  motor_back_left.set(speed, BACKWARD, kick);
  motor_back_right.set(speed, BACKWARD, kick);
}

void go_left() {
  motor_front_left.set(PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  motor_back_left.set(PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  motor_front_right.set(PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  motor_back_right.set(PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
}

void go_right() {
  motor_front_left.set(PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  motor_back_left.set(PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  motor_front_right.set(PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  motor_back_right.set(PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
}

void stop_all() {
  motor_front_left.release();
  motor_front_right.release();
  motor_back_left.release();
  motor_back_right.release();
}
