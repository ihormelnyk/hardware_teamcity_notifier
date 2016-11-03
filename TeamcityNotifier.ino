#include <EtherCard.h>

//Pins:
//2-5 output leds
//6-9, a0 motor
//10-13 ethernet

#define START_LED 2
#define END_LED 5

#define STATUS_IDLE                   0
#define STATUS_NOIP_NEEDS_UPDATE      1
#define STATUS_WAITING_FOR_NOIP       2
#define STATUS_LISTENING              3
#define STATUS_ERROR                  99

static boolean ledState[16]={0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};

//declare variables for the motor pins
int motorPin1 = 6;    // Blue   - 28BYJ48 pin 1
int motorPin2 = 7;    // Pink   - 28BYJ48 pin 2
int motorPin3 = 8;    // Yellow - 28BYJ48 pin 3
int motorPin4 = 9;    // Orange - 28BYJ48 pin 4
                        // Red    - 28BYJ48 pin 5 (VCC)
//motor positioning
int startPosButtonPin = A0;
int currentPosition = -1;
int requiredPosition = 0;

int motorSpeed = 1200;  //variable to set stepper speed
int maxSteps = 512;
int lookup[8] = {B01000, B01100, B00100, B00110, B00010, B00011, B00001, B01001};
///
#define ETHERNET_CS_PIN 10
static byte mymac[] = { 0x88,0x69,0x69,0x2D,0x30,0x31 };

//Hostname and authentication string
char noIP_host[] PROGMEM = "notifier.no-ip.org";
char noIP_auth[] PROGMEM = "bm90aWZpZXI6cGF0aHdpc2U="; //base64 notifier:pathwise
char noIP_web[] PROGMEM = "dynupdate.no-ip.com";

// Global variables
byte noIP_address[4];
byte actual_status;
static byte session_id;

#define BUF_SIZE 600
byte Ethernet::buffer[BUF_SIZE]; // tcp/ip send and receive buffer
BufferFiller bfill; //16b
char dataBuf[30];  //reduce size!

// store html header in flash to save memory
char httpHeader[] PROGMEM = 
"HTTP/1.1 200 OK\r\n"
"Content-Type: text/html;\r\n"
"Connection: close\r\n"
"Cache-Control: no-cache\r\n"
"\r\n" 
;

char httpErrorHeader[] PROGMEM = 
"HTTP/1.1 404 Not Found\r\n"
"Content-Type: text/html;\r\n"
"Connection: close\r\n"
"Cache-Control: no-cache\r\n"
"\r\n" 
;

int freeRam () {
  extern int __heap_start, *__brkval; 
  int v; 
  return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval); 
}

void setup () {
  Serial.begin(57600);
  Serial.println(F("Initialising the Ethernet controller"));
  Serial.println(F("Free RAM:"));    
  Serial.println(freeRam()); 
  //1841b

  if (!ether.begin(sizeof Ethernet::buffer, mymac, ETHERNET_CS_PIN))
    Serial.println(F("Failed to access Ethernet controller"));
  else
    Serial.println(F("Ethernet controller initialized"));        

  Serial.println(F("Attempting to get an IP address using DHCP"));
  if (!ether.dhcpSetup())
    Serial.println(F("Failed to get configuration from DHCP"));
  else
    Serial.println(F("DHCP configuration done"));

  ether.printIp("IP:\t", ether.myip);
  //ether.printIp("Gateway:\t", ether.gwip);
  //ether.printIp("DNS:\t", ether.dnsip);
  Serial.println(); 

  actual_status = STATUS_LISTENING;
#if UPDATEIP  
  actual_status = STATUS_NOIP_NEEDS_UPDATE;
#endif  

  Serial.println(F("Main loop..."));
  
  for (byte pin=START_LED; pin<=END_LED; pin++)
  {
      pinMode(pin, OUTPUT);    
      digitalWrite(pin, LOW); 
  }  
  //declare the motor pins as outputs
  pinMode(motorPin1, OUTPUT);
  pinMode(motorPin2, OUTPUT);
  pinMode(motorPin3, OUTPUT);
  pinMode(motorPin4, OUTPUT);
  
  setPosition(0);  //init motor position
}

