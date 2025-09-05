#define NUMBER_SEG_PIN  13
#define NUMBER_LED_PIN  13

//                                    |Digit2                        |Digit3 |Colon  |Icon |
int AnalogPinMapSS[NUMBER_SEG_PIN] = {A6, A7, A8, A9, A10, A11, A12, A0, A1, A4, A5, A2, A3};
int AnalogPinMapLED[NUMBER_LED_PIN] = {A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12};

void SetBoardPinMode()
{
  for (int i = 0; i < NUMBER_SEG_PIN; i++)
  {
    pinMode(AnalogPinMapSS[i], INPUT);
  }
  for (int i = 0; i < NUMBER_LED_PIN; i++)
  {
    pinMode(AnalogPinMapLED[i], INPUT);
  }
}

int SegmentAnalogRead(int seg_idx, int seg_pin_list[])
{
  int analog_value = analogRead(seg_pin_list[seg_idx]);
  return analog_value;
}
