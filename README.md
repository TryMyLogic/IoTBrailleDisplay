# IoTBrailleDisplay

Part of a university capstone project aimed at designing and prototyping an IoT-enabled, 3D-printed Braille display to enable visually impaired users to access and control smart home devices via tactile feedback. The software component acts as the intermediary between a server hosting Home Assistant, an MQTT publisher (mosquito), and the physical Braille display, which lacks sufficient processing power for full smart home control. By integrating refreshable Braille cells with smart home systems and using low-cost 3D printing and microcontrollers, the project promotes digital independence, inclusion, and accessibility in under-resourced contexts.

## Problem statement:
There is currently no affordable, tactile alternative for smart home control that is suitable for visually impaired individuals.
Existing refreshable Braille displays are expensive and are rarely designed to interface with smart home systems. 
This creates a significant accessibility gap, as blind users are unable to independently interact with their smart environments. 
These current systems fall short because of the following reasons:
* Commercial Braille displays are extremely expensive
* Many blind users rely on sighted assistance or audio
    interfaces, which tends to compromise independence,
    especially in noisy or private environments.

## Solution Overview:
Our solution is a 3D-printed, IoT-enabled Braille display
designed to make smart home interaction accessible to
visually impaired users. The system bridges the gap
between tactile communication and smart technology by
translating digital text and smart home data into real-time
Braille output using solenoid-driven pins.

The device is powered by an ESP32 microcontroller and
Arduino UNO, which handle sensor input and actuator
control. It communicates wirelessly with a .NET MAUI
application via  Bluetooth. This allows users to receive
tactile feedback from IoT systems in a format they can
physically read and respond to.

Our approach combines affordability and open-source
integration, ensuring the design is usable for various
accessibility use cases in any environment.

## Outcomes
* A fully functional .NET MAUI companion app that connects to the Braille display and smart devices.
* A partially functional IoT control system written in C++, capable of translating digital input into Braille actuator commands.
* All hardware components have been acquired and assembled, ready for integration and testing.

However, we encountered 3 major setbacks:
1. Late hardware arrival, less than one month before final submission, which restricted our ability to fully test and calibrate the solenoid array and C++ code and integrate with the MAUI application.
2. A supply error, where we received an Arduino UNO instead of the requested Arduino Leonardo which eliminated our plans for user input on the display and limited available power output.
3. A 3D printer failure at the university, which prevented us from producing the final display chassis with accurate cell dimensions.
