#include <TFT_eSPI.h>
TFT_eSPI tft = TFT_eSPI();

void setup() {
  tft.init();
  tft.fillScreen(TFT_BLUE);
  tft.setScrollArea(0, 240, 0);
  for (int i = 0; i < 100; i++) {
    tft.scroll(2);
    delay(30);
  }
  tft.fillScreen(TFT_GREEN);
}

void loop() {}
