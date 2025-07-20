#include <AFMotor.h>
#include <SoftwareSerial.h>

// =================== ROBOT CONFIGURATION (V14 - Mnemonic Commands) ===================
// --- 1. Hardware Mapping ---
const int FRONT_LEFT_MOTOR_PORT  = 1;
const int BACK_LEFT_MOTOR_PORT   = 2;
const int BACK_RIGHT_MOTOR_PORT  = 3;
const int FRONT_RIGHT_MOTOR_PORT = 4;
const int BLUETOOTH_RX_PIN = 9;
const int BLUETOOTH_TX_PIN = 10;

// --- 2. Performance Tuning ---
const int MINIMUM_MOTOR_SPEED  = 80;   // Lowest speed for advanced commands
const int MAXIMUM_MOTOR_SPEED  = 255;  // Highest speed for advanced commands
const int KICKSTART_DELAY_MS = 100;

// --- 3. Primitive Movement Speed ---
// This speed is ONLY used for the w, a, s, d, x commands.
const int PRIMITIVE_MOVEMENT_SPEED = 255;
// =================================================================


// --- System Internals ---
SoftwareSerial BLESerial(BLUETOOTH_RX_PIN, BLUETOOTH_TX_PIN);
AF_DCMotor motor_front_left(FRONT_LEFT_MOTOR_PORT);
AF_DCMotor motor_front_right(FRONT_RIGHT_MOTOR_PORT);
AF_DCMotor motor_back_left(BACK_LEFT_MOTOR_PORT);
AF_DCMotor motor_back_right(BACK_RIGHT_MOTOR_PORT);
uint8_t motor_front_left_state = RELEASE, motor_front_right_state = RELEASE, motor_back_left_state = RELEASE, motor_back_right_state = RELEASE;
int motor_front_left_speed = MAXIMUM_MOTOR_SPEED, motor_front_right_speed = MAXIMUM_MOTOR_SPEED, motor_back_left_speed = MAXIMUM_MOTOR_SPEED, motor_back_right_speed = MAXIMUM_MOTOR_SPEED;
String command;

// --- Function Prototypes ---
void forward(bool kick = false);
void back(bool kick = false);
void left();
void right();
void scribbystop();
void setMotorState(AF_DCMotor &motor, uint8_t *motorState, int newSpeed, uint8_t newDirection, bool kickstart);
// --- End System Internals ---


void setup() {
  motor_front_left.setSpeed(motor_front_left_speed);
  motor_front_right.setSpeed(motor_front_right_speed);
  motor_back_left.setSpeed(motor_back_left_speed);
  motor_back_right.setSpeed(motor_back_right_speed);
  BLESerial.begin(9600);
}

void loop() {
  if (BLESerial.available() > 0) {
    command = BLESerial.readStringUntil('\n');
    command.trim();
    // Simplified logic: if it has a hyphen, it's an advanced command. Otherwise, it's primitive.
    if (command.indexOf('-') != -1) {
      handleAdvancedCommand(command);
    } else {
      handleMoveCommand(command);
    }
  }
}

// Handler for w, a, s, d, x. Uses PRIMITIVE_MOVEMENT_SPEED.
void handleMoveCommand(String cmd) {
  // Synchronize advanced speed variables with the primitive speed
  motor_front_left_speed = motor_front_right_speed = motor_back_left_speed = motor_back_right_speed = PRIMITIVE_MOVEMENT_SPEED;
  
  if (cmd == "w") forward();
  else if (cmd == "a") left();
  else if (cmd == "d") right();
  else if (cmd == "s") scribbystop();
  else if (cmd == "x") back();
}

// Optimized handler for the new mnemonic commands.
void handleAdvancedCommand(String cmd) {
  int hyphenIndex = cmd.indexOf('-');
  if (hyphenIndex == -1) return;

  String prefix = cmd.substring(0, hyphenIndex);
  int speed = cmd.substring(hyphenIndex + 1).toInt();
  speed = constrain(speed, MINIMUM_MOTOR_SPEED, MAXIMUM_MOTOR_SPEED);

  bool forceKickstart = false;
  if (prefix.endsWith("k")) {
    forceKickstart = true;
    prefix.remove(prefix.length() - 1);
  }

  uint8_t direction = FORWARD;
  if (prefix.startsWith("b")) {
    direction = BACKWARD;
  }
  
  // Extract the target (gfl, gfr, etc.) by removing the direction character
  String target = prefix.substring(1);

  // --- Process commands ---
  if (target == "gfl") {
    motor_front_left_speed = speed;
    setMotorState(motor_front_left, &motor_front_left_state, speed, direction, forceKickstart);
  } else if (target == "gfr") {
    motor_front_right_speed = speed;
    setMotorState(motor_front_right, &motor_front_right_state, speed, direction, forceKickstart);
  } else if (target == "gbl") {
    motor_back_left_speed = speed;
    setMotorState(motor_back_left, &motor_back_left_state, speed, direction, forceKickstart);
  } else if (target == "gbr") {
    motor_back_right_speed = speed;
    setMotorState(motor_back_right, &motor_back_right_state, speed, direction, forceKickstart);
  } else if (target == "gall") {
    motor_front_left_speed = motor_front_right_speed = motor_back_left_speed = motor_back_right_speed = speed;
    if (direction == FORWARD) forward(forceKickstart); else back(forceKickstart);
  }
}

// CORE LOGIC: Sets motor state. Direction is absolute.
void setMotorState(AF_DCMotor &motor, uint8_t *motorState, int newSpeed, uint8_t newDirection, bool kickstart) {
  if (kickstart) {
    motor.setSpeed(MAXIMUM_MOTOR_SPEED);
    motor.run(newDirection);
    delay(KICKSTART_DELAY_MS);
  }
  
  motor.setSpeed(newSpeed);
  motor.run(newDirection);
  *motorState = newDirection; // Update state to the new absolute direction
}

// --- Robot Movement Functions ---
void forward(bool kick) {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, FORWARD, kick);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, FORWARD, kick);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, FORWARD, kick);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, FORWARD, kick);
}

void back(bool kick) {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, BACKWARD, kick);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, BACKWARD, kick);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, BACKWARD, kick);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, BACKWARD, kick);
}

void left() {
  setMotorState(motor_front_left, &motor_front_left_state, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_back_left, &motor_back_left_state, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_front_right, &motor_front_right_state, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_back_right, &motor_back_right_state, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
}

void right() {
  setMotorState(motor_front_left, &motor_front_left_state, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_back_left, &motor_back_left_state, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_front_right, &motor_front_right_state, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_back_right, &motor_back_right_state, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
}

void scribbystop() {
  setMotorState(motor_front_left, &motor_front_left_state, 0, RELEASE, false);
  setMotorState(motor_front_right, &motor_front_right_state, 0, RELEASE, false);
  setMotorState(motor_back_left, &motor_back_left_state, 0, RELEASE, false);
  setMotorState(motor_back_right, &motor_back_right_state, 0, RELEASE, false);
}
