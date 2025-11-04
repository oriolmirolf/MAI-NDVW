# Pre-Sprint 2 Meeting

## Current Status
- We already have the base game with the base mechanics working.
- We should now start implementing the AI elements
- Sprint 2 will briefly comment the implementation of the base game, and mainly discuss our progress in the AI parts

## AI Elements 
1. Map Layout Generation (BSP)
2. Room Generation (Cellular Automata)
3. RL Boss Enemies
4. LLM-generated Lore/Dialogue

## Tasks Assignments
1. Uri -> BSP
2. Dani -> Cellular Automata
3. Bruno -> RL Enemies
4. Jean -> LLM integration

## Additional Notes
- Set up Unity Version Control
- For map generation:
  1. First we generate the Map Layout (BSP), deciding room sizes and connections
  2. Then, we generate the rooms themselves (CA) with the appropriate sizes and exit ways (based on connections)
  - The Map Generation component would decide the Map Layout (Step 1) and then call the "Room Generation" (Step 2) function for each room
  - The background, walls and decorations (enemies as well?) need to be instantiated from code
    - The enemies and decorations spawning should be handled within the Room Generation component (maybe just with heuristics)
  - Parameterize as much as possible the level generation, so we can try out different configs (and comment the results in the reports, even the bad ones)
- For the RL enemies, we could follow 1 of 2 ideas:
  1. The 3 bosses are the "player character", at 3 different frozen versions of training
  2. Each of the 3 bosses only has 1 of the 3 player weapons. There could be some transfer learning for basic movement
- For LLM-generated Lore:
  - Fix the number of rooms
  - Generate as many lore pieces as rooms
  - Work on the first level for now (until the first boss)
  - Let's say 10 rooms/lore pieces
  - Generate the lore sequentially (when generating a new room takes into account the lore of the previous room) 
  - The lore could tell you the story about the boss you will encounter at the end of the level
  - To ground the LLM, fix who the boss is and have the LLM generate a different backstory every time
  - Main boss is the 3rd boss and he wipes memories, that is why the story changes every time

## Next Meeting
- Sunday 09/11/2025 around 15:00 (for ~30 mins)