byte ledCycleCombination[] = {0,1,2,3,4,5,6,7,15,14,13,12,11,10,9,8};
byte ledCombinationPos = 0;
long ledTime;
byte currentFailLed = -1;
boolean currentFailLedState = 0;

void ledLoop()
{
  long t = millis();
  boolean any = false; 
  for (byte i=0; i<16; i++)
  {
    if (ledState[i])
    {
      any = true;
      if (currentFailLed==i)
      {
        if (!currentFailLedState)
        {
          writeLed(i);
          delayMicroseconds(2000);
        }
        if ((t - ledTime)>1000)
        {
          ledTime = t;
          currentFailLedState=!currentFailLedState;   
        }
      }
      else
      {
         writeLed(i);
        delayMicroseconds(2000);
      }      
    }
  }
  if (!any)
  {
    if ((t - ledTime)>100)
    {
      ledTime = t;
      if (ledCombinationPos >= sizeof ledCycleCombination)
      {
        ledCombinationPos = 0;
      }     
      writeLed(ledCycleCombination[ledCombinationPos++]);
    }
  }
}

void writeLed(int val)
{  
  digitalWrite(2, (val&1) ? HIGH : LOW);
  digitalWrite(3, (val&2) ? HIGH : LOW);
  digitalWrite(4, (val&4) ? HIGH : LOW);
  digitalWrite(5, (val&8) ? HIGH : LOW);
}


///////////// motor methods ///////////////////
int stepsToInit = 0;
void stepperLoop()
{
  if (currentPosition < 0)
  {    
    if ((analogRead(startPosButtonPin)>512) && stepsToInit > 100)
    {
      currentPosition = 0;
      stepsToInit = 0;
    }
    else
    {
      anticlockwise();
      stepsToInit++;
    }
  }
  else
  {
    if (currentPosition < requiredPosition)
    {
      clockwise();
      currentPosition++;
    }
    else if (currentPosition > requiredPosition)
    {
      anticlockwise();
      currentPosition--;
    }    
  }  
}

void setPosition(int pos)
{
  if (pos < 0) pos = 0;
  if (pos > maxSteps) pos = maxSteps;
  requiredPosition = pos;  
}

//////////////////////////////////////////////////////////////////////////////
//set pins to ULN2003 high in sequence from 1 to 4
//delay "motorSpeed" between each pin setting (to determine speed)
void clockwise()
{
  for(int i = 0; i < 8; i++)
  {
    setOutput(i);
    delayMicroseconds(motorSpeed);
  }
}

void anticlockwise()
{
  for(int i = 7; i >= 0; i--)
  {
    setOutput(i);
    delayMicroseconds(motorSpeed);
  }
}

void setOutput(int out)
{
  digitalWrite(motorPin1, bitRead(lookup[out], 0));
  digitalWrite(motorPin2, bitRead(lookup[out], 1));
  digitalWrite(motorPin3, bitRead(lookup[out], 2));
  digitalWrite(motorPin4, bitRead(lookup[out], 3));
}
//////// end motor methods ////////////////

static word okPage() {  
  bfill = ether.tcpOffset();
  bfill.emit_p( PSTR ("$F$S"), httpHeader, "Ok");
  return bfill.position();
}

static word errorPage() {
  bfill = ether.tcpOffset();  
  bfill.emit_p( PSTR ("$F$S"), httpErrorHeader, "Error");
  return bfill.position();
}

#if UPDATEIP

