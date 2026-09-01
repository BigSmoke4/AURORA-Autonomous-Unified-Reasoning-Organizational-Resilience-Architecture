// aurora.ai.js — the AI Command Panel (spec section 59). Sends the question plus the
// currently-selected graph node to /api/ai/investigate and renders the evidence-backed result.
(function () {
  document.addEventListener('DOMContentLoaded', () => {
    const btn = document.getElementById('ai-investigate-btn');
    const questionBox = document.getElementById('ai-question');
    const resultBox = document.getElementById('ai-result');
    if (!btn || !questionBox || !resultBox) return;

    let selectedNode = null;
    window.addEventListener('aurora:node-selected', (e) => { selectedNode = e.detail; });

    btn.addEventListener('click', async () => {
      if (!selectedNode) {
        alert('Click a node in the neural graph first, then ask about it — AURORA investigates real entities, it does not guess a target.');
        return;
      }
      btn.disabled = true;
      btn.textContent = 'Investigating...';

      try {
        const result = await Aurora.api.post('/api/ai/investigate', {
          targetNodeId: selectedNode.id,
          question: questionBox.value || `What would be affected if ${selectedNode.name} failed?`
        });

        resultBox.hidden = false;
        resultBox.querySelector('.aurora-ai-result__confidence').textContent = `Confidence: ${result.confidence}%`;
        resultBox.querySelector('.aurora-ai-result__narrative').textContent = result.narrative;

        const evidenceEl = resultBox.querySelector('.aurora-ai-result__evidence');
        evidenceEl.innerHTML = '';
        (result.evidence || []).forEach(ev => {
          const div = document.createElement('div');
          div.className = 'aurora-ai-evidence-item';
          div.innerHTML = `<strong>${ev.tool}</strong> (${Math.round(ev.durationMs)}ms): ${ev.summary}`;
          evidenceEl.appendChild(div);
        });
      } catch (err) {
        resultBox.hidden = false;
        resultBox.querySelector('.aurora-ai-result__narrative').textContent = 'Investigation failed: ' + err.message;
      } finally {
        btn.disabled = false;
        btn.textContent = 'Investigate';
      }
    });
  });
})();
