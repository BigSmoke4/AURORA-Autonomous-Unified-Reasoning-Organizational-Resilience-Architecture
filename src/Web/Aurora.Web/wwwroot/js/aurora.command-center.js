// aurora.command-center.js — wires the neural graph into the Command Center page.
(function () {
  document.addEventListener('DOMContentLoaded', async () => {
    const container = document.getElementById('neural-graph-container');
    if (!container) return;

    let handles = null;
    let selectedNodeId = null;

    async function load() {
      const data = await Aurora.api.get('/api/graph/snapshot');
      handles = Aurora.graph.render(container, data, (node) => {
        selectedNodeId = node.id;
        Aurora.graph.focusNode(handles, node.id);
        window.dispatchEvent(new CustomEvent('aurora:node-selected', { detail: node }));
      });
    }

    document.getElementById('graph-search')?.addEventListener('input', Aurora.core.debounce((e) => {
      if (handles) Aurora.graph.filterBySearch(handles, e.target.value);
    }, 150));

    document.getElementById('graph-reset')?.addEventListener('click', () => load());

    await load();
  });
})();
