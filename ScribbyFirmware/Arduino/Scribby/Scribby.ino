#include <AFMotor.h>
#include <SoftwareSerial.h>

// =================== ROBOT CONFIGURATION ===================
// --- 1. Motor Port Mapping ---
// Assign the physical motor shield port (1-4) to its logical location on the robot.
const int FRONT_LEFT_MOTOR_PORT  = 1;
const int BACK_LEFT_MOTOR_PORT   = 2;
const int BACK_RIGHT_MOTOR_PORT  = 3;
const int FRONT_RIGHT_MOTOR_PORT = 4;

// --- 2. Bluetooth Module Wiring ---
// Assign the Arduino pins for the Bluetooth module's RX and TX.
const int BLUETOOTH_RX_PIN = 9;
const int BLUETOOTH_TX_PIN = 10;

// --- 3. Performance Tuning ---
// Define the robot's speed limits and the kickstart pulse duration.
const int MINIMUM_MOTOR_SPEED  = 50;
const int MAXIMUM_MOTOR_SPEED  = 255;
const int KICKSTART_DELAY_MS = 200; 
// ===========================================================


// --- Hardware Object Initialization ---
// Initialize the Bluetooth serial connection using the pins defined above.
SoftwareSerial BLESerial(BLUETOOTH_RX_PIN, BLUETOOTH_TX_PIN);

// Initialize the motor objects using the logical names.
AF_DCMotor motor_front_left(FRONT_LEFT_MOTOR_PORT);
AF_DCMotor motor_front_right(FRONT_RIGHT_MOTOR_PORT);
AF_DCMotor motor_back_left(BACK_LEFT_MOTOR_PORT);
AF_DCMotor motor_back_right(BACK_RIGHT_MOTOR_PORT);

// --- State and Speed Tracking Variables ---
// The robot will start with its speed set to the defined maximum.
uint8_t motor_front_left_state = RELEASE;
uint8_t motor_front_right_state = RELEASE;
uint8_t motor_back_left_state = RELEASE;
uint8_t motor_back_right_state = RELEASE;

int motor_front_left_speed = MAXIMUM_MOTOR_SPEED;
int motor_front_right_speed = MAXIMUM_MOTOR_SPEED;
int motor_back_left_speed = MAXIMUM_MOTOR_SPEED;
int motor_back_right_speed = MAXIMUM_MOTOR_SPEED;

String command; // A string to hold incoming commands

void setup() {
  // Set the initial speed for all motors
  motor_front_left.setSpeed(motor_front_left_speed);
  motor_front_right.setSpeed(motor_front_right_speed);
  motor_back_left.setSpeed(motor_back_left_speed);
  motor_back_right.setSpeed(motor_back_right_speed);

  BLESerial.begin(9600); // Start serial for Bluetooth
}

void loop() {
  if (BLESerial.available() > 0) {
    command = BLESerial.readStringUntil('\n');
    command.trim();

    if (command.startsWith("sp")) {
      handleAdvancedCommand(command);
    } else {
      handleMoveCommand(command);
    }
  }
}

// Function to handle simple movement commands (w, a, s, d, x)
void handleMoveCommand(String cmd) {
  if (cmd == "w") forward();
  else if (cmd == "a") left();
  else if (cmd == "d") right();
  else if (cmd == "s") scribbystop();
  else if (cmd == "x") back();
}

// Revised advanced command handler using logical motor names and configured speed limits
void handleAdvancedCommand(String cmd) {
  int hyphenIndex = cmd.indexOf('-');
  if (hyphenIndex == -1) return; // Exit if command is malformed

  String prefix = cmd.substring(0, hyphenIndex);
  int speed = cmd.substring(hyphenIndex + 1).toInt();
  // Enforce the speed limits using the configured constants
  speed = constrain(speed, MINIMUM_MOTOR_SPEED, MAXIMUM_MOTOR_SPEED);

  // Speed & Go Commands
  if (prefix == "spflg") {
    motor_front_left_speed = speed;
    motor_front_left.setSpeed(speed);
    setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, FORWARD);
  } else if (prefix == "spfrg") {
    motor_front_right_speed = speed;
    motor_front_right.setSpeed(speed);
    setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, FORWARD);
  } else if (prefix == "spblg") {
    motor_back_left_speed = speed;
    motor_back_left.setSpeed(speed);
    setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, FORWARD);
  } else if (prefix == "spbrg") {
    motor_back_right_speed = speed;
    motor_back_right.setSpeed(speed);
    setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, FORWARD);
  }

  // Speed-Only Commands
  else if (prefix == "spfl") {
    motor_front_left_speed = speed;
    motor_front_left.setSpeed(speed);
  } else if (prefix == "spfr") {
    motor_front_right_speed = speed;
    motor_front_right.setSpeed(speed);
  } else if (prefix == "spbl") {
    motor_back_left_speed = speed;
    motor_back_left.setSpeed(speed);
  } else if (prefix == "spbr") {
    motor_back_right_speed = speed;
    motor_back_right.setSpeed(speed);
  }

  // "All Motors" Commands
  else if (prefix == "spallg") {
    motor_front_left_speed = motor_front_right_speed = motor_back_left_speed = motor_back_right_speed = speed;
    motor_front_left.setSpeed(speed);
    motor_front_right.setSpeed(speed);
    motor_back_left.setSpeed(speed);
    motor_back_right.setSpeed(speed);
    forward();
  } else if (prefix == "spall") {
    motor_front_left_speed = motor_front_right_speed = motor_back_left_speed = motor_back_right_speed = speed;
    motor_front_left.setSpeed(speed);
    motor_front_right.setSpeed(speed);
    motor_back_left.setSpeed(speed);
    motor_back_right.setSpeed(speed);
  }
}

// CORE LOGIC: Set Motor State with Automatic Kickstart
void setMotorState(AF_DCMotor &motor, uint8_t *motorState, int motorSpeed, uint8_t newDirection) {
  if (*motorState == RELEASE && newDirection != RELEASE) {
    motor.setSpeed(MAXIMUM_MOTOR_SPEED); // Kickstart with configured max speed
    motor.run(newDirection);
    delay(KICKSTART_DELAY_MS);
    motor.setSpeed(motorSpeed);
  } else {
    motor.run(newDirection);
  }
  *motorState = newDirection;
}

// --- Robot Movement Functions ---
void forward() {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, FORWARD);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, FORWARD);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, FORWARD);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, FORWARD);
}

void back() {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, BACKWARD);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, BACKWARD);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, BACKWARD);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, BACKWARD);
}

void right() {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, FORWARD);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, FORWARD);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, BACKWARD);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, BACKWARD);
}

void left() {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, BACKWARD);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, BACKWARD);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, FORWARD);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, FORWARD);
}

void scribbystop() {
  setMotorState(motor_front_left, &motor_front_left_state, motor_front_left_speed, RELEASE);
  setMotorState(motor_front_right, &motor_front_right_state, motor_front_right_speed, RELEASE);
  setMotorState(motor_back_left, &motor_back_left_state, motor_back_left_speed, RELEASE);
  setMotorState(motor_back_right, &motor_back_right_state, motor_back_right_speed, RELEASE);
}
