---
name: debug-info-collector
description: Collects debugging information from the Docked AI application using Windows App CLI (winappcli) tools. Captures debug output, UI inspection data, screenshots, and window information for troubleshooting.
keywords: [debugging, winappcli, diagnostics, ui-inspection, screenshot, window-info, troubleshooting]
compatibility: [Windows App CLI 0.3+, .NET 10.0, WinUI 3, Windows 10/11]
---

# Debug Info Collector Skill

Specialized skill for collecting comprehensive debugging information from the Docked AI application using Windows App CLI (winappcli) tools.

## USE FOR

- Capturing application debug output and exceptions
- Inspecting UI element hierarchy and properties
- Taking screenshots of application windows
- Listing and monitoring application windows
- Collecting diagnostic information for troubleshooting
- Analyzing runtime behavior and performance issues

## DO NOT USE FOR

- Code analysis or static code review
- Performance profiling (use dedicated profiling tools)
- Memory leak detection (use memory profilers)
- Network debugging
- Database query debugging

## Prerequisites

Ensure Windows App CLI is installed:
```bash
# Check if winappcli is available
winapp --version

# If not installed, install via WinGet
winget install Microsoft.WinAppCli

# Or via npm
npm install -g @microsoft/winappcli
```

## Core Commands

### 1. Run Application with Debug Output

Capture real-time debug output and exceptions:

```bash
# Build the project first
dotnet build "Docked AI.csproj" -c Debug /p:Platform=x64

# Run with debug output capture
winapp run .\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --debug-output
```

**Output includes:**
- Console output and logs
- Exception stack traces
- Debug.WriteLine messages
- Application lifecycle events

### 2. List Application Windows

Get information about all windows belonging to the application:

```bash
winapp ui list-windows -app "Docked AI"
```

**Returns:**
- Window titles
- Window handles (HWND)
- Window dimensions and position
- Visibility state
- Window class names

### 3. Inspect UI Tree

Examine the complete UI element hierarchy:

```bash
# Basic inspection
winapp ui inspect -app "Docked AI"

# Save to file for detailed analysis
winapp ui inspect -app "Docked AI" --output ui-tree.json

# Inspect specific window by title
winapp ui inspect -window "MainWindow Title"
```

**Provides:**
- Complete UI element tree structure
- Element types and names
- Automation properties
- Control patterns supported
- Parent-child relationships

### 4. Capture Screenshots

Take screenshots of application windows for visual debugging:

```bash
# Screenshot of main application window
winapp ui screenshot -app "Docked AI" -output screenshot.png

# Screenshot with timestamp
winapp ui screenshot -app "Docked AI" -output "screenshot_$(Get-Date -Format 'yyyyMMdd_HHmmss').png"

# Screenshot specific window
winapp ui screenshot -window "Settings Window" -output settings-screenshot.png
```

**Useful for:**
- Visual bug reporting
- UI layout verification
- Documenting issues
- Before/after comparisons

### 5. Monitor Application Events

Track application lifecycle and user interactions:

```bash
# Monitor window events
winapp ui monitor -app "Docked AI" --events window

# Monitor focus changes
winapp ui monitor -app "Docked AI" --events focus

# Comprehensive monitoring
winapp ui monitor -app "Docked AI" --all-events
```

## Common Debugging Scenarios

### Scenario 1: Application Crashes on Startup

```bash
# Step 1: Run with debug output to capture crash details
dotnet build "Docked AI.csproj" -c Debug /p:Platform=x64
winapp run .\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64 --debug-output > crash-log.txt 2>&1

# Step 2: Check if any windows were created before crash
winapp ui list-windows -app "Docked AI"

# Step 3: Review crash-log.txt for exception details
```

### Scenario 2: UI Element Not Responding

```bash
# Step 1: Inspect UI tree to verify element exists
winapp ui inspect -app "Docked AI" --output ui-inspection.json

# Step 2: Take screenshot to see current UI state
winapp ui screenshot -app "Docked AI" -output ui-state.png

# Step 3: List windows to check visibility
winapp ui list-windows -app "Docked AI"
```

### Scenario 3: Layout or Rendering Issues

```bash
# Step 1: Capture screenshot of problematic area
winapp ui screenshot -app "Docked AI" -output layout-issue.png

# Step 2: Inspect UI tree to check element properties
winapp ui inspect -app "Docked AI" | Select-String -Pattern "Width|Height|Margin|Padding"

# Step 3: Monitor window resize events
winapp ui monitor -app "Docked AI" --events resize
```

### Scenario 4: Navigation Problems

