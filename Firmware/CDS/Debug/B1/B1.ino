#include "IOPin.h"


void loop() {
  for (int i = 0; i < 6; i++) {
    int val = SegmentAnalogRead(i, AnalogPinMapLED);
    Serial.print(" LED");
    Serial.print(i + 14);
    Serial.print(" ");
    Serial.print(val);
  }
  Serial.println();
  delay(500);
}

void setup() {
  Serial.begin(115200);
  SetBoardPinMode();
}
