# ClunkyBorders - Rules & Testing

## Rules

### Core Functionality
- Application draws a colored border overlay around the currently focused/active window
- Only one instance of the application can run at a time
- Application runs in system tray in the background

### Border Display Rules
- Border is shown ONLY when window meets ALL criteria:
  - Window state is Normal (not minimized, maximized, or hidden)
  - Window is a parent/root window (not a child control)
  - Window has valid non-empty rectangle dimensions
  - Window class name is NOT in the exclusion list (config)

- Border is hidden when:
  - Window is minimized
  - Window is maximized
  - Window is destroyed or hidden
  - Window is excluded by class name
  - Window loses focus and new window can't have border (e.g elevated privileges)

### Border Rendering
- Border width, offset, and color are configurable (config.toml)
- Border scales with window DPI (high DPI aware)
- **Offset** controls border position:
  - Positive offset: Border drawn outside window boundary (e.g., offset=6, width=3 creates 3px gap + 3px border)
  - Zero offset: Border drawn at window edge
  - Negative offset: Border drawn inside window boundary (e.g., offset=-3, width=3 creates 3px gap + 3px border inside)

### Window Tracking
- Monitors two main events:
  - `EVENT_SYSTEM_FOREGROUND`: New window gets focus
  - `EVENT_OBJECT_LOCATIONCHANGE`: Window position/size changes
  - `EVENT_OBJECT_DESTROY`: Window is destroyed
  - `EVENT_OBJECT_HIDE`: Window is hidden
  - `EVENT_SYSTEM_MINIMIZESTART`: Window is minimized

## Manual Testing

### Border Display
- Open Notepad ? Verify border appears around window
- Click on desktop ? Verify border disappears
- Switch between multiple windows ? Verify border follows active window

### Window State Changes
- Minimize active window ? Verify border disappears
- Restore minimized window ? Verify border reappears
- Maximize window ? Verify border disappears
- Restore from maximized ? Verify border reappears
- Close single window on the desktop ? Verify border disappears

### Window Movement (Drag)
- Drag window slowly ? Verify border follows smoothly
- Drag window quickly ? Verify border tracks accurately
- Move window to second monitor (if available) ? Verify border follows
- Drag window partially off-screen ? Verify border behavior

### Window Resizing
- Resize window from corner ? Verify border adjusts immediately
- Resize window from edge ? Verify border adjusts immediately
- Resize to very small dimensions ? Verify border remains visible/proper
- Resize to very large dimensions ? Verify border scales correctly

### Multiple Applications
- Switch between different apps using mouse 
- Rapid Alt+Tab between apps ? Verify border transitions correctly
- Minimize to desktop Win+D ? Verify border disappears / appears correctly

### Edge Cases
- Open popup dialogs ? Verify border behavior
- Open context menus ? Verify border doesn't interfere
- Open system dialogs (Save/Open) ? Verify appropriate handling
- Lock workstation and unlock ? Verify application recovers
- Switch to fullscreen application ? Verify border hidden
- Delay when window is not ready (e.g when cloaked - opening window window with WIN+E) ? Verify border is not displayed for long time without active window

### Application Lifecycle
- Exit application ? Verify clean shutdown
- Check logs after session ? Verify no critical errors
- Restart after crash (simulate) ? Verify recovery

### Excluded Windows
- Configure exclusion list in config.toml
- Focus on excluded window ? Verify no border appears
- Switch from excluded to non-excluded window ? Verify border appears

## Todo

- Offset - document & test when not using window manager that introduce gap
- Command line parameter to point to different config file location
- Z-order issue - border is drawn over window task bar vs. border color changes
- Add About window

