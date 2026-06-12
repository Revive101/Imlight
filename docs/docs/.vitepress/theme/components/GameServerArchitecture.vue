<script setup>
import { Server, Users, Wifi, Globe, Terminal, Cog, Gamepad2 } from 'lucide-vue-next'
import TreeView from './TreeView.vue'

const views = [
  {
    id: 'overview',
    label: 'Architecture',
    color: 'purple',
    data: {
      name: 'GameServer',
      icon: Server,
      color: 'purple',
      children: [
        {
          name: 'GameWorld',
          icon: Globe,
          color: 'purple',
          children: [
            {
              name: 'Zone',
              color: 'purple'
            },
            {
              name: 'Zone',
              color: 'purple'
            },
            {
              name: 'Zone',
              color: 'purple'
            }
          ]
        },
        {
          name: 'CommandDispatcher',
          icon: Terminal,
          color: 'purple'
        },
        {
          name: 'ProcessSupervisor',
          icon: Cog,
          color: 'purple'
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'purple',
          defaultExpanded: true,
          children: [
            {
              name: 'SocketListener',
              icon: Wifi,
              color: 'blue'
            },
            {
              name: 'SocketSender',
              icon: Wifi,
              color: 'blue'
            },
            {
              name: 'AccountService',
              color: 'blue'
            },
            {
              name: 'WizardService',
              color: 'blue'
            },
            {
              name: 'ZoneService',
              color: 'blue'
            },
            {
              name: 'QuestService',
              color: 'blue'
            },
            {
              name: 'CombatService',
              color: 'blue'
            },
            {
              name: 'InventoryService',
              color: 'blue'
            },
            {
              name: 'ChatService',
              color: 'blue'
            },
            {
              name: 'AttachService',
              color: 'blue'
            }
          ]
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'purple',
          defaultExpanded: false,
          children: [
            {
              name: 'Message Services',
              color: 'blue'
            }
          ]
        }
      ]
    }
  },
  {
    id: 'detailed',
    label: 'Zone Detail',
    color: 'green',
    data: {
      name: 'GameWorld',
      icon: Globe,
      color: 'purple',
      children: [
        {
          name: 'Zone',
          icon: Gamepad2,
          color: 'purple',
          defaultExpanded: true,
          children: [
            {
              name: 'ZoneLoader',
              color: 'green'
            },
            {
              name: 'ZoneObjectSupervisor',
              color: 'green',
              children: [
                {
                  name: 'ZoneEntity',
                  color: 'green'
                }
              ]
            },
            {
              name: 'ZonePlayerSupervisor',
              color: 'green'
            },
            {
              name: 'ZoneTriggerSupervisor',
              color: 'green',
              children: [
                {
                  name: 'ZoneTrigger',
                  color: 'green'
                }
              ]
            },
            {
              name: 'ZoneVolumeSupervisor',
              color: 'green',
              children: [
                {
                  name: 'ZoneEntity',
                  color: 'green'
                }
              ]
            },
            {
              name: 'ZonePathSupervisor',
              color: 'green',
              children: [
                {
                  name: 'ZonePath',
                  color: 'green',
                  children: [
                    {
                      name: 'ZoneEntity',
                      color: 'green'
                    }
                  ]
                }
              ]
            },
            {
              name: 'ZoneSigilSupervisor',
              color: 'green',
              children: [
                {
                  name: 'ZoneEntity',
                  color: 'green'
                }
              ]
            }
          ]
        },
        {
          name: 'Zone',
          icon: Gamepad2,
          color: 'purple',
          defaultExpanded: false,
          children: [
            {
              name: 'Zone Supervisors',
              color: 'green'
            }
          ]
        }
      ]
    }
  }
]
</script>

<template>
  <TreeView
    :views="views"
    default-view="overview"
  />
</template>