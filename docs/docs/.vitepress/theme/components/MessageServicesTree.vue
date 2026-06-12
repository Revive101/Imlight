<script setup>
import { Server, Users, Wifi } from 'lucide-vue-next'
import TreeView from './TreeView.vue'

const servicesData = {
  name: 'GameServer',
  icon: Server,
  color: 'purple',
  description: 'Game Logic & World Management',
  children: [
    {
      name: 'SessionActor',
      icon: Users,
      color: 'blue',
      description: 'Client 1',
      children: [
        {
          name: 'SocketListener',
          icon: Wifi,
          color: 'blue',
          description: 'Incoming messages'
        },
        {
          name: 'SocketSender',
          icon: Wifi,
          color: 'blue',
          description: 'Outgoing messages'
        },
        {
          name: 'AccountService',
          color: 'green'
        },
        {
          name: 'WizardService',
          color: 'purple'
        },
        {
          name: 'ZoneService',
          color: 'purple'
        },
        {
          name: 'QuestService',
          color: 'purple'
        },
        {
          name: 'CombatService',
          color: 'purple'
        },
        {
          name: 'InventoryService',
          color: 'purple'
        },
        {
          name: 'ChatService',
          color: 'purple'
        },
        {
          name: 'AttachService',
          color: 'purple'
        }
      ]
    },
    {
      name: 'SessionActor',
      icon: Users,
      color: 'blue',
      description: 'Client 2',
      defaultExpanded: false,
      children: [
        {
          name: 'SocketListener',
          icon: Wifi,
          color: 'blue',
          description: 'Incoming messages'
        },
        {
          name: 'SocketSender',
          icon: Wifi,
          color: 'blue',
          description: 'Outgoing messages'
        },
        {
          name: 'AccountService',
          color: 'green'
        },
        {
          name: 'WizardService',
          color: 'purple'
        },
        {
          name: 'ZoneService',
          color: 'purple'
        },
        {
          name: 'QuestService',
          color: 'purple'
        },
        {
          name: 'CombatService',
          color: 'purple'
        },
        {
          name: 'InventoryService',
          color: 'purple'
        },
        {
          name: 'ChatService',
          color: 'purple'
        },
        {
          name: 'AttachService',
          color: 'purple'
        }
      ]
    },
    {
      name: 'SessionActor',
      icon: Users,
      color: 'blue',
      description: 'Client 3',
      defaultExpanded: false,
      children: [
        {
          name: 'SocketListener',
          icon: Wifi,
          color: 'blue',
          description: 'Incoming messages'
        },
        {
          name: 'SocketSender',
          icon: Wifi,
          color: 'blue',
          description: 'Outgoing messages'
        },
        {
          name: 'AccountService',
          color: 'green'
        },
        {
          name: 'WizardService',
          color: 'purple'
        },
        {
          name: 'ZoneService',
          color: 'purple'
        },
        {
          name: 'QuestService',
          color: 'purple'
        },
        {
          name: 'CombatService',
          color: 'purple'
        },
        {
          name: 'InventoryService',
          color: 'purple'
        },
        {
          name: 'ChatService',
          color: 'purple'
        },
        {
          name: 'AttachService',
          color: 'purple'
        }
      ]
    }
  ]
}
</script>

<template>
  <TreeView
    :data="servicesData"
  />
</template>