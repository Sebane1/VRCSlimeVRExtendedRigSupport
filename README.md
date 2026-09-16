Adds extended rig support compatible with VRChat avatars and specific versions of the SlimeVR server.

## Installation

[![Add to VCC](https://img.shields.io/badge/Add_to-VCC-blue?logo=vrchat&logoColor=white&style=for-the-badge)](https://sebane1.github.io/VRCSlimeVRToeRigSupport/index.html)

*(If the button above does not work in your browser, follow the manual steps below)*

1. Open the **VRChat Creator Companion**.
2. Navigate to **Settings** -> **Packages** -> **Add Repository**.
3. Enter the repository URL for this plugin: `https://raw.githubusercontent.com/Sebane1/VRCSlimeVRToeRigSupport/main/index.json`
4. Once added, open your Avatar project in VCC.
5. Find "SlimeVR Toe Rig Support" in the package list and click the **(+)** button to install it.

## Setup Instructions

1. Open the tool by going to **`Tools` -> `Toe Rig` -> `Add Toe Tracking Compatibility`** in the top menu.
2. Wait for the configuration window to appear.
3. **Assign Files:**
   - Find your avatar's **VRC Expression Parameters** and drag the file into the appropriate slot.
   - Find your avatar's **Animator Controller** and drag the file into its slot.
4. **Adjust Values (Optional):** Tweak the numbered values in degrees as you desire, or keep them as is. 
   - *Splay values control how much toes can go sideways per toe.*
5. **Assign Toe Bones:** After the values are to your liking, find and assign the toe bones from your avatar's rig into the slots. 
   - *Note:* Use as many toes as your rig supports (minimum 1 per foot, up to 5). If your avatar has fewer than 5 toes, simply leave the extra boxes empty (`None`). The tool will safely skip them. You may wish to assign your final toe to slot 5 for pinky toe splay.
6. Click **Generate Toe Support**.
7. Your avatar should now have toe tracking support with compatible versions of SlimeVR!

## Optional: OSCSmooth

You may wish to use an additional plugin called [OSCSmooth](https://github.com/regzo2/OSCmooth) to make sure the toes look smooth to other people over the network. 

- If you are using OSCSmooth, check the **"Uses OSC Smooth"** box before hitting generate.
- You will have to run the OSCSmooth plugin **AFTER** running the initial generation from this tool.
