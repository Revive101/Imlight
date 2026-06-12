<script setup>
import { ref, computed, useSlots } from 'vue'
import { ChevronRight, ChevronDown } from 'lucide-vue-next'

const props = defineProps({
  name: String,
  icon: Object,
  color: { type: String, default: 'blue' },
  description: String,
  children: Array,
  defaultExpanded: { type: Boolean, default: true }
})

const isExpanded = ref(props.defaultExpanded)
const slots = useSlots()

const hasChildren = computed(() => {
  const hasSlots = slots.default && slots.default().length > 0
  const hasChildrenProp = props.children && props.children.length > 0
  return hasSlots || hasChildrenProp
})
</script>

<template>
  <div class="tree-node">
    <div class="tree-node-content">
      <button
        v-if="hasChildren"
        @click="isExpanded = !isExpanded"
        class="expand-button"
      >
        <ChevronDown v-if="isExpanded" :size="16" />
        <ChevronRight v-else :size="16" />
      </button>
      <div v-else class="spacer" />
      
      <div class="node-info">
        <component v-if="icon" :is="icon" :size="16" :class="'icon-' + color" />
        <span class="node-name">{{ name }}</span>
        <span v-if="description" class="node-description"># {{ description }}</span>
      </div>
    </div>
    
    <div v-if="hasChildren && isExpanded" class="node-children">
      <slot>
        <TreeNode 
          v-for="(child, index) in children" 
          :key="index"
          v-bind="child"
        />
      </slot>
    </div>
  </div>
</template>

<script>
export default {
  name: 'TreeNode'
}
</script>

<style scoped>
.tree-node {
  margin-left: 1rem;
}

.tree-node-content {
  display: flex;
  align-items: start;
  gap: 0.5rem;
  padding: 0.25rem 0;
}

.expand-button {
  margin-top: 0.25rem;
  background: none;
  border: none;
  color: var(--vp-c-text-3);
  cursor: pointer;
  padding: 0;
  display: flex;
  align-items: center;
  transition: color 0.2s;
}

.expand-button:hover {
  color: var(--vp-c-text-1);
}

.spacer {
  width: 1rem;
}

.node-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
}

.node-name {
  font-weight: 500;
  color: var(--vp-c-text-1);
}

.node-description {
  font-size: 0.75rem;
  color: var(--vp-c-text-2);
  font-style: italic;
}

.icon-blue { color: #3b82f6; }
.icon-green { color: #22c55e; }
.icon-purple { color: #a855f7; }
.icon-orange { color: #f97316; }

.node-children {
  border-left: 2px solid var(--vp-c-divider);
  margin-left: 0.5rem;
}
</style>