# Color Block Jam

Color Block Jam is a Unity puzzle game.

The player slides colored blocks on a grid. Each block must go to a door with the same color. The level is complete when all blocks leave the board before the time is over.

## Project Info

- Unity version: 2022.3.53f1
- Main scene: `Assets/Scenes/MainScene.unity`
- Game version: 0.1
- Input: mouse or touch pointer input
- Languages: English and Turkish

## How To Run

1. Open the project folder in Unity Hub.
2. Use Unity `2022.3.53f1`.
3. Open `Assets/Scenes/MainScene.unity`.
4. Press Play.

## How To Play

- Click or touch a block.
- Drag the block up, down, left, or right.
- Move each block to a door with the same color.
- Clear all blocks to pass the level.
- If the timer ends, the level fails.

## Game Features

- 16 level assets.
- Different block shapes and colors.
- Color doors on the board edge.
- A timer for each level.
- Saved level progress.
- Music and SFX settings.
- English and Turkish localization.
- Simple UI panels for start, game, settings, feature unlock, win, and fail states.

Some levels also use special block rules:

- Horizontal blocks can only move left and right.
- Vertical blocks can only move up and down.
- Timed exit blocks must leave in a limited number of moves.
- Locked blocks open after other blocks leave.
- Nested shape blocks have more than one color layer.

## Main Folders

- `Assets/Scripts/Runtime` - main game code.
- `Assets/Data` - levels, shapes, and level collection data.
- `Assets/Scenes` - Unity scenes.
- `Assets/Art` - block art, materials, prefabs, and icons.
- `Assets/Audio` - music and sound effects.
- `Assets/UI` - UI Toolkit panels and styles.
- `Assets/Localization` - English and Turkish text tables.
- `Assets/Editor` - editor tools for level data, shape data, pools, and player data.

## Level Editing

Levels are ScriptableObject assets in `Assets/Data/LevelDefinitions`.

The level order is stored in `Assets/Data/LevelCollection.asset`.

Useful Unity menu items:

- `Tools > Color Block Jam > Level Editor`
- `Tools > Color Block Jam > Shape Editor`
- `Tools > Color Block Jam > Data > Sync Collection From Assets`
- `Tools > Color Block Jam > Player Data`

## Save Data

The game saves player data as `player-data.json` in Unity `Application.persistentDataPath`.

Saved data includes:

- Current level
- Seen feature unlocks
- Music setting
- SFX setting
- Language setting
