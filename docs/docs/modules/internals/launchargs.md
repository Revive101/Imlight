# Launch Arguments
When launching the `.exe` located in the `bin` directory, there are various launch arguments that can be passed to modify the behavior of the client.

Below is a table of the possible launch arguments.

| Argument | Description |
| :------- | :---------- |
| `-?` | Displays a generic help message. |
| `-L [IP] [PORT]` | Skips the launcher and connects to a login server. |
| `-U ..[ID] [PasswordHash] [Username]` | Hands relevant user identification to the game client. |
| `-C [GID]` | Skips character creation and loads a character by ID. Must be used in tandem with `-U`. |
| `-Z [IP]` | Used in tandem with `-C` to load the character into a specific zone server. |
| `-T [Name]` | Tests the local zone server. |
| `-E [GID]` | Equip this item on the local player. Must be used in tandem with `-T-R [Zone Name] - Run Zone`. |
| `-R [IP]` | Runs a zone server. |
| `-R2 [IP] [GID]` | Runs a secondary test player connecting to the `-R` zone server. |
| `-D [DIRECTORY]` | Sets the data root directory. |
| `-S [NAME]` | Runs a script by name. |
| `-SR [RESOLUTION]` | Sets the screen resolution. Ex. `1280x1024`. |
| `-K [0 \| 1]` | Enables the script debugger. |
| `-HS` | Enable heap server. |
| `-HD` | Enables heap debugging. |
| `-EF_OVERFLOW` | Enables overflow detection. | 
| `-EF_UNDERFLOW` | Enables underflow detection. |
| `-G [PATH]` | Enables logging to a given file path. |
| `-P [0 \| 1]` | Enables patching. |
| `-M [0 \| 1]` | Enables maintenance mode. |
| `-X [PATH]` | Dumps internal types to a given file path. Requires elevated authentication. |
| `-O` | Logs all resource requests. |
| `-A [LOCALE]` | Sets the clients language. |
| `-UN [0 \| 1]` | Force unique character names. |
| `-ST` | Steam required. |
| `-PT` | Patch client patch time. |
| `-CS`| Dumps client signature. |