# Keychain folder guidance

This folder is used for storing various API keys that are required for the bot to function, but that cannot be exposed in the source code due to their sensitive nature.

For most, the only file that you will need in here is `token.txt`, which is used for storing the token that the bot will use to run. It **must** be named `token.txt` and it **must** be in this directory, as this is what the program looks for and reads the bot's token from. This is also the case for any additional API keys you may want to utilize.

For some of the extra commands, particularly those that query web services, additional API keys are required in order to get a successful response to the query. The method for obtaining these API keys vary between sites, but all of them should be free and easy (given some research) to obtain.

A full list of filenames and what API keys are expected from them is as follows:

- `token.txt`: Discord bot token. Get one by creating an application on Discord (https://discordapp.com/developers/applications/me) and adding a bot user. You can then click on some text to reveal that bot's token

- `searchkey.txt`: Google search key to enable querying of Google for ?search, the logic for which is contained in ExtraCommands.cs.
