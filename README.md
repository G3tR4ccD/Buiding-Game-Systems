# Building Game Systems

A Unity sandbox where I build and test common game systems one at a time: a first-person controller, an inventory with a hotbar, equipment, health, and a melee combo. Each system is a small, separate script so I can practice wiring them together.

This is a learning project, not a finished game.

First person View 

<img width="505" height="462" alt="image" src="https://github.com/user-attachments/assets/c5189ff1-3ba2-420c-8dcb-a1b268067438" />


Scene View 

<img width="426" height="322" alt="image" src="https://github.com/user-attachments/assets/ed89f14d-f08b-4ac9-9eef-9b2c610528f2" />


Inventory 

<img width="1370" height="850" alt="image" src="https://github.com/user-attachments/assets/cbb1810e-342a-46fe-b441-961d9754f529" />


## Systems

### First-person controller (`PlayerMovement.cs`)
- WASD or stick movement relative to where the camera looks.
- Mouse look where the head turns first, then the body follows once the head hits its limit.
- A camera anchor that smoothly follows the animated head bone, so animation jitter doesn't shake the view.
- Coyote time and jump buffering, so jumps feel forgiving.
- Jump animation speed that scales to match the real air time.

### Inventory and hotbar (`Inventory.cs`, `Slot.cs`, `ItemSO.cs`)
- Items are ScriptableObject assets, so every copy of an item shares one source of truth.
- Adding items stacks onto matching slots first, then fills empty ones.
- Drag and drop to move, merge, or swap stacks. Right-click drag carries half a stack.
- Shift-click sends a stack to the other container. Ctrl-click spreads it across matching stacks.
- Hotbar selection with the number keys 1 to 5 and the scroll wheel, with wrap-around.
- Hover an inventory item and press a number key to swap it into that hotbar slot.
- A shared tooltip and a number label that lights up with its slot (`SlotNumberLabel.cs`).
- Opening the inventory frees the cursor and pauses movement and look input.

### Equipment (`Equipment.cs`)
- Four gear slots: helmet, chestplate, leggings, and boots.
- Each slot only accepts the matching equipment type.
- Equipped items appear on the character through anchor points, and disappear when removed.

### Health (`Health.cs`, `HealthBar.cs`)
- A health value with damage, healing, and a max, clamped so it can't go out of range.
- Changes fire an event, so the health bar updates without checking every frame.
- The bar slides smoothly to the new value and can show the number as text.

### Melee combo (`AttackCombo.cs`)
- Three-hit combo: bite right, bite left, then paws.
- Timers block mashing and reset the combo if you wait too long between hits.

## Debug keys

These are temporary, for testing:

| Key | Action |
| --- | --- |
| P | Give one of every item in `Resources/Items` |
| O | Take damage |
| U | Heal |

## Controls

| Input | Keyboard and mouse | Gamepad |
| --- | --- | --- |
| Move | W A S D (or I J K L) | Left stick |
| Look | Mouse | Not bound |
| Jump | Space | South button |
| Attack combo | Left mouse button | Right trigger |
| Open and close inventory | Tab | North button |
| Select hotbar slot | 1 to 5 | Not bound |
| Cycle hotbar | Scroll wheel | Not bound |
| Quick transfer / spread a stack | Shift-click / Ctrl-click a slot | Not bound |
Debug keys (P, O, U) are listed above.

## Running it

1. Install Unity Hub and the Unity version listed in `ProjectSettings/ProjectVersion.txt`.
2. Clone this repo and add the folder in Unity Hub.
3. Open the project's scene and press Play. There's only one.

The project uses Unity's Input System package, so the action events need to be hooked up in the Inspector.

## Status

Working: everything listed above.

Not built yet:
- Saving and loading. `DataManager.cs` is an empty placeholder for it.
- Using items from the hotbar. The selected slot is tracked, but nothing happens when you use it.
- Pickups and drops in the world.
- The attack combo plays animations but doesn't deal damage yet.

## How I built it

<!-- Change this to match how you actually built the project. -->
I followed Unity tutorials and asked Claude (an AI assistant) for help when I got stuck. I tested and adjusted the results in my own project.

## What I practiced

- Events, so one script can react to another without constant checking.
- ScriptableObjects for item data.
- Unity's UI event interfaces for drag and drop and tooltips.
- The Input System, including coyote time and input buffering.
