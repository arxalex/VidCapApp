#define NUM_LEDS 92
#define MAX_POWER_MILAMP 2000
#define BODSRATE 500000
#define CONTROL_LED_PIN 13
#define TIME_TO_CONNECT 0.3

#include <FastLED.h>
CRGB leds[NUM_LEDS];
boolean connectionStatus;
unsigned long loopTime;
byte brightness;
bool pin;

void wait_connection(){
  if(connectionStatus){
    if((millis() - loopTime) > (TIME_TO_CONNECT * 1000))
    {
      connectionStatus = false;
      FastLED.clear();
      FastLED.show();
    }
  }
}

void setup() {
  FastLED.addLeds<WS2812B, CONTROL_LED_PIN, GRB>(leds, NUM_LEDS);   
  brightness = 100;
  FastLED.setMaxPowerInVoltsAndMilliamps(5, MAX_POWER_MILAMP);
  LEDS.setBrightness(brightness);
  Serial.begin(BODSRATE);
  connectionStatus = true;
}

void loop() {  
  if(!connectionStatus) connectionStatus = true;
  loopTime = millis();

  do{
    while(!Serial.available()) wait_connection();
    pin = Serial.read() == 66;
    while(!Serial.available()) wait_connection();
    pin = pin ? Serial.read() == 82 : false;
    while(!Serial.available()) wait_connection();
    pin = pin ? Serial.read() == 73 : false;
  }
  while(!pin);
  
  while(!Serial.available()) wait_connection();
  brightness = Serial.read();
  LEDS.setBrightness(brightness);
  
  for(int i = 0; i < NUM_LEDS; i++)
  {
    while(!Serial.available()) wait_connection();
    leds[i].r = Serial.read();
    while(!Serial.available()) wait_connection();
    leds[i].g = Serial.read();
    while(!Serial.available()) wait_connection();
    leds[i].b = Serial.read();
  }
  FastLED.show();
}
