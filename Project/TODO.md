# Unity 2D RPG - Procedural Generation TODO

## ✅ Completed - Phase 1 & 2
- [x] Core chunk system (loading/unloading)
- [x] WorldGenerator with Perlin noise
- [x] BiomeConfig ScriptableObject system
- [x] ForestBiome with ground tiles
- [x] Enemy spawning (Slime, Ghost)
- [x] Object spawning (Bush, Tree)
- [x] Grouped spawning system
- [x] Fog of war / vision system
- [x] Sorting layer fixes
- [x] Increased spawn density

## 🚧 Current Phase: Polish & Expansion

### 1. Implement Canopy System
**Goal:** Add dark tree foliage overlay that renders above player

**Tasks:**
- [ ] Verify Canopy Rule Tile is assigned in ForestBiome
- [ ] Test canopy rendering (should appear above player at sorting order 5)
- [ ] Adjust canopy density (currently 0.3) for better coverage
- [ ] Consider multiple canopy variations for visual interest
- [ ] Optimize canopy generation if performance issues

**Files to check:**
- `BiomeConfig.cs` - Canopy fields already added
- `WorldGenerator.cs` - Canopy generation logic already implemented
- `Chunk.cs` - Canopy tilemap already created

### 2. Add Ground Variation to ForestBiome
**Goal:** More visual variety in terrain using decoration tiles

**Tasks:**
- [ ] Identify additional ground tiles from spritesheet (tiles 22-26, 31-35)
- [ ] Test which tiles work well as standalone decoration
- [ ] Add 3-5 decoration tiles to ForestBiome decorationTiles array
- [ ] Adjust Perlin noise threshold for decoration placement
- [ ] Consider adding Path Rule Tile overlays in some areas
- [ ] Test different terrainScale values for noise variation

**Recommended tiles to try:**
- Tiles 22-26 (generic ground variations)
- Tiles 31-35 (mixed terrain)
- Path Rule Tile (for natural trails between areas)

**Code adjustments:**
- `WorldGenerator.cs:53` - Adjust `noise > 0.7f` threshold
- Consider multi-layer decoration (2-3 decoration tiers)

### 3. Create Additional Biomes
**Goal:** Multiple distinct biomes for world variety

#### 3.1 Plains Biome
**Characteristics:**
- Open grassland with fewer trees
- More bushes and grass variations
- Higher enemy density
- Brighter lighting

**Implementation:**
- [ ] Create `PlainsBiome.asset` ScriptableObject
- [ ] Assign ground tiles (lighter grass tiles)
- [ ] Reduce tree spawn rate
- [ ] Increase bush spawn rate
- [ ] Configure lower canopy density (0.1)
- [ ] Set enemy density higher (0.6-0.8)

#### 3.2 Swamp Biome
**Characteristics:**
- Water features using Water Rule Tile
- Grape enemies (projectile-based)
- Darker, murkier atmosphere
- Dense vegetation

**Implementation:**
- [ ] Create `SwampBiome.asset` ScriptableObject
- [ ] Add Water Rule Tile support to generation
- [ ] Use darker ground tiles
- [ ] Add Grape enemy prefab to enemy spawns
- [ ] Increase object density with bushes
- [ ] Add fog effect (reduce vision radius in swamp)

#### 3.3 Dungeon Biome (Future)
**Characteristics:**
- Enclosed rooms and corridors
- Higher enemy density
- Ledge Rule Tile for walls
- No canopy

**Implementation:**
- [ ] Research BSP or cellular automata for room generation
- [ ] Create `DungeonBiome.asset` ScriptableObject
- [ ] Use Ledge Rule Tile for walls
- [ ] Implement room carving algorithm
- [ ] Add corridor connections
- [ ] Place enemies in rooms
- [ ] Consider treasure/loot rooms

### 4. Biome Transition System
**Goal:** Smooth transitions between biomes

**Tasks:**
- [ ] Analyze current biome selection (Perlin noise moisture/temperature)
- [ ] Test biome boundaries - verify smooth transitions
- [ ] Consider blending tiles at biome edges
- [ ] Add transition chunks (mixed biome features)
- [ ] Ensure no harsh visual seams between biomes

**Code location:**
- `WorldGenerator.cs:25-37` - `DetermineBiome()` method

### 5. Advanced Features (Post-Biomes)
**Future enhancements after basic biomes are complete**

- [ ] Procedural quest system integration
- [ ] Dynamic difficulty based on biome
- [ ] Special landmark generation (boss arenas, towns)
- [ ] Biome-specific loot tables
- [ ] Save/load chunk persistence
- [ ] Minimap system
- [ ] Biome discovery tracking

## 📝 Notes

**Performance Targets:**
- Target: 60 FPS with 25 active chunks
- Chunk generation: <50ms per chunk
- Monitor object pooling efficiency

**Testing Checklist:**
- [ ] Move between biomes - verify transitions
- [ ] Test chunk loading/unloading at boundaries
- [ ] Verify enemy spawning in all biomes
- [ ] Check sorting layers for all new objects
- [ ] Ensure canopy renders above player
- [ ] Test fog of war in different biomes
- [ ] Profile memory usage with multiple biomes

**Configuration Files:**
- `Assets/ScriptableObjects/Biomes/ForestBiome.asset`
- `Assets/ScriptableObjects/Biomes/PlainsBiome.asset` (to create)
- `Assets/ScriptableObjects/Biomes/SwampBiome.asset` (to create)

**Key Scripts:**
- `Assets/Scripts/World Generation/WorldGenerator.cs`
- `Assets/Scripts/World Generation/BiomeConfig.cs`
- `Assets/Scripts/World Generation/ChunkManager.cs`
- `Assets/Scripts/World Generation/Chunk.cs`
