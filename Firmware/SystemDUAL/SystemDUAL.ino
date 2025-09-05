#define IS_MASTER 1  // 1 = Master, 0 = Slave

#include <Arduino.h>

// --- Pin definitions ---
#define MICA A0
#define MICB A1

#define SDOWN 2
#define CLUP 4
#define CLUP_TEMP 4

#define AC220 3
#define AC110 11

#define LPY 6
#define LPG 5
#define LPR 7
#define BZ 8

#define RLY1 26
#define RLY2 27
#define RLY3 28
#define RLY4 29

#define KEY1 22
#define KEY2 23
#define KEY3 24
#define KEY4 25

// --- Buffers ---
uint8_t systemRespInput[10] = { 0x44, 0x45, 0x06, 0x49, 0x00, 0xE8, 0x00, 0x00, 0x52, 0x56 };
uint8_t systemRespOutput[10] = { 0x44, 0x45, 0x4F, 0x00, 0x52, 0x56 };

// Global variable needed for debounce tracking
uint8_t LastStartState = 0;

// --- Slave state ---
#if IS_MASTER
uint8_t SDOWN_remote = 0;
#else
int SlaveNeedUp = 0;
int MasterNeedUp = 0;

void OperateUp() {
  if (SlaveNeedUp == 1 && MasterNeedUp == 1) {
    digitalWrite(CLUP_TEMP, HIGH);
    delay(1000);
    SlaveNeedUp = 0;
    MasterNeedUp = 0;
  }
  if (SlaveNeedUp == 0 && MasterNeedUp == 0) {
    digitalWrite(CLUP_TEMP, LOW);
  }
}

#endif

uint8_t startSignal = 0;
void CollectInput() {
#if !IS_MASTER
  // Slave mode - read directly from pin and respond to master
  startSignal = digitalRead(SDOWN);
  uint8_t response[2] = { 0xBA, startSignal };
  Serial1.write(response, 2);

#else

  // Check for incoming SDOWN status from slave
  if (Serial1.available() >= 2) {
    uint8_t header = Serial1.read();
    if (header == 0xBA) {  // SDOWN status response header
      int temp = Serial1.read();
      if (temp == 0x00 || temp == 0x01) {
        SDOWN_remote = temp;
      }
    }
  }
  startSignal = SDOWN_remote;
#endif

  // Common debounce and state change handling
  if (startSignal != LastStartState) {
    delay(500);  // Shorter debounce delay for better responsiveness
    LastStartState = startSignal;

    // Handle state change
    if (startSignal == HIGH) {
#if !IS_MASTER
      MasterNeedUp = 0;
      SlaveNeedUp = 0;
      OperateUp();
#endif

      digitalWrite(LPR, LOW);
      digitalWrite(LPY, HIGH);
      digitalWrite(LPG, LOW);
    } else {
      digitalWrite(AC110, LOW);
      digitalWrite(AC220, LOW);
    }

    // Update response packet
    bitWrite(systemRespInput[5], 1, startSignal);
    // Calculate checksum
    uint8_t xorTemp = systemRespInput[0];
    for (int i = 1; i < 8; i++) {
      xorTemp ^= systemRespInput[i];
    }
    systemRespInput[8] = xorTemp;
    // Send to PC
    Serial.write(systemRespInput, 10);
  }
}


// ================= WRAPPERS ===================
uint8_t ReadSDOWN() {
#if IS_MASTER
  return SDOWN_remote;  // Master nhận SDOWN từ Slave qua Serial1
#else
  return digitalRead(SDOWN);  // Slave đọc trực tiếp
#endif
}


void WriteCLUP(uint8_t val) {
#if IS_MASTER
  uint8_t buf[2] = { 0xAA, val };  // chỉ gửi CLUP
  Serial1.write(buf, 2);           // Master gửi CLUP cho Slave
#else
  if (val == 1) {
    SlaveNeedUp = 1;
    OperateUp();
  }
#endif
}

// ================= INIT ===================
void SetSystemIOPinMode() {
  Serial.begin(9600);   // Serial0: PC
  Serial1.begin(9600);  // Serial1: Master <-> Slave

#if !IS_MASTER
  pinMode(SDOWN, INPUT);
  pinMode(CLUP, OUTPUT);
#endif
  pinMode(CLUP_TEMP, OUTPUT);

  pinMode(MICA, INPUT);
  pinMode(MICB, INPUT);

  pinMode(RLY1, OUTPUT);
  pinMode(RLY2, OUTPUT);
  pinMode(RLY3, OUTPUT);
  pinMode(RLY4, OUTPUT);

  pinMode(KEY1, OUTPUT);
  pinMode(KEY2, OUTPUT);
  pinMode(KEY3, OUTPUT);
  pinMode(KEY4, OUTPUT);

  pinMode(LPG, OUTPUT);
  pinMode(LPY, OUTPUT);
  pinMode(LPR, OUTPUT);
  pinMode(BZ, OUTPUT);
  pinMode(AC110, OUTPUT);
  pinMode(AC220, OUTPUT);
}

