#include "IOPin.h"

void ResponseAnalogSevenSegment()
{
    int frame_length = NUMBER_SEG_PIN * 2 + 6;
    uint8_t response_frame[frame_length] = {0x44, 0x45, 0x1D, 0x51, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x56};

    for (int pin_idx = 0; pin_idx < NUMBER_SEG_PIN; pin_idx++)
    {
        int data10bit = SegmentAnalogRead(pin_idx, AnalogPinMapSS);

        byte higherByte = (byte)((data10bit >> (10 - 2)) & 0x03);
        byte lowerByte = (byte)(data10bit & 0xFF);

        response_frame[4 + pin_idx * 2] = higherByte;
        response_frame[4 + pin_idx * 2 + 1] = lowerByte;
    }

    unsigned char xor_tmp = response_frame[0];
    for (int i = 1; i < frame_length - 2; i++)
    {
        xor_tmp ^= response_frame[i];
    }
    response_frame[frame_length - 2] = xor_tmp;
    Serial.write(response_frame, frame_length);
}
 void  loop(){
  
 }
void ResponseAnalogLED()
{
    int frame_length = NUMBER_LED_PIN * 2 + 6;
    uint8_t response_frame[frame_length] = {0x44, 0x45, 0x1D, 0x52, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x56};

    for (int pin_idx = 0; pin_idx < NUMBER_LED_PIN; pin_idx++)
    {
        int data10bit = SegmentAnalogRead(pin_idx, AnalogPinMapLED);

        byte higherByte = (byte)((data10bit >> (10 - 2)) & 0x03);
        byte lowerByte = (byte)(data10bit & 0xFF);

        response_frame[4 + pin_idx * 2] = higherByte;
        response_frame[4 + pin_idx * 2 + 1] = lowerByte;
    }

    unsigned char xor_tmp = response_frame[0];
    for (int i = 1; i < frame_length - 2; i++)
    {
        xor_tmp ^= response_frame[i];
    }
    response_frame[frame_length - 2] = xor_tmp;
    Serial.write(response_frame, frame_length);
}

void setup()
{
    Serial.begin(115200);
    SetBoardPinMode();
}

// runs whenever there's serial buffer
// System control event
void serialEvent()
{
    uint8_t bytes_data[20];
    if (Serial.available())
    {
        delay(10);
        uint8_t read_byte = Serial.read();
        if (read_byte == 0x44)
        {
            int index = 0;
            bytes_data[index] = read_byte;
            while (Serial.available())
            {
                read_byte = Serial.read();
                index++;
                bytes_data[index] = read_byte;

                if (index >= 9)
                {
                    if ((bytes_data[2] != 0x49) && (bytes_data[2] != 0x50) && (bytes_data[2] != 0x51) && (bytes_data[2] != 0x52))
                    {
                        if (bytes_data[3] == 0x53)
                        {
                            uint8_t bytes[4] = {bytes_data[4], bytes_data[5], bytes_data[6], bytes_data[7]};
                            SetSolenoidPin(bytes);
                            uint8_t response_bytes[] = {0x44, 0x45, 0x53, 0x00, 0x52, 0x56};
                            Serial.write(response_bytes, 6);
                        }
                    }

                    break;
                }

                if (index == 2 && bytes_data[2] == 0x51)
                {
                    ResponseAnalogSevenSegment();
                    break;
                }
                if (index == 2 && bytes_data[2] == 0x52)
                {
                    ResponseAnalogLED();
                    break;
                }
            }
            while (Serial.available())
            {
                read_byte = Serial.read();
            }
        }
    }
}
