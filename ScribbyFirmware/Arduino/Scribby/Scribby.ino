#include <AFMotor.h>
#include <SoftwareSerial.h>

// =================== ROBOT CONFIGURATION (V15.1 - Corrected) ===================
// --- 1. Hardware Mapping ---
const int FRONT_LEFT_MOTOR_PORT  = 1;
const int BACK_LEFT_MOTOR_PORT   = 2;
const int BACK_RIGHT_MOTOR_PORT  = 3;
const int FRONT_RIGHT_MOTOR_PORT = 4;
const int BLUETOOTH_RX_PIN = 9;
const int BLUETOOTH_TX_PIN = 10;

// --- 2. Performance Tuning ---
const int MINIMUM_MOTOR_SPEED  = 80;
const int MAXIMUM_MOTOR_SPEED  = 255;
const int KICKSTART_DELAY_MS   = 100;

// --- 3. Primitive Movement Speed ---
const int PRIMITIVE_MOVEMENT_SPEED = 255;
// =================================================================


// --- System Internals (Optimized for performance) ---
SoftwareSerial BLESerial(BLUETOOTH_RX_PIN, BLUETOOTH_TX_PIN);
AF_DCMotor motor_front_left(FRONT_LEFT_MOTOR_PORT);
AF_DCMotor motor_front_right(FRONT_RIGHT_MOTOR_PORT);
AF_DCMotor motor_back_left(BACK_LEFT_MOTOR_PORT);
AF_DCMotor motor_back_right(BACK_RIGHT_MOTOR_PORT);

const byte COMMAND_BUFFER_SIZE = 16;
char command_buffer[COMMAND_BUFFER_SIZE];
byte command_pos = 0;
bool command_ready = false;

// --- CORRECTED: Function Prototypes ---
// This tells the compiler that these functions exist before they are used.
void readSerialCommand();
void processCommand();
void handlePrimitiveCommand(char* cmd);
void handleAdvancedCommand(char* cmd);
void setMotorState(AF_DCMotor &motor, int newSpeed, uint8_t newDirection, bool kickstart);
void forward(int speed = PRIMITIVE_MOVEMENT_SPEED, bool kick = false);
void back(int speed = PRIMITIVE_MOVEMENT_SPEED, bool kick = false);
void left();
void right();
void scribbystop();
// --- End System Internals ---


void setup() {
  motor_front_left.setSpeed(PRIMITIVE_MOVEMENT_SPEED);
  motor_front_right.setSpeed(PRIMITIVE_MOVEMENT_SPEED);
  motor_back_left.setSpeed(PRIMITIVE_MOVEMENT_SPEED);
  motor_back_right.setSpeed(PRIMITIVE_MOVEMENT_SPEED);
  BLESerial.begin(9600);
}

void loop() {
  readSerialCommand();
  if (command_ready) {
    processCommand();
    command_pos = 0;
    command_ready = false;
  }
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
  if (strcmp(cmd, "w") == 0) forward();
  else if (strcmp(cmd, "a") == 0) left();
  else if (strcmp(cmd, "d") == 0) right();
  else if (strcmp(cmd, "s") == 0) scribbystop();
  else if (strcmp(cmd, "x") == 0) back();
}

void handleAdvancedCommand(char* cmd) {
  char* hyphen = strchr(cmd, '-');
  if (hyphen == NULL) return;

  int speed = atoi(hyphen + 1);
  speed = constrain(speed, MINIMUM_MOTOR_SPEED, MAXIMUM_MOTOR_SPEED);
  *hyphen = '\0';

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

  if (strcmp(target, "gfl") == 0) setMotorState(motor_front_left, speed, direction, forceKickstart);
  else if (strcmp(target, "gfr") == 0) setMotorState(motor_front_right, speed, direction, forceKickstart);
  else if (strcmp(target, "gbl") == 0) setMotorState(motor_back_left, speed, direction, forceKickstart);
  else if (strcmp(target, "gbr") == 0) setMotorState(motor_back_right, speed, direction, forceKickstart);
  else if (strcmp(target, "gall") == 0) {
    if (direction == FORWARD) forward(speed, forceKickstart); else back(speed, forceKickstart);
  }
}

void setMotorState(AF_DCMotor &motor, int newSpeed, uint8_t newDirection, bool kickstart) {
  if (kickstart) {
    motor.setSpeed(MAXIMUM_MOTOR_SPEED);
    motor.run(newDirection);
    delay(KICKSTART_DELAY_MS);
  }
  motor.setSpeed(newSpeed);
  motor.run(newDirection);
}

void forward(int speed, bool kick) {
  setMotorState(motor_front_left, speed, FORWARD, kick);
  setMotorState(motor_front_right, speed, FORWARD, kick);
  setMotorState(motor_back_left, speed, FORWARD, kick);
  setMotorState(motor_back_right, speed, FORWARD, kick);
}

void back(int speed, bool kick) {
  setMotorState(motor_front_left, speed, BACKWARD, kick);
  setMotorState(motor_front_right, speed, BACKWARD, kick);
  setMotorState(motor_back_left, speed, BACKWARD, kick);
  setMotorState(motor_back_right, speed, BACKWARD, kick);
}

void left() {
  setMotorState(motor_front_left, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_back_left, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_front_right, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_back_right, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
}

void right() {
  setMotorState(motor_front_left, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_back_left, PRIMITIVE_MOVEMENT_SPEED, FORWARD, false);
  setMotorState(motor_front_right, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
  setMotorState(motor_back_right, PRIMITIVE_MOVEMENT_SPEED, BACKWARD, false);
}

void scribbystop() {
  motor_front_left.run(RELEASE);
  motor_front_right.run(RELEASE);
  motor_back_left.run(RELEASE);
  motor_back_right.run(RELEASE);
}
