// aurora.simulation.js — Scenario Lab progress bar (SignalR) + Simulation Results chart (SVG).
(function () {
  document.addEventListener('DOMContentLoaded', () => {
    wireProgressBar();
    renderImpactChart();
  });

  function wireProgressBar() {
    const form = document.getElementById('run-simulation-form');
    const progress = document.getElementById('simulation-progress');
    if (!form || !progress) return;

    let connection = null;
    const runIdMatch = window.location.pathname.match(/scenarioId/);

    window.addEventListener('aurora:signalr-ready', async () => {
      connection = new signalR.HubConnectionBuilder().withUrl('/hubs/simulation').build();
      connection.on('progress', (payload) => {
        const bar = progress.querySelector('.aurora-progress__bar');
        const message = progress.querySelector('.aurora-progress__message');
        const pct = payload.totalDays > 0 ? Math.round((payload.currentDay / payload.totalDays) * 100) : 100;
        bar.style.setProperty('--pct', pct + '%');
        bar.querySelector('::after');
        bar.style.setProperty('width', '100%');
        bar.style.background = `linear-gradient(90deg, var(--aurora-accent-strong) ${pct}%, var(--aurora-bg-recessed) ${pct}%)`;
        message.textContent = payload.message;
      });
      try { await connection.start(); } catch (e) { console.warn('SignalR connection failed', e); }
    });

    form.addEventListener('submit', () => {
      progress.hidden = false;
    });
  }

  function renderImpactChart() {
    const el = document.getElementById('impact-chart');
    if (!el) return;
    const series = JSON.parse(el.dataset.series || '[]');
    if (!series.length) {
      el.innerHTML = '<p class="aurora-note" style="padding:16px;">No simulation steps to chart yet.</p>';
      return;
    }

    const width = el.clientWidth || 800, height = el.clientHeight || 260;
    const padding = 36;
    const maxDay = Math.max(...series.map(s => s.day));
    const maxImpact = Math.max(...series.map(s => s.impact), 0.01);
    const maxRevenue = Math.max(...series.map(s => s.revenue), 1);

    const xScale = d => padding + (d / maxDay) * (width - padding * 2);
    const yScaleImpact = v => height - padding - (v / maxImpact) * (height - padding * 2);
    const yScaleRevenue = v => height - padding - (v / maxRevenue) * (height - padding * 2);

    const impactPoints = series.map(s => `${xScale(s.day)},${yScaleImpact(s.impact)}`).join(' ');
    const revenuePoints = series.map(s => `${xScale(s.day)},${yScaleRevenue(s.revenue)}`).join(' ');

    const svgNs = 'http://www.w3.org/2000/svg';
    const svg = document.createElementNS(svgNs, 'svg');
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);

    const axisX = document.createElementNS(svgNs, 'line');
    axisX.setAttribute('x1', padding); axisX.setAttribute('y1', height - padding);
    axisX.setAttribute('x2', width - padding); axisX.setAttribute('y2', height - padding);
    axisX.setAttribute('class', 'axis');
    svg.appendChild(axisX);

    const impactLine = document.createElementNS(svgNs, 'polyline');
    impactLine.setAttribute('points', impactPoints);
    impactLine.setAttribute('class', 'impact-line');
    svg.appendChild(impactLine);

    const revenueLine = document.createElementNS(svgNs, 'polyline');
    revenueLine.setAttribute('points', revenuePoints);
    revenueLine.setAttribute('class', 'revenue-line');
    svg.appendChild(revenueLine);

    const legend = document.createElementNS(svgNs, 'text');
    legend.setAttribute('x', padding); legend.setAttribute('y', 16);
    legend.setAttribute('class', 'axis-label');
    legend.textContent = 'Red: total impact score  ·  Teal (dashed): daily revenue impact';
    svg.appendChild(legend);

    el.innerHTML = '';
    el.appendChild(svg);
  }
})();
