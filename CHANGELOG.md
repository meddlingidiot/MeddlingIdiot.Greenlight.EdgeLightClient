# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- First cut: a strip one to five pixels deep along one edge of the primary monitor, coloured
  from the Greenlight running on the machine. Green while everything passes, amber while a pull
  request wants you, red when a pipeline breaks, and a cold grey when there is no Greenlight to
  ask. Any of the four edges.
- A build pulse with a depth of your choosing - from not at all to all the way out and back -
  eased in when the build starts and eased back to steady when it ends.
- A hazard light for red: an old rotating beacon that slides out from under the strip, turns
  for as long as the pipeline is broken, and slides back in when it is fixed. Where along the
  edge it comes in and how big it is are both settings, and it can be turned off altogether.
  Drawn once for the top edge and rotated into place for the other three.
- A changeover. A new colour fades the old one all the way out first rather than blending where
  it stands, so the strip never spends a few frames being a colour that means nothing. The
  light waits for the red to actually be showing before it comes out.
- Two click-through, never-activated, out-of-Alt+Tab windows: the strip, and a square for the
  light that exists only while the light is out. Both repaint only when something would look
  different.
- The one Win32 trick the strip needs: Windows holds an ordinary window to about 136×39 pixels
  whatever it answers to `WM_GETMINMAXINFO`, so the strip is made a popup window after it exists.
- A tray menu for everything - edge, thickness, opacity, pulse depth, the light's size and
  entry point, whether Greenlight being away shows grey or nothing - each written straight back
  to `edgelight.json`.
- "Start with Windows" in the tray menu, registering the stable shim beside the install rather
  than the versioned copy an update would move.
