# Verdant Zero

**Purify. Reforest. Save the City.**

Verdant Zero is an arcade drone game made in Unity 6 (URP). Fly the **Verdant-1** drone over a smog-choked
city, break up pollution clouds with the **Purifier Beam** and fire **Instant Seeds** at planting spots to
grow trees that clean the air on their own. Bring the smog down to 0% before the clock runs out.

## How to play

| Action | Keyboard / mouse | Gamepad | Touch |
| --- | --- | --- | --- |
| Move / turn | W A S D | Left stick | Joystick (left) |
| Climb / descend | Space / Left Shift | Right stick, D-pad or RB / LB | ▲ / ▼ buttons |
| Purifier Beam | Hold left click | RT or A | Hold **BEAM** |
| Instant Seed | Right click | LT or X | **SEED** |
| Switch camera (3rd / 1st person) | V | Select / View | Camera button |
| Pause | Esc | Start | II button |
| Toggle fullscreen | F11 (desktop) | – | Options → Fullscreen |

- **Victory:** smog reaches 0%.
- **Defeat:** smog reaches 100%, time runs out, or the drone crashes into a building.
- Difficulty (Easy / Normal / Hard) is set from **Options** in the main menu.

## Fullscreen

- **Desktop builds** start in borderless fullscreen at the monitor's native resolution. F11 or the
  *Fullscreen* toggle (Options / Pause menu) switches to a window; the choice is remembered.
- **Web build** uses the `VerdantZero` WebGL template (`Assets/WebGLTemplates/VerdantZero`): the game always
  fills the entire browser window and enters true browser fullscreen on the first click, tap or key press.
  There is also a fullscreen button in the corner and the *Fullscreen* toggle in the menus.
- **Android / iOS** run fullscreen (immersive) in landscape.

## Project layout

```
VerdantZero/                 Unity project (open this folder in Unity 6000.5.6f1 or newer)
  Assets/VerdantZero/        Game content: Scenes, Scripts, UI (UI Toolkit), Audio, Prefabs, VFX...
  Assets/WebGLTemplates/     Fullscreen web page template
```

Scenes: `Bootstrapper`, `MainMenu`, `Level01` (in `Assets/VerdantZero/Scenes`).

## Third-party assets (import locally)

These Asset Store packages are licensed and not included in the repository. Import them into
`VerdantZero/Assets` before opening `Level01`:

- POLYGON City Pack (city buildings and props)
- LowpolyVegetationPackFree by PolyMount (vegetation)
- Low Poly Birds by Zacxophone (bird flocks)

`sfx_drone_loop.wav`, `birds_sound.wav` and `running_out_of_time_sound.mp3` are royalty-free synthesized
stand-ins; replace them at the same paths with your own clips if you prefer.

## Credits

Skies: Kloofendal Overcast Pure Sky and Spruit Sunrise, Poly Haven (CC0). Font: Fredoka (SIL Open Font
License 1.1). Engine: Unity with the Universal Render Pipeline.
