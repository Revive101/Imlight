# Imlight
Imlight is a private server emulation project by the Wizard101Rewritten community.

---

Imlight is intended to be the successor to Ravenwood. It's primary goal is better server architecture and documentation. Each server is controlled by a series of nodes, each working in unison to emulate Kingisle's backend service. You can view the overview of server architecture [here](https://drive.google.com/file/d/17utqstWzrlxPp8cVjTZX4e_Hhy8ThKSn/view?usp=sharing).

---

## Contributing
Contributions are welcome by Wizard101Rewritten developers only. All changes should be branched by topic, and pull requested to main.

Review proper branch naming conventions [here](https://codingsight.com/git-branching-naming-convention-best-practices/).

---

## What needs doing
This list is updated sparingly. For more recent updates, use the Trello board found in the developer info channel in the Wizard101Rewritten server.
- [ ] Core Systems
  - [ ] Backend
    - [ ] Reviving hung or dead nodes 
  - [ ] Realm
    - [x] Handling connections 
    - [x] Receiving data
    - [ ] Returning data
    - [ ] Sending data to a processor
    - [ ] Zones 
    - [ ] Worlds
  - [ ] Engine
    - [x] Message deserialization
    - [x] Message serialization
    - [x] Deserializing types
    - [x] Serializing types 
    - [ ] Deserializing CoreObject
    - [ ] Serializing CoreObject 
  - [ ] Processor
    - [ ] Message handlers 
- [ ] Common
  - [ ] Logging
    - [x] Logging to console
    - [x] Logging to file
    - [x] Logging events
    - [x] Rollback log files
    - [x] Archiving old log files
    - [ ] Logging to a database
- [ ] Login
- [ ] Storage
  - [ ] User Data
  - [ ] World Data
    - [ ] Zones
      - [ ] Zone transfer coordinates 
    - [ ] Worlds  
- [ ] StorageBackup

- [ ] Unit testing
  - [ ] @todo 

 Tools are properitary software to gather data for Imlight.
- [ ] Tools
  - [x] Generating ingame types along with their hashes.
  - [ ] Gathering zone transfer coordinates
  - [ ] Gathering questing data
  - [ ] Packet capture
    - [ ] Deserializating packets to messages
