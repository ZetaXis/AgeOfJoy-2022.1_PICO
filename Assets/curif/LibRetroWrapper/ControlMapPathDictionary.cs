using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// this class maps the quest controls to unity paths and behaviors
public static class ControlMapPathDictionary
{
  static Dictionary<string, string> map; 

  static ControlMapPathDictionary() {
    /*
    * secondaryButton [LeftHand XR Controller] = Y button
       primaryButton [LeftHand XR Controller] = X button
       secondaryButton [RightHand XR Controller] = B button
       primaryButton [RightHand XR Controller] = A buttonsecondaryButton [LeftHand XR Controller] = Y button
       primaryButton [LeftHand XR Controller] = X button
       secondaryButton [RightHand XR Controller] = B button
       primaryButton [RightHand XR Controller] = A button
   */
    map = new Dictionary<string, string>
    {
        // Left VR controller mappings
        { "quest-x", "<XRController>{LeftHand}/primaryButton" }, //primaryButton
        { "quest-y", "<XRController>{LeftHand}/secondaryButton" },
        { "quest-start", "<XRController>{LeftHand}/menuButton" },
        { "quest-left-grip", "<XRController>{LeftHand}/gripButton" },
        { "quest-left-trigger", "<XRController>{LeftHand}/triggerButton" },
        { "quest-left-thumbstick", "<XRController>{LeftHand}/Primary2DAxis" },
        { "quest-left-thumbstick-press", "<XRController>{LeftHand}/Primary2DAxisClick" },
        
        // Right VR controller mappings
        { "quest-a", "<XRController>{RightHand}/primaryButton" },
        { "quest-b", "<XRController>{RightHand}/secondaryButton" },
        { "quest-select", "<XRController>{RightHand}/menuButton" },
        { "quest-right-grip", "<XRController>{RightHand}/gripButton" },
        { "quest-right-trigger", "<XRController>{RightHand}/triggerButton" },
        { "quest-right-thumbstick", "<XRController>{RightHand}/Primary2DAxis" },
        { "quest-right-thumbstick-press", "<XRController>{RightHand}/Primary2DAxisClick" },

        //Gamepad mappings
        { "gamepad-a", "<Gamepad>/buttonSouth" },
        { "gamepad-b", "<Gamepad>/buttonEast" },
        { "gamepad-x", "<Gamepad>/buttonWest" },
        { "gamepad-y", "<Gamepad>/buttonNorth" },
        { "gamepad-select", "<Gamepad>/select" },
        { "gamepad-start", "<Gamepad>/start" },
        { "gamepad-left-bumper", "<Gamepad>/leftShoulder" },
        { "gamepad-right-bumper", "<Gamepad>/rightShoulder" },
        { "gamepad-left-trigger", "<Gamepad>/leftTrigger" },
        { "gamepad-right-trigger", "<Gamepad>/rightTrigger" },
        { "gamepad-left-thumbstick-down", "<Gamepad>/leftStick/down" },
        { "gamepad-left-thumbstick-up", "<Gamepad>/leftStick/up" },
        { "gamepad-left-thumbstick-left", "<Gamepad>/leftStick/left" },
        { "gamepad-left-thumbstick-right", "<Gamepad>/leftStick/right" },
        { "gamepad-left-thumbstick-press", "<Gamepad>/leftStickPress" },
        { "gamepad-right-thumbstick-down", "<Gamepad>/rightStick/down" },
        { "gamepad-right-thumbstick-up", "<Gamepad>/rightStick/up" },
        { "gamepad-right-thumbstick-left", "<Gamepad>/rightStick/left" },
        { "gamepad-right-thumbstick-right", "<Gamepad>/rightStick/right" },
        { "gamepad-right-thumbstick-press", "<Gamepad>/rightStickPress" }
    };
}

  public static string GetInputPath(string realControl) {
    if (map.ContainsKey(realControl) ) {
      return map[realControl];
    }
    else {
      ConfigManager.WriteConsoleError("[ControlMapPathDictionary] Control not found in control map: " + realControl);
      return "";
    }
  }

}
