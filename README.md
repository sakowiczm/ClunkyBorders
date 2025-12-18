# ClunkyBorders

Todo:
- AOT - use dotnet publish -c Release ~1.5MB
- App should have only one instance
- Log to file
- Add rounded corners to the border
- Introduce gap
- Performance - when window class is excluded - we don't need other calls
- How can handle elevate windows?
- Exclude pintscreen app
     Class Name: XamlWindow
     Text: Snipping Tool Overlay
- Border is drawn over window task bar - z-order issue
- child window issue - clicking pop-up border is hidden from parent window
   what about - get child window parent window HWND and compare with HWND of window currenlty have border - if the same keep border?