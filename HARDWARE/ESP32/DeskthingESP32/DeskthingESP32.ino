#include <Arduino.h>
#include <TJpg_Decoder.h>
#include <Adafruit_GFX.h>
#include <Adafruit_ST7789.h>
#include <Fonts/FreeSans9pt7b.h>

#define TFT_CS 1
#define TFT_DC 0
#define TFT_RST 4
#define TFT_SCLK 6
#define TFT_MOSI 7

#define NUM_BAND 16
#define BAR_HEIGHT 60

Adafruit_ST7789 display = Adafruit_ST7789(TFT_CS, TFT_DC, TFT_RST);

uint16_t waveColor = ST77XX_CYAN;
uint16_t backColor = ST77XX_BLACK;

const size_t MAX_IMG_SIZE = 15000;
uint8_t imgBuf[MAX_IMG_SIZE];
uint32_t imgSize = 0;
uint32_t imgBytesReceived = 0;
bool imgReceiving = false;

char serialPrefix[5] = {0};
String lineBuffer = "";

bool testMode = false;

int spectrum[NUM_BAND];

// TJpgDecoder callback for drawing to ST7789
bool tftDrawCallback(int16_t x, int16_t y, uint16_t w, uint16_t h, uint16_t* bitmap) {
  Serial.printf("Drawing block: %d,%d  %d×%d\n", x, y, w, h);
  display.startWrite();
  display.setAddrWindow(x, y, w, h);
  display.writePixels(bitmap, w * h);
  display.endWrite();
  return true;
}


void setup() {
  Serial.begin(921600);

  SPI.begin(TFT_SCLK, -1, TFT_MOSI, -1);
  SPI.setFrequency(40000000);

  display.init(240, 320);
  display.setRotation(3);
  display.fillScreen(ST77XX_BLACK);
  display.setTextColor(ST77XX_WHITE);
  display.setTextSize(1);
  display.setFont(&FreeSans9pt7b);

  TJpgDec.setCallback(tftDrawCallback);

  drawFixedTextAndSymbols();

  if(testMode){
    //display fake audio bars and values 
     for(int i = 0; i < NUM_BAND; i++){
      spectrum[i] = 255 - i * 10;
     }
     drawBars();

     drawFakeImage();

     drawUserInfo("User@PCNAME");
     drawWindowsInfo("Windows 11");

     drawCpuTemp("55C");
     drawGpuTemp("45C");
     drawRam("10/64Go");

     drawMediaTitle("DECO - Internet overdose");
  }
}

