Adds extended rig support compatible with VRChat avatars and specific versions of the SlimeVR server.

## Installation

[![Add to VCC](https://img.shields.io/badge/Add_to-VCC-blue?logo=vrchat&logoColor=white&style=for-the-badge)](https://sebane1.github.io/VRCSlimeVRExtendedRigSupport/index.html)

*(If the button above does not work in your browser, follow the manual steps below)*

1. Open the **VRChat Creator Companion**.
2. Navigate to **Settings** -> **Packages** -> **Add Repository**.
3. Enter the repository URL for this plugin: `https://raw.githubusercontent.com/Sebane1/VRCSlimeVRExtendedRigSupport/main/index.json`
4. Once added, open your Avatar project in VCC.
5. Find "SlimeVR Toe Rig Support" in the package list and click the **(+)** button to install it.

## Setup Instructions

1. Open the tool by going to **`Tools` -> `Toe Rig` -> `Add Toe Tracking Compatibility`** in the top menu.
2. Wait for the configuration window to appear.
3. **Assign Files:**
   - Find your avatar's **VRC Expression Parameters** and drag the file into the appropriate slot.
   - Find your avatar's **Animator Controller** and drag the file into its slot.
5. **Assign Bones:** Assign bones Bones from your avatar's rig into the relevant slots. 
   - *Note:* Use as many bones as your rig supports.
6. Click the relevant generate button.
7. Your avatar should now have extended tracking support with compatible versions of SlimeVR!

## Optional: OSCSmooth

You may wish to use an additional plugin called [OSCSmooth](https://github.com/regzo2/OSCmooth) to make sure the toes look smooth to other people over the network. 

- If you are using OSCSmooth, check the **"Uses OSC Smooth"** box before hitting generate.
- You will have to run the OSCSmooth plugin **AFTER** running the initial generation from this tool.
