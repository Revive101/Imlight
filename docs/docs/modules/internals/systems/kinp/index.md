# Kinetic Networking Protocol

_Kinetic Networking Protocol_ is a proprietary networking protocol used by the game client. It serves to enable interactions between each player and the game environment by facilitating communication between the game client and server.

KiNP is based on a master/slave protocol architecture, in which numerous client connections (the "slaves") communicate with a single game server (the "master") to manage the game environment. This gives the game server complete control over each and every transaction, and it is highly prevalent in other games of its kind.