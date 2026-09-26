# Wiring the Cherry switches

Disconnect USB power before wiring. These are dry-contact inputs: do not connect switches to 5 V or VIN. Each switch connects one GPIO to a shared ESP32 ground when pressed. Internal pull-ups are enabled in firmware, so no external resistors are required for short wires.

| Function | ESP32 header label | Switch terminal 1 | Switch terminal 2 |
|---|---|---|---|
| Play/pause | GPIO25 / 25 / D25 | GPIO25 | GND |
| Next | GPIO26 / 26 / D26 | GPIO26 | GND |
| Previous | GPIO27 / 27 / D27 | GPIO27 | GND |
| Volume up | GPIO32 / 32 / D32 | GPIO32 | GND |
| Volume down | GPIO33 / 33 / D33 | GPIO33 | GND |

```text
ESP32                             Cherry switches (normally open)

GPIO25 ───────────────────────────o  o─────┐  Play/pause
GPIO26 ───────────────────────────o  o─────┤  Next
GPIO27 ───────────────────────────o  o─────┤  Previous
GPIO32 ───────────────────────────o  o─────┤  Volume up
GPIO33 ───────────────────────────o  o─────┤  Volume down
                                         │
GND ─────────────────────────────────────┘  Shared ground

USB-C → board power and initial programming
```

Use the **GPIO numbers printed on your board**, not physical header positions; ELEGOO board layouts may vary. The diagram is electrical, not a physical pinout. On a Cherry MX-style switch use its two electrical metal terminals; plastic locating posts and any LED terminals are not switch contacts. Confirm continuity only when pressed if terminal identity is unclear.

The selected pins avoid boot-strapping GPIO0/2/5/12/15, flash GPIO6–11, UART0 GPIO1/3, and GPIO34–39, which lack internal pull-ups. No keyboard matrix, diodes, USB keyboard, or keyboard protocol is involved. Use short leads; external pull-ups and filtering may be needed for long/noisy wiring.

Named button pins, debounce, volume step, and repeat timing live in `NanoDevice/DeviceOptions.cs`. All five pins must be unique. The finished device needs no display or additional LED.
