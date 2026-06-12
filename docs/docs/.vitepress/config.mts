import { defineConfig } from 'vitepress'
import taskLists from 'markdown-it-task-lists';

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Imlight",
  description: "Rewriting Magic",
  srcDir: "modules",
  base: "/Imlight-docs/",
  markdown: {
    config: async (md) => {
      md.use(taskLists);
    }
  },
  head: [
    ['link', { rel: "apple-touch-icon", sizes: "180x180", href: "/apple-touch-icon.png" }],
    ['link', { rel: "icon", type: "image/png", sizes: "32x32", href: "/favicon-32x32.png" }],
    ['link', { rel: "icon", type: "image/png", sizes: "16x16", href: "/favicon-16x16.png" }],
    ['link', { rel: "shortcut icon", href: "/favicon.ico" }],
  ],
  themeConfig: {
    logo: '/logo-nobg.png',
    nav: [
      { text: 'Imlight', link: '/imlight/index' },
      { text: 'Game Internals', link: '/internals/index' },
    ],

    sidebar: {
      '/imlight': [
        {
          text: 'Frequent',
          items: [
            { text: 'Imlight', link: '/imlight/index' },
            { text: 'Quickstart', link: '/imlight/quickstart.md' },
            { text: 'Commands', link: '/imlight/commands' },
            { text: 'Todo', link: '/imlight/todo' },
          ]
        },
        {
          text: 'Core Concepts',
          items: [
            { text: 'Imcodec', link: '/imlight/concepts/imcodec' },
            { text: 'Actor System', link: '/imlight/concepts/actorfundamentals' },
            { text: 'Message Handling', link: '/imlight/concepts/messagehandling' },
            { text: 'Player Architecture', link: '/imlight/concepts/playerarchitecture' },
            { text: 'Shared Data Systems', link: '/imlight/concepts/shared-data-systems' },
          ]
        },
        {
          text: 'Session Actors',
          items: [
            { text: 'Session Actor', link: '/imlight/concepts/sessionactor' },
            { text: 'Message Services', link: '/imlight/concepts/messageservices' },
          ]
        },
        {
          text: 'Login Server',
          items: [
            { text: 'Login Server', link: '/imlight/loginserver/loginserver' },
            { text: 'Authentication & Characters', link: '/imlight/loginserver/authentication-flow' },
            { text: 'Server Discovery', link: '/imlight/loginserver/server-discovery' },
          ]
        },
        {
          text: 'Game Server',
          items: [
            { text: 'Game Server', link: '/imlight/gameserver/gameserver' },
            { text: 'Commands', link: '/imlight/gameserver/commands' },
            { text: 'Combat System', link: '/imlight/gameserver/combat-system' },
            { text: 'Zone Architecture', link: '/imlight/gameserver/zone-architecture' },
            { text: 'Zone Components', link: '/imlight/gameserver/zone-components' },
          ]
        },
        {
          text: 'WizardData',
          items: [
            { text: 'WizardData', link: '/imlight/wizarddata/wizarddata' },
            { text: 'Player Models', link: '/imlight/wizarddata/player-models' },
          ]
        },
      ],

      '/internals/': [
        {
          text: 'Game Internals',
          items: [
            { text: 'Getting Started', link: '/internals/index' },
            { text: 'Launch Args', link: '/internals/launchargs' },
            { text: 'Schemas', link: '/internals/schemas' },
          ]
        },
        {
          text: 'Systems',
          items: [
            {
              text: "KiNP",
              items: [
                { text: 'KI Networking Protocol', link: '/internals/systems/kinp/index' },
                { text: 'Packet Framing', link: '/internals/systems/kinp/packet-framing' },
                { text: 'Control Messages', link: '/internals/systems/kinp/controlmessages' },
                { text: 'Session', link: '/internals/systems/kinp/session' },
              ]
            },
            {
              text: "DML",
              items: [
                { text: 'Distributed Message Layer', link: '/internals/systems/dml/index' },
                { text: 'Message Protocols', link: '/internals/systems/dml/protocols' },
                { text: 'Message Records', link: '/internals/systems/dml/records' },
                { text: 'Serialization', link: '/internals/systems/dml/serialization' },
              ]
            },
            {
              text: "Object Property",
              items: [
                { text: 'Object Property', link: '/internals/systems/op/index' },
                { text: 'Property Class', link: '/internals/systems/op/propertyclass' },
                { text: 'Serialization', link: '/internals/systems/op/serialization' },
                { text: 'Core Objects', link: '/internals/systems/op/coreobject' },
              ]
            },
          ]
        },
        {
          text: "Authority",
          items: [
            { text: 'Authentication', link: '/internals/auth/auth' },
            { text: 'Validation', link: '/internals/auth/validation' },
          ]
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Revive101/Imlight-docs' }
    ]
  }
})
