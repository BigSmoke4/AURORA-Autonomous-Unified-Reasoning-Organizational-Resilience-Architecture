// aurora.graph.js — renders the neural digital twin graph (spec section 54-56).
// A small, dependency-free force-directed layout (spring attraction along edges + Coulomb-like
// repulsion between all nodes), rendered as SVG. No charting library required.
window.Aurora = window.Aurora || {};

Aurora.graph = (function () {
  const STATUS_CLASS = { Healthy: 'healthy', Warning: 'warning', Critical: 'critical' };

  function layout(nodes, edges, width, height, iterations = 220) {
    const positions = new Map();
    const n = nodes.length;
    nodes.forEach((node, i) => {
      const angle = (2 * Math.PI * i) / n;
      const radius = Math.min(width, height) * 0.32;
      positions.set(node.id, {
        x: width / 2 + radius * Math.cos(angle) + (Math.random() - 0.5) * 20,
        y: height / 2 + radius * Math.sin(angle) + (Math.random() - 0.5) * 20,
        vx: 0, vy: 0
      });
    });

    const k = Math.sqrt((width * height) / Math.max(n, 1)); // ideal spring length
    const adjacency = edges.map(e => ({ a: e.source, b: e.target, strength: e.strength }));

    for (let iter = 0; iter < iterations; iter++) {
      const cooling = 1 - iter / iterations;

      // Repulsion between every pair (fine for graphs of this size — tens of nodes, not thousands).
      for (let i = 0; i < nodes.length; i++) {
        for (let j = i + 1; j < nodes.length; j++) {
          const p1 = positions.get(nodes[i].id), p2 = positions.get(nodes[j].id);
          let dx = p1.x - p2.x, dy = p1.y - p2.y;
          let dist = Math.sqrt(dx * dx + dy * dy) || 0.01;
          const force = (k * k) / dist;
          dx /= dist; dy /= dist;
          p1.vx += dx * force * 0.02; p1.vy += dy * force * 0.02;
          p2.vx -= dx * force * 0.02; p2.vy -= dy * force * 0.02;
        }
      }

      // Attraction along edges.
      adjacency.forEach(({ a, b, strength }) => {
        const p1 = positions.get(a), p2 = positions.get(b);
        if (!p1 || !p2) return;
        let dx = p2.x - p1.x, dy = p2.y - p1.y;
        let dist = Math.sqrt(dx * dx + dy * dy) || 0.01;
        const force = (dist * dist) / k * (0.3 + strength * 0.7);
        dx /= dist; dy /= dist;
        p1.vx += dx * force * 0.01; p1.vy += dy * force * 0.01;
        p2.vx -= dx * force * 0.01; p2.vy -= dy * force * 0.01;
      });

      positions.forEach(p => {
        p.x += p.vx * cooling; p.y += p.vy * cooling;
        p.vx *= 0.85; p.vy *= 0.85;
        p.x = Math.max(30, Math.min(width - 30, p.x));
        p.y = Math.max(30, Math.min(height - 30, p.y));
      });
    }

    return positions;
  }

  function render(container, data, onNodeClick) {
    const width = container.clientWidth || 900;
    const height = container.clientHeight || 560;
    const positions = layout(data.nodes, data.edges, width, height);

    const svgNs = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(svgNs, 'svg');
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);

    const edgeGroup = document.createElementNS(svgNs, 'g');
    const nodeGroup = document.createElementNS(svgNs, 'g');

    data.edges.forEach(edge => {
      const p1 = positions.get(edge.source), p2 = positions.get(edge.target);
      if (!p1 || !p2) return;
      const line = document.createElementNS(svgNs, 'line');
      line.setAttribute('x1', p1.x); line.setAttribute('y1', p1.y);
      line.setAttribute('x2', p2.x); line.setAttribute('y2', p2.y);
      line.setAttribute('class', 'aurora-graph-edge');
      line.setAttribute('stroke-width', Math.max(0.5, edge.strength * 3));
      line.dataset.source = edge.source;
      line.dataset.target = edge.target;
      edgeGroup.appendChild(line);
    });

    data.nodes.forEach(node => {
      const p = positions.get(node.id);
      const g = document.createElementNS(svgNs, 'g');
      const statusClass = STATUS_CLASS[node.status] || 'unknown';
      g.setAttribute('class', `aurora-graph-node aurora-graph-node--${statusClass}`);
      g.setAttribute('transform', `translate(${p.x}, ${p.y})`);
      g.dataset.nodeId = node.id;

      const circle = document.createElementNS(svgNs, 'circle');
      const radius = 6 + node.criticality * 2.2;
      circle.setAttribute('r', radius);
      g.appendChild(circle);

      const label = document.createElementNS(svgNs, 'text');
      label.setAttribute('text-anchor', 'middle');
      label.setAttribute('y', radius + 12);
      label.textContent = node.name.length > 22 ? node.name.slice(0, 20) + '…' : node.name;
      g.appendChild(label);

      g.addEventListener('click', () => onNodeClick && onNodeClick(node, g));
      nodeGroup.appendChild(g);
    });

    svg.appendChild(edgeGroup);
    svg.appendChild(nodeGroup);
    container.innerHTML = '';
    container.appendChild(svg);

    return { svg, edgeGroup, nodeGroup };
  }

  function focusNode(handles, nodeId) {
    handles.nodeGroup.querySelectorAll('.aurora-graph-node').forEach(el => {
      el.classList.toggle('aurora-graph-node--focused', el.dataset.nodeId === nodeId);
    });
    handles.edgeGroup.querySelectorAll('.aurora-graph-edge').forEach(el => {
      const connected = el.dataset.source === nodeId || el.dataset.target === nodeId;
      el.classList.toggle('aurora-graph-edge--highlight', connected);
    });
  }

  function filterBySearch(handles, query) {
    const q = (query || '').toLowerCase().trim();
    handles.nodeGroup.querySelectorAll('.aurora-graph-node').forEach(el => {
      const label = el.querySelector('text')?.textContent?.toLowerCase() || '';
      el.style.opacity = !q || label.includes(q) ? '1' : '0.15';
    });
  }

  return { render, focusNode, filterBySearch };
})();