void loop() {
  if (Serial.available() >= 4) {
    char prefix[5] = {0};
    Serial.readBytes((uint8_t*)prefix, 4);
    prefix[4] = '\0';

    if (strcmp(prefix, "IMG:") == 0) {
      Serial.println("[IMG] Start receiving image...");

      // Read 4-byte image size
      while (Serial.available() < 4) delay(1);
      uint8_t sizeBytes[4];
      Serial.readBytes(sizeBytes, 4);
      uint32_t size = sizeBytes[0] | (sizeBytes[1] << 8) | (sizeBytes[2] << 16) | (sizeBytes[3] << 24);
      Serial.printf("[IMG] Declared size: %lu bytes\n", size);
      if (size == 0 || size > MAX_IMG_SIZE) {
        Serial.printf("[IMG] Invalid size: %lu\n", size);
        return;
      }
      if (size > MAX_IMG_SIZE) {
        Serial.printf("[IMG] Error: image too big (max %u bytes)\n", MAX_IMG_SIZE);
        while (Serial.available() < size) delay(1);
        Serial.readBytes(imgBuf, size);
        return;
      }

      // Read image bytes
      uint32_t bytesRemaining = size;
      uint8_t* ptr = imgBuf;

      while (bytesRemaining > 0) {
        if (Serial.available()) {
          int chunk = Serial.read(ptr, bytesRemaining);
          if (chunk > 0) {
            ptr += chunk;
            bytesRemaining -= chunk;
          }
        }
      }

      Serial.println("[IMG] Data fully received");

      // Consume newline
      while (Serial.available() && Serial.read() != '\n') {}

      Serial.printf("[IMG] Free heap before decode: %lu bytes\n", ESP.getFreeHeap());
      TJpgDec.setJpgScale(1);
      JRESULT res = TJpgDec.drawJpg(0, 0, imgBuf, size);

      if (res == JDR_OK)
        Serial.println("[IMG] Image drawn successfully");
      else
        Serial.printf("[IMG] JPEG decode failed (code %d)\n", res);

      Serial.printf("[IMG] Free heap after decode: %lu bytes\n", ESP.getFreeHeap());
    }
    else {
      String line = String(prefix) + Serial.readStringUntil('\n');
      line.trim();

      if (line.startsWith("SOUND:")) { parseBars(line.substring(6)); drawBars(); }
      else if (line.startsWith("WIND:")) { drawWindowsInfo(line.substring(5)); }
      else if (line.startsWith("USER:")) { drawUserInfo(line.substring(5)); }
      else if (line.startsWith("CPUT:")) { drawCpuTemp(line.substring(5)); }
      else if (line.startsWith("GPUT:")) { drawGpuTemp(line.substring(5)); }
      else if (line.startsWith("RAM:")) { drawRam(line.substring(4)); }
      else if (line.startsWith("TITLE:")) { drawMediaTitle(line.substring(6)); }
      else if (line.startsWith("PALE:")) { setPalette(line.substring(5)); }
    }
  }
}

void parseBars(String values) {
  int index = 0;
  int lastComma = 0;
  for (int i = 0; i < NUM_BAND; i++) {
    int commaIndex = values.indexOf(',', lastComma);
    if (commaIndex == -1) commaIndex = values.length();
    spectrum[i] = values.substring(lastComma, commaIndex).toInt();
    lastComma = commaIndex + 1;
  }
}

void drawBars() {
  display.fillRect(0, display.height() - BAR_HEIGHT, display.width(), BAR_HEIGHT, backColor);

  int barWidth = display.width() / NUM_BAND;
  for (int i = 0; i < NUM_BAND; i++) {
    int h = map(spectrum[i], 0, 255, 0, BAR_HEIGHT);
    display.fillRect(i * barWidth, display.height() - h, barWidth - 2, h, waveColor);
  }
}

void drawMediaTitle(String text){
  display.fillRect(20, display.height() - (BAR_HEIGHT + 25), display.width(), 20, backColor);

  display.setCursor(20, display.height() - (BAR_HEIGHT + 10));
  display.print(text);
}

void drawFixedTextAndSymbols(){
  display.setCursor(160, 90);
  display.print("CPU ->");
  display.setCursor(160, 115);
  display.print("GPU ->");
  display.setCursor(160, 140);
  display.print("RAM ->");

  display.setCursor(4, display.height() - (BAR_HEIGHT + 18));
  display.setFont();
  display.setTextSize(2);
  display.write(0x10);
  display.setFont(&FreeSans9pt7b);
  display.setTextSize(1);
}

void drawFakeImage(){
  display.fillRect(0, 0, 150, 150, backColor);
}

void drawWindowsInfo(String text){
  display.setCursor(160, 45);
  display.fillRect(160, 30, 160, 25, backColor);
  display.print(text);
}

void drawUserInfo(String text){
  display.setCursor(160, 20);
  display.fillRect(160, 5, 160, 25, backColor);
  display.print(text);
}

void drawCpuTemp(String text){
  display.setCursor(230, 90);
  display.fillRect(230, 75, 90, 25, backColor);
  display.print(text);
}

void drawGpuTemp(String text){
  display.setCursor(230, 115);
  display.fillRect(230, 100, 90, 25, backColor);
  display.print(text);
}

void drawRam(String text){
  display.setCursor(230, 140);
  display.fillRect(230, 125, 90, 25, backColor);
  display.print(text);
}

void setPalette(String paletteName) {
  paletteName.toUpperCase();
  //todo
}
