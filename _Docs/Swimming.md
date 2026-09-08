# Character Swimming

## Definition, Visuals

Character Swimming is a third-person movement mode that activates automatically when the character becomes sufficiently immersed in water.

The intended feel is broadly similar to GTA V swimming. GTA V is a behavioral reference, not a requirement to reproduce its exact implementation.

### Movement
- Swimming supports full 3D movement.
- Swimming movement is camera-relative.
- Moving forward sends the character toward the direction the camera is facing.
- Camera-directed control uses the camera's vertical angle for underwater movement.
- Dive-button control keeps horizontal movement camera-yaw-relative and controls depth with a dedicated action.
- The character rotates and pitches toward the actual swimming direction.
- Swimming supports normal and fast movement speeds.
- Holding Shift activates fast swimming.
- Fast swimming is available both at the surface and underwater.

### Surface Behavior
- At the surface, the character remains constrained to a natural position around the waterline.
- Normal surface movement occurs along the water surface.
- The character should not accidentally move above or beneath the surface during ordinary surface swimming.
- Camera-directed control submerges when the player deliberately aims/moves downward.
- Dive-button control submerges while Dive is held and floats upward after release.
- The developer selects the active control mode in the swimming movement settings.
- Jump is available from surface swimming.
- Reaching the waterline from underwater automatically transitions the character into surface swimming.

### Underwater Behavior
- Underwater movement is freely controllable in three dimensions.
- The character can ascend, descend, and move horizontally through camera-relative movement.
- Camera-directed control approximately maintains depth when movement input stops.
- Dive-button control automatically floats toward the surface whenever Dive is released.

### Buoyancy
Buoyancy is an assisted gameplay behavior rather than a realistic physical simulation.

- At the surface, buoyancy stabilizes the character around the waterline.
- Underwater, buoyancy should not fight deliberate movement.
- An idle underwater character approximately holds their chosen depth.
- The priority is predictable and responsive player control.

### Locomotion States
The swimming feature conceptually distinguishes:

- Surface idle
- Underwater idle
- Active surface swimming
- Active underwater swimming
- Fast surface swimming
- Fast underwater swimming

Surface and underwater swimming may have different movement feel and presentation, but remain part of the same overall swimming movement mode.

### Animation Requirements
Separate animations are supported for:

- Surface idle
- Underwater idle
- Surface swimming
- Underwater swimming
- Fast surface swimming
- Fast underwater swimming

Underwater-specific animations are optional at runtime.

If an underwater animation is unavailable, use the corresponding surface animation:

- Underwater idle → Surface idle
- Underwater swimming → Surface swimming
- Fast underwater swimming → Fast surface swimming

Missing underwater animations must not prevent the associated swimming behavior from functioning.

## Boundaries & Constraints

- **Scope Limits**
  - No breath or oxygen system.
  - No drowning system.
  - No water currents.
  - No underwater combat.

- **Hard Constraints**
  - Swimming activates automatically based on sufficient immersion.
  - Swimming uses camera-relative movement.
  - Underwater movement supports three dimensions.
  - Surface movement remains constrained to the waterline until the player deliberately moves downward.
  - Camera-directed diving requires no dedicated action; Dive-button mode provides an alternative dedicated action.
  - Reaching the surface automatically transitions to surface swimming.
  - Leaving sufficiently deep water automatically returns the character to an appropriate ground locomotion mode.
  - Swimming supports normal and fast speeds.
  - Shift activates fast swimming.
  - Swimming collision should support sliding along obstacles similarly to normal character locomotion.

- **Negative Space**
  - Swimming is not intended to simulate realistic fluid physics.
  - Buoyancy should not override deliberate player movement.
  - Surface swimming should not allow the character to rise unnaturally above the water.
  - Underwater characters should not automatically float back to the surface.
  - A dedicated Dive button is required only when Dive-button control is selected.
  - The feature does not require underwater-specific animations to exist before the corresponding gameplay behavior can function.

## Execution

### Interaction 1: Entering Water
1. The character enters a body of water.
2. While the water is shallow enough for ground movement, swimming remains inactive.
3. If shallow-water ground locomotion is available, it is used.
4. Otherwise, normal ground locomotion is used.
5. Once the character becomes sufficiently immersed and ground locomotion is no longer appropriate, swimming activates automatically.
6. The character begins with surface or underwater behavior according to their position relative to the waterline.

### Interaction 2: Surface Idle
1. The character is at the water surface.
2. The player provides no movement input.
3. The character remains stabilized around the waterline.
4. The surface idle animation is used.

### Interaction 3: Underwater Idle
1. The character is submerged.
2. The player provides no movement input.
3. Active propulsion stops.
4. The character approximately maintains their current depth.
5. The underwater idle animation is used when available.
6. Otherwise, the surface idle animation is used.

### Interaction 4: Surface Swimming
1. The player provides movement input while at the surface.
2. Movement is interpreted relative to the camera.
3. The character moves along the surface while remaining constrained around the waterline.
4. The character rotates toward the movement direction.
5. The surface swimming animation is used.

### Interaction 5: Diving From the Surface
1. The character is at the surface.
2. The player deliberately aims/moves downward in camera-directed mode, or holds Dive in Dive-button mode.
3. The surface constraint is released.
4. The character pitches toward the requested movement direction.
5. The character moves beneath the surface.
6. Underwater swimming behavior becomes active.

### Interaction 5A: Jumping From the Surface
1. The character is in surface swimming.
2. The player presses Jump.
3. The character launches upward using the normal jump force and enters airborne locomotion.
4. Swimming re-entry is suppressed while the jump is still rising inside the water threshold.
5. If the character falls back into the water, swimming activates normally.

### Interaction 6: Underwater Swimming
1. The player provides movement input while submerged.
2. Movement is interpreted relative to the camera in three dimensions.
3. The character moves through the water in the requested direction.
4. The character rotates and pitches toward that direction.
5. The underwater swimming animation is used when available.
6. Otherwise, the surface swimming animation is used.

### Interaction 7: Returning to the Surface
1. The character swims upward, or automatically floats upward after releasing Dive in Dive-button mode.
2. The character reaches the waterline.
3. Upward movement does not carry the character unnaturally above the surface.
4. The character settles into the appropriate surface position.
5. Surface swimming behavior becomes active.

### Interaction 8: Fast Swimming
1. The character is actively swimming.
2. The player holds Shift.
3. Swimming switches to the faster movement speed.
4. At the surface, the fast surface swimming animation is used.
5. Underwater, the fast underwater swimming animation is used when available.
6. If unavailable, the fast surface swimming animation is used.
7. Releasing Shift returns the character to normal swimming speed.

### Interaction 9: Swimming Into Obstacles
1. The character swims into an obstacle.
2. Collision prevents movement through the obstacle.
3. Valid movement parallel to the obstacle remains possible.
4. The character slides along the obstacle similarly to normal character locomotion.

### Interaction 10: Entering Shallow Water or Leaving Water
1. The character reaches land or water shallow enough that swimming is no longer necessary.
2. Swimming ends automatically.
3. If shallow-water ground locomotion is available, the character transitions into it.
4. Otherwise, the character transitions into normal ground locomotion.

### Interaction 11: Moving From Shallow Water Into Deep Water
1. The character moves through shallow water using ground locomotion.
2. The character becomes sufficiently immersed that ground locomotion is no longer appropriate.
3. Swimming activates automatically.
4. No dedicated player command is required.
