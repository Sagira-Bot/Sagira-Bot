# Sagira-Bot

TODO: Write this readme.

# Usage
This is a very impromptu summary of usage, but once you get the bot online after setting your envs, launch the bot. Once done, you have a few commands you can run:
Assuming prefix = !
!rolls [ITEM NAME] which will pull either an exact match to the item name regardless of year, or a list of vague matches and prompt the user to select the right one. When duplicates of the same item are found, it prioritizes random rollable versions.

!y1 [ITEM NAME] will do the same as above but the logic is shifted to prioritize year 1 variants

!curated [ITEM NAME] will do the same as above but the logic is shifted to prioritize curated rolls for y2 items (and falls back on y1 items)

# Workflow
* BungieDriver -> Pulls Manifest DB
* Sagira -> Your DB handler. Parses all Json found via DB queries
* SagiraBot -> Discord Bot instance
* SagiraModule -> Actual Module that uses DI to utilize Sagira Object's DB functionalities. 

# Setup
The only particular thing to note about the setup is that you need to put a .env file in your binaries folder (or edit the Env.Load calls to point to wherever you want your env file).
Format of that file: Plaintext file with each of these entries on their own lines.

* APIKEY=
* BTOKEN=
* PREFIX=!
* LOG=DEBUG.log
* LOGDIR=Logs

# Envs
* APIKEY -> Bungie's API Key.
* BTOKEN -> Discord Bot Token.
* Prefix -> Server Prefix
* LOG -> Bungie.Driver/Sagira objects log to this file.
* LOGDIR -> Log above stored here.
