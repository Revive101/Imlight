<script setup>
import { Server, Users, Wifi, ShieldCheck, UserCog, ArrowRightLeft, Database } from 'lucide-vue-next'
import TreeView from './TreeView.vue'

const views = [
  {
    id: 'overview',
    label: 'Architecture',
    color: 'green',
    data: {
      name: 'LoginServer',
      icon: Server,
      color: 'green',
      children: [
        {
          name: 'GameServerPool',
          icon: Database,
          color: 'green',
          children: [
            {
              name: 'GameServer',
              icon: Server,
              color: 'purple'
            },
            {
              name: 'GameServer',
              icon: Server,
              color: 'purple'
            },
            {
              name: 'GameServer',
              icon: Server,
              color: 'purple'
            }
          ]
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'green',
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
              name: 'ControlService',
              color: 'blue'
            },
            {
              name: 'AccountService',
              color: 'blue'
            },
            {
              name: 'AuthenticatorService',
              icon: ShieldCheck,
              color: 'blue'
            },
            {
              name: 'CharacterService',
              icon: UserCog,
              color: 'blue'
            },
            {
              name: 'GameTransitionService',
              icon: ArrowRightLeft,
              color: 'blue'
            },
            {
              name: 'LoginAFKService',
              color: 'blue'
            }
          ]
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'green',
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
    id: 'flow',
    label: 'Connection Flow',
    color: 'purple',
    data: {
      name: 'LoginServer',
      icon: Server,
      color: 'green',
      children: [
        {
          name: 'TcpListenerActor',
          icon: Wifi,
          color: 'green'
        },
        {
          name: 'SessionActor',
          icon: Users,
          color: 'blue',
          children: [
            {
              name: 'Session Handshake',
              color: 'blue'
            },
            {
              name: 'AuthenticatorService',
              icon: ShieldCheck,
              color: 'blue'
            },
            {
              name: 'CharacterService',
              icon: UserCog,
              color: 'blue'
            },
            {
              name: 'GameTransitionService',
              icon: ArrowRightLeft,
              color: 'purple'
            }
          ]
        },
        {
          name: 'GameServerPool',
          icon: Database,
          color: 'purple',
          children: [
            {
              name: 'Server Selection',
              color: 'purple'
            },
            {
              name: 'Session Handoff',
              color: 'purple'
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