STEALTH ESCAPE - SETUP (Unity 2023 / Unity 6, 3D)

1. Create a new 3D project (Built-in, URP or HDRP all work).
2. Window > Package Manager > "Unity Registry" > install "AI Navigation".
3. Edit > Project Settings > Player > Other Settings > Active Input Handling
   -> set to "Input Manager (Old)" or "Both" (the scripts use UnityEngine.Input).
4. Copy the "Scripts" folder into your Assets folder.
5. Open an EMPTY scene (keep the Directional Light, the camera gets replaced).
6. Create an empty GameObject at position 0,0,0, name it "Game" and add the "GameManager" script.
   (MazeGenerator and SpawnManager are added automatically.)
7. File > Build Settings > "Add Open Scenes" (needed for the R = restart key).
8. Press Play.

CONTROLS: WASD move, mouse look, Shift run (loud), C / Ctrl crouch (quiet), R restart after the game ends, Esc frees the mouse.

ENEMY COLOURS (debug): Guard = blue, Dog = brown, Assassin = purple.
 yellow = suspicious, orange = investigating, red = chasing, pink = searching, cyan = dog following your scent, white = dashing.

TIP: turn on Gizmos in the Game view to see vision cones and the scent trail (green dots).

----------------------------------------------------------------
GRAPHICS UPDATE
New files: Game/Gfx.cs, Game/FlickerLight.cs, AI/Visuals/EnemyModels.cs, AI/Visuals/EnemyAnimator.cs
Changed files: World/MazeGenerator.cs, World/SpawnManager.cs, Game/GameManager.cs,
               AI/Core/EnemyBase.cs, Game/PlayerController.cs
- Textured lab walls / floor / ceiling, ceiling lamps, flickering lights, fog, flashlight (F).
- Open rooms and shortcut loops (Maze Generator: "Rooms" and "Shortcuts"). Doors can never be bypassed.
- Guard = human with flashlight, Dog = four-legged dog, Assassin = hovering robot with blades.
- Eye colour shows the enemy state: green = unaware, yellow = suspicious, orange = investigating,
  red = chasing, pink = searching, cyan = dog on your scent, white = dashing.
- Red screen edge + RUN! while something is chasing you.

----------------------------------------------------------------
SOUND + GUIDE UPDATE
New files: Game/AudioManager.cs, Game/GuideSystem.cs
Changed: GameManager, Door, KeyCard, PlayerController, EnemyAnimator, GuardDog, SpawnManager, MazeGenerator
- All sounds are generated in code. Enemies have 3D footsteps, the robot hums, the dog barks.
- Top of screen: objective + arrow. M = big map. H = draw the real route on the floor (15 s cooldown).
- Escape order: Red key > Red door > Blue key > Blue door > Green key > Green door > yellow pillar.
