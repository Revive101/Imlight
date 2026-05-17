import DefaultTheme from 'vitepress/theme'
import ActorHierarchy from './components/ActorHierarchy.vue'
import MessageServicesTree from './components/MessageServicesTree.vue'
import LoginServerArchitecture from './components/LoginServerArchitecture.vue'
import GameServerArchitecture from './components/GameServerArchitecture.vue'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('ActorHierarchy', ActorHierarchy)
    app.component('MessageServicesTree', MessageServicesTree)
    app.component('LoginServerArchitecture', LoginServerArchitecture)
    app.component('GameServerArchitecture', GameServerArchitecture)
  }
}