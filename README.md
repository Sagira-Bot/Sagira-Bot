# Sagira-Bot

TODO: Write this readme.

# Usage
This is a very impromptu summary of usage, but once you get the bot online after setting your envs, launch the bot. Once done, you have a few commands you can run:

/rolls [ITEM NAME] which will pull either an exact match to the item name regardless of year, or a list of vague matches and prompt the user to select the right one. When duplicates of the same item are found, it prioritizes random rollable versions.

/year1 [ITEM NAME] will do the same as above but the logic is shifted to prioritize year 1 variants

/curated [ITEM NAME] will do the same as above but the logic is shifted to prioritize curated rolls for y2 items (and falls back on y1 items)

# Workflow
* BungieDriver -> Pulls Manifest DB Tables
* ItemHandler -> Your Table handler. Will act as a service to parse item tables as needed.
* SagiraBot -> Discord Bot instance
* ItemModule -> Actual Module that handles all discord interactions between the User, Bot, and ItemHandler.

# Setup
The only particular thing to note about the setup is that you need to populate sagiraConfig.json.
Fields are self explanatory, you can leave DefaultTimeoutSeconds and DebugServer blank if you want (not suggested).
If you leave DebugServer blank, your bot will upload global commands which will take 1 hour to process.
To find the value for DebugServer, right click the desired server -> copy ID.

