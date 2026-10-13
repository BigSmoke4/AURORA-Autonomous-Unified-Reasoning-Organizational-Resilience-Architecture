// aurora.simulation.js — Scenario Lab progress bar (SignalR) + Simulation Results chart (SVG).
(function () {
  document.addEventListener('DOMContentLoaded', () => {
    wireProgressBar();
    renderImpactChart();
  });

  function wireProgressBar() {
    const form = document.getElementById('run-simulation-form');
    const progress = document.getElementById('simulation-progress');
    if (!form || !progress || typeof window.signalR === 'undefined') return;

    // The run's phase messages are sent to the scenario group as soon as the POST starts. A submit
    // therefore waits, for at most this long, for the join to finish, so no phase message is missed.
    const JOIN_WAIT_MS = 3000;

    const scenarioId = form.dataset.scenarioId;
    const message = progress.querySelector('.aurora-progress__message');
    const bar = progress.querySelector('.aurora-progress__bar');
    let joined = false;
    let submitting = false;

    const connection = new window.signalR.HubConnectionBuilder().withUrl('/hubs/simulation').withAutomaticReconnect().build();
    connection.on('progress', (payload) => {
      if (payload.scenarioId !== scenarioId) return;
      const pct = payload.totalPhases > 0 ? Math.round((payload.completedPhases / payload.totalPhases) * 100) : 0;
      bar.style.setProperty('--aurora-progress', pct + '%');
      message.textContent = payload.message;
    });

    // Failure is not an error for the user: the run still starts, just without live updates.
    const joinFinished = connection.start()
      .then(() => connection.invoke('JoinScenario', scenarioId))
      .then(() => { joined = true; })
      .catch(() => {});
    const joinOrTimeout = Promise.race([
      joinFinished,
      new Promise((resolve) => setTimeout(resolve, JOIN_WAIT_MS)),
    ]);

    form.addEventListener('submit', (event) => {
      if (submitting) {
        event.preventDefault();
        return;
      }

      progress.hidden = false;
      bar.style.setProperty('--aurora-progress', '0%');
      message.textContent = 'Starting...';

      if (joined) return; // Joined already: let the browser post the form normally.

      event.preventDefault();
      submitting = true;
      joinOrTimeout.then(() => form.submit());
    });
  }

  function renderImpactChart() {
    const el = document.getElementById('impact-chart');
    if (!el) return;
    const series = JSON.parse(el.dataset.series || '[]');
    if (!series.length) {
      el.innerHTML = '<p class="aurora-note aurora-note--padded">No simulation steps to chart yet.</p>';
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