void updateNoIP() {
  Serial.println(F("No-ip DNS lookup..."));
  if (!ether.dnsLookup(noIP_web)) {
    Serial.println(F("Unable to resolve IP for no-ip"));
    actual_status = STATUS_ERROR;
  } else {
    ether.copyIp(noIP_address, ether.hisip);
    ether.printIp(" resolved to:\t", ether.hisip);    
  } 
  ////
  
  Serial.println(F("Updating NoIP..."));
  Stash::prepare(PSTR("GET /nic/update?hostname=$F&myip=$D.$D.$D.$D HTTP/1.0" "\r\n"
    "Host: $F" "\r\n"
    "Authorization: Basic $F" "\r\n"
    "User-Agent: NoIP_Client" "\r\n" "\r\n"),
  noIP_host, ether.myip[0], ether.myip[1], ether.myip[2], ether.myip[3], noIP_web, noIP_auth);
  ether.copyIp(ether.hisip, noIP_address);
  session_id = ether.tcpSend();
  
  // Wait for response or timeout...
  actual_status = STATUS_WAITING_FOR_NOIP;   
}

void checkNoIPResponse() {
  const char* reply = ether.tcpReply(session_id);
  boolean done;

  if(reply != 0) {
    if(strstr(reply, "good") != 0) {
      Serial.println(F(" done!"));
      done = true;
    } 
    else if(strstr(reply, "nochg") != 0) {
      Serial.println(F(" no change required!"));
      done = true;
    }    
    else Serial.println(reply);    

    if(done) {   
      actual_status = STATUS_LISTENING;
    }
  }  
}

#endif

void switchConfiguration(const char* data, byte state)
{
  if (ether.findKeyVal(data+5, dataBuf , sizeof dataBuf , "conf") > 0) {
      byte conf = atoi(dataBuf);
      if (conf>=0 && conf<=7)
      {
        Serial.print(conf, DEC);
        Serial.println(F(" conf received"));
        if (state)
        {
          ledState[conf] = 1; 
          ledState[conf+8] = 0;        
        }
        else
        {
          ledState[conf] = 0; 
          ledState[conf+8] = 1;
          currentFailLed = conf+8;
          currentFailLedState = 0;
        }
        return ether.httpServerReply(okPage());        
      }
    }
    Serial.println(F("received but conf not specified"));
    ether.httpServerReply(errorPage());

}

void setMotorSteps(const char* data)
{
  if (ether.findKeyVal(data+5, dataBuf , sizeof dataBuf , "steps") > 0) {
      int steps = atoi(dataBuf);
      Serial.print(steps, DEC);
      Serial.println(F(" steps"));
      setPosition(steps);
    }  
}

void listenForRequests(word pos)
{ 
   if (pos) { //id data is received  
    char* data = (char *) ether.tcpOffset();    
    
    #if DEBUG       // display incoming data    
      Serial.println(F("-------- Data received --------"));
      Serial.println(data);
      Serial.println(F("-------------------------------"));
    #endif  
     
    // "on" command received     
    if (strncmp( "GET /pass" , data , 9 ) == 0) {
      setMotorSteps(data);
      Serial.print(F("ON "));
      switchConfiguration(data, HIGH);    
    }        
    // "off" command received     
    else if (strncmp( "GET /fail" , data , 9 ) == 0) {  
      setMotorSteps(data);    
      Serial.print(F("OFF "));
      switchConfiguration(data, LOW);    
    }
    else if (strncmp( "GET /reinitMotor" , data , 15 ) == 0) {
      currentPosition = -1;
      ether.httpServerReply(okPage());  
    }
    else if (strncmp( "GET /reset" , data , 10 ) == 0) {
      for (byte i=0;i<16;i++) ledState[i]=0;
      currentPosition = -1;
      setPosition(0);
      ether.httpServerReply(okPage());  
    }   
    else {         
      ether.httpServerReply(errorPage());
    }    
  } 
}

void loop(){
  ledLoop();
  stepperLoop(); //motor loop
  // check if anything has come in via ethernet
  word len = ether.packetReceive();
  word pos = ether.packetLoop(len);  

  switch(actual_status) {
#if UPDATEIP    
    case STATUS_NOIP_NEEDS_UPDATE: updateNoIP(); break;
    case STATUS_WAITING_FOR_NOIP: checkNoIPResponse(); break;
#endif    
    case STATUS_LISTENING: listenForRequests(pos); break;
  }
} 
