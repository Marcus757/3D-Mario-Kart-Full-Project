# 3D Mario Kart - Control Mappings

## New Input System Configuration

This project now uses Unity's New Input System with full keyboard and gamepad controller support!

---

## 🎮 **Controller Mappings**

### **Xbox / PlayStation / Generic Gamepad**

| Action | Xbox Controller | PlayStation Controller |
|--------|----------------|----------------------|
| **Accelerate** | A Button (RT/ZR supported) | Cross (×) / R2 |
| **Brake/Reverse** | B Button | Circle (○) |
| **Steer** | Left Stick (Horizontal) | Left Stick (Horizontal) |
| **Drift/Trick** | R / ZR | R1 / R2 |
| **Use Item (Forward/Backward)** | L / ZL (Hold stick down for backward) | L1 / L2 |
| **Look Back** | X Button | Square (□) |
| **Camera: First Person** | D-Pad Up | D-Pad Up |
| **Camera: Regular** | D-Pad Down | D-Pad Down |
| **Glider Up** | Left Stick Up | Left Stick Up |
| **Glider Down** | Left Stick Down | Left Stick Down |

---

## ⌨️ **Keyboard Mappings**

| Action | Key |
|--------|-----|
| **Accelerate** | W |
| **Brake/Reverse** | S |
| **Steer Left** | A |
| **Steer Right** | D |
| **Drift/Trick** | Space |
| **Use Item (Forward/Backward)** | Left Shift (hold Down Arrow to throw backward) |
| **Look Back** | Right Shift |
| **Camera: First Person** | 1 |
| **Camera: Regular** | 2 |
| **Glider Up** | W |
| **Glider Down** | S |
| **Screenshot** | F9 |

---

## 🔧 **Technical Details**

### Input System Files

- **Input Actions Asset**: `Assets/Settings/GameControls.inputactions`
- **Generated C# Class**: `Assets/Scripts/GameControls.cs`
- **Package Version**: Unity Input System 1.4.4

### Modified Scripts

The following scripts have been updated to use the new Input System:

- `Player.cs` - Main player controller
- `RACE_MANAGER.cs` - Camera controls
- `ItemManager.cs` - Item usage
- `UtilityFunctions.cs` - Utility functions

### Control Schemes

Two control schemes are configured:

1. **Keyboard&Mouse** - For keyboard players
2. **Gamepad** - For controller players (Xbox, PlayStation, Switch Pro, etc.)

The Input System automatically switches between control schemes based on the last input device used.

---

## 🎯 **Gameplay Tips**

### **Drifting**

1. Hold **Space** (or **A/Cross** on controller) while turning
2. The longer you drift, the bigger the boost:
   - **Blue sparks** = Small boost
   - **Orange sparks** = Medium boost  
   - **Pink sparks** = Large boost
3. Release **Space** (or **A/Cross**) to activate the boost

### **Tricks**

- Press **Space** (or **A/Cross**) while in the air to perform tricks
- Successfully landing tricks gives you a speed boost
- Works best when jumping off ramps at high speed

### **Items**

- **Left Shift** (or **L/ZL**) - Fire items forward (shells, bob-ombs)
- **Hold Down Arrow / Left Stick Down + Item Button** - Throw or drop items behind you

### **Starting Boost**

- Hold **W** (or **RT/R2**) just before the race starts
- Perfect timing gives you a boost at the start!

---

## 🔄 **Switching Between Input Methods**

The game seamlessly switches between keyboard and controller input. Simply use either input method and the game will automatically detect and respond to it.

**Note**: Both the old and new input systems are currently active for compatibility. The old Input Manager is still present but the new Input System takes priority.

---

## 📝 **Customizing Controls**

To customize controls:

1. Open `Assets/Settings/GameControls.inputactions` in Unity
2. Modify the bindings as needed
3. Save the asset
4. Unity will automatically regenerate the C# class

Alternatively, you can regenerate the C# class manually:
- Select the `GameControls.inputactions` asset
- In the Inspector, click **Generate C# Class**

---

## 🐛 **Troubleshooting**

### Controller Not Working?

1. Make sure your controller is connected before starting the game
2. Check that Unity's Input System package is installed (v1.4.4)
3. Verify `activeInputHandler` is set to "Both" in Project Settings

### Inputs Not Responding?

- Check that the `GameControls` instance is being created in `Awake()`
- Verify the input actions are enabled in `OnEnable()`
- Make sure the scripts are properly attached to GameObjects in the scene

---

**Enjoy racing! 🏁🎮**


