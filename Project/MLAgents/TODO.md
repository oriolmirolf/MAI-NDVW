1. Fix attacking action (now only equips weapon)
2. Implement 2 continuous actions for aiming: one for movement and another for attacking
    - Merge movement and dashing discrete branches into one with 3 actions: nothing, walk or dash
3. Add logging when rewards are given (new addition and total for each agent)
4. Maybe add reward for being closer to opponent in order for them to learn that they have to fight, and then remove it once they have a basic control of the game.