// ================= SLAVE PROCESS ===================
#if !IS_MASTER
void SlaveProcess() {
  static uint8_t buf[2];
  static int idx = 0;
  while (Serial1.available()) {
    buf[idx++] = Serial1.read();
    if (idx >= 2) {
      idx = 0;
      if (buf[0] == 0xAA) {
        uint8_t clup_val = buf[1];
        if (clup_val == 1) {
          MasterNeedUp = clup_val;
          OperateUp();
        }
      }
    }
  }
}
#endif

// ================= COMMON FUNCTIONS ===================
void SetSolenoidPin(uint8_t data[4]) {
  uint32_t d = 0;
  for (int i = 0; i < 4; i++) d = (d << 8) | data[i];
  for (int i = 0; i < 4; i++) digitalWrite(i + 22, bitRead(d, i + 4));
}

void ResponseMIC() {
  unsigned char frame[10] = { 0x44, 0x45, 0x06, 0x50, 0, 0, 0, 0, 0, 0x56 };
  uint16_t mic_a = analogRead(MICA);
  uint16_t mic_b = analogRead(MICB);

  frame[4] = mic_a & 0xFF;
  frame[5] = (mic_a >> 8) & 0xFF;
  frame[6] = mic_b & 0xFF;
  frame[7] = (mic_b >> 8) & 0xFF;

  unsigned char xor_tmp = frame[0];
  for (int i = 1; i < 8; i++) xor_tmp ^= frame[i];
  frame[8] = xor_tmp;

  Serial.write(frame, 10);  // PC
}

void ResponseInput() {
  uint8_t startSignal = ReadSDOWN();
  bitWrite(systemRespInput[5], 1, startSignal);

  unsigned char xorTemp = systemRespInput[0];
  for (int i = 1; i < 8; i++) xorTemp ^= systemRespInput[i];
  systemRespInput[8] = xorTemp;

  Serial.write(systemRespInput, 10);  // PC
}

void SetSystemOutput(uint8_t data[4]) {
  uint32_t d = 0;
  for (int i = 0; i < 4; i++) d = (d << 8) | data[i];

  WriteCLUP(bitRead(d, 0));  // Master gửi, Slave ghi trực tiếp

  digitalWrite(LPR, bitRead(d, 8));
  digitalWrite(LPY, bitRead(d, 9));
  digitalWrite(LPG, bitRead(d, 10));
  digitalWrite(BZ, bitRead(d, 11));
  // digitalWrite(RLY6, bitRead(d, 12));

  if (ReadSDOWN()) {
    digitalWrite(AC110, bitRead(d, 24));
    digitalWrite(AC220, bitRead(d, 26));
  }

  digitalWrite(RLY1, bitRead(d, 29));
  digitalWrite(RLY2, bitRead(d, 30));
  digitalWrite(RLY3, bitRead(d, 21));
  digitalWrite(RLY4, bitRead(d, 22));
  // digitalWrite(RLY5, bitRead(d, 23));
}

// ================= SERIAL0 EVENT ===================
void serialEvent() {
  uint8_t bytesData[20];
  if (Serial.available()) {
    delay(10);
    uint8_t readbyte = Serial.read();
    if (readbyte == 0x44) {
      int index = 0;
      bytesData[index] = readbyte;
      while (Serial.available()) {
        readbyte = Serial.read();
        index++;
        bytesData[index] = readbyte;
        if (index >= 9) {
          // check solenoid command
          if ((bytesData[2] != 0x49) && (bytesData[2] != 0x50)) {
            if (bytesData[3] == 0x53) {
              uint8_t bytes[4] = { bytesData[4], bytesData[5], bytesData[6], bytesData[7] };
              SetSolenoidPin(bytes);
              Serial.write(systemRespOutput, 6);
            }
          }
          // check output command
          if (bytesData[3] == 0x4F) {
            uint8_t bytes[4] = { bytesData[4], bytesData[5], bytesData[6], bytesData[7] };
            SetSystemOutput(bytes);
            Serial.write(systemRespOutput, 6);
          }
          break;
        }
        if (index == 2 && bytesData[2] == 0x49) {
          ResponseInput();
        }
        if (index == 2 && bytesData[2] == 0x50) {
          ResponseMIC();
          break;
        }
      }
    }
    while (Serial.available()) Serial.read();  // clear buffer
  }
}

// ================= SETUP / LOOP ===================
void setup() {
  SetSystemIOPinMode();
}

void loop() {
  CollectInput();
#if !IS_MASTER
  SlaveProcess();  // cập nhật Serial1 nếu là Slave
#endif
}