```bash
# Step 1: List all windows to see navigation state
winapp ui list-windows -app "Docked AI"

# Step 2: Inspect UI to check current page/frame
winapp ui inspect -app "Docked AI" | Select-String -Pattern "Frame|Page|NavigationView"

# Step 3: Monitor navigation events
winapp ui monitor -app "Docked AI" --events navigation
```

## Advanced Techniques

### Combining Multiple Commands

Create a comprehensive debug report:

```powershell
# PowerShell script to collect all debug info
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$outputDir = ".\debug-info-$timestamp"
New-Item -ItemType Directory -Path $outputDir -Force

Write-Host "Collecting debug information..."

# 1. List windows
Write-Host "Listing windows..."
winapp ui list-windows -app "Docked AI" | Out-File "$outputDir\windows.txt"

# 2. Inspect UI tree
Write-Host "Inspecting UI tree..."
winapp ui inspect -app "Docked AI" --output "$outputDir\ui-tree.json"

# 3. Take screenshot
Write-Host "Taking screenshot..."
winapp ui screenshot -app "Docked AI" -output "$outputDir\screenshot.png"

Write-Host "Debug information collected in: $outputDir"
```

### Filtering UI Inspection Results

```bash
# Find specific control types
winapp ui inspect -app "Docked AI" | Select-String -Pattern "Button|TextBlock"

# Find elements with specific automation IDs
winapp ui inspect -app "Docked AI" | Select-String -Pattern "AutomationId"

# Search for error states
winapp ui inspect -app "Docked AI" | Select-String -Pattern "Error|Invalid|Disabled"
```

### Automated Monitoring

```bash
# Monitor for 30 seconds and save output
winapp ui monitor -app "Docked AI" --all-events --duration 30 --output monitor-log.txt
```

## Best Practices

1. **Always Build First**: Ensure you're testing the latest code by building before running debug commands
2. **Use Debug Configuration**: Always use `-c Debug` when building for debugging
3. **Timestamp Outputs**: Add timestamps to output files to track when data was collected
4. **Combine Evidence**: Use multiple commands together for comprehensive debugging
5. **Save Everything**: Keep all debug outputs for comparison and historical tracking
6. **Check Platform**: Ensure platform matches your build (x64, x86, ARM64)
7. **Run as Administrator**: Some operations may require elevated privileges

## Troubleshooting winappcli Issues

### Command Not Found
```bash
# Verify installation
where winapp

# Reinstall if needed
winget reinstall Microsoft.WinAppCli
```

### Application Not Detected
```bash
# List all running apps to find exact name
winapp ui list-windows

# Try using process name instead
winapp ui list-windows -process "Docked AI.exe"
```

### Permission Errors
```bash
# Run PowerShell/Command Prompt as Administrator
# Or adjust UAC settings
```

## Output Interpretation

### UI Tree Structure
```json
{
  "type": "Window",
  "name": "Main Window",
  "children": [
    {
      "type": "NavigationView",
      "name": "NavView",
      "automationId": "MainNavView",
      "children": [...]
    }
  ]
}
```

**Key fields to examine:**
- `type`: Control type (Button, TextBlock, etc.)
- `name`: Display name or content
- `automationId`: Unique identifier for automation
- `bounds`: Position and size
- `isEnabled`: Whether control is interactive
- `isVisible`: Whether control is visible

### Window Information
```
Title: Docked AI
Handle: 0x00123ABC
Position: (100, 100)
Size: 1200x800
Visible: True
Topmost: False
```

## Integration with Issue Reporting

When reporting bugs, include:

1. **Debug Output Log**: From `--debug-output` flag
2. **UI Inspection**: JSON output from `inspect` command
3. **Screenshot**: Visual evidence of the issue
4. **Window List**: State of all application windows
5. **Steps to Reproduce**: Clear reproduction steps
6. **Expected vs Actual**: What should happen vs what happened

Example bug report attachment structure:
```
bug-report-2024/
├── debug-output.log
├── ui-tree.json
├── screenshot-before.png
├── screenshot-after.png
├── windows-list.txt
└── reproduction-steps.md
```

## Resources

- [Windows App CLI Documentation](https://github.com/microsoft/winappCli)
- [WinUI 3 Debugging Guide](https://learn.microsoft.com/windows/apps/winui/winui3/debugging)
- [UI Automation Overview](https://learn.microsoft.com/windows/win32/winauto/ui-automation-overview)
- [Project README](../README.md) - For project-specific commands

---

**Remember:** This skill is specifically for collecting debugging information using winappcli. For code-level debugging, use Visual Studio debugger or other IDE debugging tools. Always ensure the application is built in Debug configuration before collecting debug information.
