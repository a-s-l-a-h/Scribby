#include <AFMotor.h>
#include<SoftwareSerial.h>
SoftwareSerial BLESerial(9,10);
AF_DCMotor motor1(1);
AF_DCMotor motor2(2);
AF_DCMotor motor3(3);
AF_DCMotor motor4(4);
void forward();
void back();
void right();
void left();
void Robostop();

char command;

void setup() {
  motor1.setSpeed(255);
  motor2.setSpeed(255);
  motor3.setSpeed(255);
  motor4.setSpeed(255);
  BLESerial.begin(9600); // Initialize serial communication
}

void loop() {
  if (BLESerial.available() > 0) {
     command = BLESerial.read(); // Read the incoming character

    if (command == 'w') {
      forward(); // Start the motor
    } else if (command == 'a') {
      left(); // Stop the motor
    } else if(command=='d') {
      right();
    } else if(command=='s'){
      Robostop();
    } else if(command=='x'){
      back();
    }
  }
}
void forward(){
  motor1.run(FORWARD);
  motor2.run(FORWARD);
  motor3.run(FORWARD);
  motor4.run(FORWARD);
}
void back(){
  motor1.run(BACKWARD);
  motor2.run(BACKWARD);
  motor3.run(BACKWARD);
  motor4.run(BACKWARD);

}
void right(){
  motor1.run(FORWARD);
  motor2.run(FORWARD);
  motor3.run(BACKWARD);
  motor4.run(BACKWARD);

}
void left(){
  motor1.run(BACKWARD);
  motor2.run(BACKWARD);
  motor3.run(FORWARD);
  motor4.run(FORWARD);
}
void Robostop(){
  motor1.run(RELEASE);
  motor2.run(RELEASE);
  motor3.run(RELEASE);
  motor4.run(RELEASE);
}
