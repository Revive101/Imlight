<script setup>
import { Server, Users, Zap, Package } from 'lucide-vue-next'
import TreeView from './TreeView.vue'

const views = [
  {
    id: 'hierarchy',
    label: 'Full Hierarchy',
    color: 'blue',
    data: {
      name: 'ActorSystem',
      icon: Zap,
      color: 'blue',
      description: 'Imlight',
      children: [
        {
          name: 'LoginServer',
          icon: Server,
          color: 'green',
          description: 'Authentication & Server Registry',
          children: [
            {
              name: 'GameServerPool',
              color: 'green',
              children: [
                {
                  name: 'GameServer',
                  color: 'purple',
                  description: 'per instance',
                  children: [
                    {
                      name: 'GameWorld',
                      color: 'purple',
                      children: [
                        {
                          name: 'Zone',
                          color: 'purple',
                          description: 'per zone instance',
                          children: [
                            {
                              name: 'ZoneObjectSupervisor',
                              color: 'purple',
                              children: [
                                {
                                  name: 'ZoneEntity',
                                  description: 'NPCs, objects'
                                }
                              ]
                            },
                            {
                              name: 'ZonePlayerSupervisor',
                              color: 'purple'
                            },
                            {
                              name: 'ZoneTriggerSupervisor',
                              color: 'purple',
                              children: [
                                {
                                  name: 'ZoneTrigger',
                                  description: 'quest triggers, interactions'
                                }
                              ]
                            },
                            {
                              name: 'ZoneVolumeSupervisor',
                              color: 'purple',
                              children: [
                                {
                                  name: 'ZoneEntity',
                                  description: 'volume areas'
                                }
                              ]
                            },
                            {
                              name: 'ZonePathSupervisor',
                              color: 'purple',
                              children: [
                                {
                                  name: 'ZonePath',
                                  description: 'movement paths',
                                  children: [
                                    {
                                      name: 'ZoneEntity'
                                    }
                                  ]
                                }
                              ]
                            },
                            {
                              name: 'ZoneSigilSupervisor',
                              color: 'purple',
                              children: [
                                {
                                  name: 'ZoneEntity',
                                  description: 'sigil objects'
                                }
                              ]
                            },
                            {
                              name: 'ResultExecutorActor',
                              description: 'quest/zone results'
                            }
                          ]
                        }
                      ]
                    },
                    {
                      name: 'CommandDispatcher',
                      color: 'purple'
                    },
                    {
                      name: 'SessionActor',
                      icon: Users,
                      color: 'purple',
                      description: 'per client'
                    },
                    {
                      name: 'ProcessSupervisor',
                      color: 'purple'
                    }
                  ]
                }
              ]
            },
            {
              name: 'SessionActor',
              icon: Users,
              color: 'green',
              description: 'per client'
            }
          ]
        },
        {
          name: 'PatchServer',
          icon: Package,
          color: 'orange',
          description: 'Game Updates & Patches',
          children: [
            {
              name: 'SessionActor',
              icon: Users,
              color: 'orange',
              description: 'per client'
            }
          ]
        }
      ]
    }
  },
  {
    id: 'login',
    label: 'Login Server',
    color: 'green',
    data: {
      name: 'LoginServer',
      icon: Server,
      color: 'green',
      description: 'Authentication & Server Registry',
      children: [
        {
          name: 'GameServerPool',
          color: 'green',
          children: [
            {
              name: 'GameServer',
              color: 'purple',
              description: 'per instance'
            }
          ]
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'green',
          description: 'per client'
        }
      ]
    }
  },
  {
    id: 'game',
    label: 'Game Server Detail',
    color: 'purple',
    data: {
      name: 'GameServer',
      icon: Server,
      color: 'purple',
      description: 'Managed by LoginServer\'s GameServerPool',
      children: [
        {
          name: 'GameWorld',
          color: 'purple',
          children: [
            {
              name: 'Zone',
              color: 'purple',
              description: 'per zone instance',
              children: [
                {
                  name: 'ZoneObjectSupervisor',
                  color: 'purple',
                  children: [
                    {
                      name: 'ZoneEntity',
                      description: 'NPCs, objects'
                    }
                  ]
                },
                {
                  name: 'ZonePlayerSupervisor',
                  color: 'purple'
                },
                {
                  name: 'ZoneTriggerSupervisor',
                  color: 'purple',
                  children: [
                    {
                      name: 'ZoneTrigger',
                      description: 'quest triggers, interactions'
                    }
                  ]
                },
                {
                  name: 'ZoneVolumeSupervisor',
                  color: 'purple',
                  children: [
                    {
                      name: 'ZoneEntity',
                      description: 'volume areas'
                    }
                  ]
                },
                {
                  name: 'ZonePathSupervisor',
                  color: 'purple',
                  children: [
                    {
                      name: 'ZonePath',
                      description: 'movement paths',
                      children: [
                        {
                          name: 'ZoneEntity'
                        }
                      ]
                    }
                  ]
                },
                {
                  name: 'ZoneSigilSupervisor',
                  color: 'purple',
                  children: [
                    {
                      name: 'ZoneEntity',
                      description: 'sigil objects'
                    }
                  ]
                },
                {
                  name: 'ResultExecutorActor',
                  description: 'quest/zone results'
                }
              ]
            }
          ]
        },
        {
          name: 'CommandDispatcher',
          color: 'purple'
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'purple',
          description: 'per client'
        },
        {
          name: 'ProcessSupervisor',
          color: 'purple'
        }
      ]
    }
  },
  {
    id: 'patch',
    label: 'Patch Server',
    color: 'orange',
    data: {
      name: 'PatchServer',
      icon: Package,
      color: 'orange',
      description: 'Game Updates & Patches',
      children: [
        {
          name: 'SessionActor',
          icon: Users,
          color: 'orange',
          description: 'per client'
        }
      ]
    }
  }
]
</script>

<template>
  <TreeView
    :views="views"
    default-view="hierarchy"
    note="Each server type includes TcpListenerActor, MessageServices, SocketListener, and SocketSender actors (not shown for clarity)."
  />
</template>