#define ST7789_DRIVER

// --- Pin definitions for your wiring ---
#define TFT_MOSI 7
#define TFT_SCLK 6
#define TFT_CS   3
#define TFT_DC   2
#define TFT_RST  10

// --- Display size ---
#define TFT_WIDTH  240
#define TFT_HEIGHT 320

// --- Optional SPI speed (reduce if unstable) ---
#define SPI_FREQUENCY  40000000  // 40 MHz
