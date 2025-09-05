/// Solenoid board by arduino control 
//
//----------Data recive frame------------------
//0x44	0x45	0x06	0x53	Data1	Data2	Data3	Data4	CS	0x56
//
//----------Data response frame----------------
//0x44	0x45	0x53	0x00	CS	0x56
//
// Serial communication config: 115200 8 N 1

#include "SolenoidPin.h"

uint8_t responseBytes[] = {0x44,0x45,0x53,0x00,0x52,0x56};

void setup()
{
    Serial.begin(9600);
    SetSolenoidPinMode();
}

void loop()
{ 
}

//Solenoid control event
void serialEvent()
{
   uint8_t bytesData[20];
   if(Serial.available())
   {
        delay(10);
        uint8_t readdedbyte = Serial.read();
        if(readdedbyte == 0x44)
        {
            int index = 0;
            bytesData[index] = readdedbyte;
            while (Serial.available())
            {
                readdedbyte = Serial.read();
                index ++;
                bytesData[index] = readdedbyte;
                if(index >= 9)
                {
                    if(bytesData[3] == 0x53)
                    {
                        uint8_t bytes[4] = {bytesData[4],bytesData[5],bytesData[6],bytesData[7]};
                        SetSolenoidPin(bytes);
                        Serial.write(responseBytes, 6);
                    }
                    break;
                }
            }
            while (Serial.available())
            {
                readdedbyte = Serial.read();
            }
        }
   }
}

