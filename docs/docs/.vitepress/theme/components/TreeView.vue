<script setup>
import { ref } from 'vue'
import TreeNode from './TreeNode.vue'

const props = defineProps({
  title: String,
  subtitle: String,
  data: Object,
  views: {
    type: Array,
    default: () => []
  },
  note: String,
  defaultView: {
    type: String,
    default: null
  }
})

const selectedView = ref(props.defaultView || (props.views.length > 0 ? props.views[0].id : null))

const getCurrentData = () => {
  if (props.views.length > 0) {
    const view = props.views.find(v => v.id === selectedView.value)
    return view?.data || props.data
  }
  return props.data
}
</script>

<template>
  <div class="tree-view-wrapper">
    <div class="tree-view-header">
      <h2>{{ title }}</h2>
      <p v-if="subtitle" class="subtitle">{{ subtitle }}</p>
      
      <div v-if="views.length > 0" class="view-filters">
        <button
          v-for="view in views"
          :key="view.id"
          @click="selectedView = view.id"
          :class="{ 
            active: selectedView === view.id,
            [`btn-${view.color}`]: view.color
          }"
        >
          {{ view.label }}
        </button>
      </div>

      <div v-if="note" class="info-note">
        {{ note }}
      </div>
    </div>

    <div class="tree-content">
      <TreeNode v-bind="getCurrentData()" />
    </div>
  </div>
</template>

<style scoped>
.tree-view-wrapper {
  margin: 2rem 0;
  font-family: var(--vp-font-family-base);
}

.tree-view-header h2 {
  margin-top: 0;
  margin-bottom: 0.5rem;
  border-top: none;
}

.subtitle {
  color: var(--vp-c-text-2);
  margin-bottom: 1rem;
}

.view-filters {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.view-filters button {
  padding: 0.5rem 1rem;
  border-radius: 0.5rem;
  font-size: 0.875rem;
  font-weight: 500;
  border: 1px solid var(--vp-c-divider);
  background: var(--vp-c-bg-soft);
  color: var(--vp-c-text-1);
  cursor: pointer;
  transition: all 0.2s;
}

.view-filters button:hover {
  background: var(--vp-c-bg-mute);
}

.view-filters button.active {
  color: white;
}

.view-filters button.active:not([class*="btn-"]) {
  background: var(--vp-c-brand-1);
  border-color: var(--vp-c-brand-1);
}

.view-filters button.btn-green.active {
  background: #22c55e;
  border-color: #22c55e;
}

.view-filters button.btn-purple.active {
  background: #a855f7;
  border-color: #a855f7;
}

.view-filters button.btn-orange.active {
  background: #f97316;
  border-color: #f97316;
}

.view-filters button.btn-blue.active {
  background: #3b82f6;
  border-color: #3b82f6;
}

.info-note {
  background: var(--vp-custom-block-info-bg);
  border-left: 4px solid var(--vp-c-brand-1);
  padding: 1rem;
  margin-bottom: 1rem;
  font-size: 0.875rem;
  border-radius: 0.25rem;
}

.tree-content {
  background: var(--vp-c-bg-soft);
  border-radius: 0.5rem;
  padding: 1.5rem;
  overflow-x: auto;
  font-family: var(--vp-font-family-mono);
  font-size: 0.875rem;
}
</style>