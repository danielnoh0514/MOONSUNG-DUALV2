#define NUMBER_SEG_PIN 14
#define NUMBER_LED_PIN 8


#define SW1 48
#define SW2 49
#define SW3 50
#define SW4 51

#define SW5 8
#define SW6 9
#define SW7 10
#define SW8 11

//                                    |Digit0                           |Digit1                   |

int AnalogPinMapSS[NUMBER_SEG_PIN] = {A9, A10, A11, A12, A13, A14, A15, A2, A3, A4, A5, A6, A7, A8};
int AnalogPinMapLED[8] = {A2, A3, A4, A5, A6, A7, A8, A9};

void SetSolenoidPinMode()
{
  pinMode(SW1, OUTPUT);
  pinMode(SW2, OUTPUT);
  pinMode(SW3, OUTPUT);
  pinMode(SW4, OUTPUT);
  pinMode(SW5, OUTPUT);
  pinMode(SW6, OUTPUT);
  pinMode(SW7, OUTPUT);
  pinMode(SW8, OUTPUT);
}



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

  SetSolenoidPinMode();
}

void SetSolenoidPin(uint8_t data[4])
{
  uint32_t data32 = 0x00000000;

  for (int index = 0; index < 4; index++)
  {
    data32 = data32 << 8 | data[index];
  }
  for (int i = 0; i < 4; i++)
  {
    digitalWrite(i + 48, bitRead(data32, i));
  }
  for (int i = 0; i < 4; i++)
  {
    digitalWrite(i + 8, bitRead(data32, i + 4));
  }
}

int SegmentAnalogRead(int seg_idx, int seg_pin_list[])
{
  int analog_value = analogRead(seg_pin_list[seg_idx]);
  return analog_value;
}
