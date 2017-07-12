# Clu

*"Am I still to create the perfect system?"*

Intends to be a primarily mod-focused bot with some other nifty features.

One goal of the project is to hand as much control as possible to the bot operators (i.e. the server owners using it), and so it won't be publically hosted. Configuration will still be largely accessible without access to the bot's instance, though.

Very much a work in progress with a very small portion of its intended functionality at this time.

n.b. it is not intended in future for the bot to take over the Discord server, derez all the ISOs, and force its creator into hiding outside of the Grid, all while plotting to invade the real world with an army of repurposed programs.

## Current commands:
(`$arg`: an argument for you to give, described by what's after the dollar sign)

`?google $query`: Search Google for the results of $query. Uses Google CSE and requires a Google API key. See README in the keychain folder for more info.

`?uptime`: How long has the bot been up for?

## Current functionality:

The bot is able to set the name of any voice channel to what the most 'popular' game within it is, so as to indicate to others what's going on in there, should they be interested. This setting is able to be toggled by reacting with majority X in the #clu-bot-settings channel under the relevant message.

## Settings:

Settings are configured by viewing the #clu-bot-settings channel which the bot automatically makes (and posts messages into) for new guilds. For now, the only type of setting is Y/N which can be configured with reactions. It is planned for settings to support passing data such as user(s) and role(s), which may have to be done by commands. I'm not sure.