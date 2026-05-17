# Imlight

__Imlight__ is the focal point of Revive101's endeavours. It is the internal name for the private server. It is the game, login, and patch server.

## About
The "Imlight" project began as a learning exercise to explore the mechanics and architecture of MMORPG server design. We have the utmost respect for the original developers and have taken steps to ensure our project does not infringe on their intellectual property. That being said, Imlight has a BYOD (bring-your-own-data) philosophy and does not distribute any copyrighted game files. Users must obtain the original client and any necessary assets independently.

## Imlight Parts
There are three primary modules to Imlight:
* __Common__: A library of commonly used parts.
* __CoreLib__: The central hub of traffic, and the actual server. This is just a library and cannot run on its own.
* __Director__: Given a library of multiple parts and running gears, the _director_ is responsible for starting and managing those gears in a convenient order.