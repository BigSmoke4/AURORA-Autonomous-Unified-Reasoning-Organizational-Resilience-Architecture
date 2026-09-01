// aurora.signalr.js — minimal SignalR client loader for simulation progress (spec section 39).
// Loaded from the official CDN only on pages that need it (Simulation/Details).
(function () {
  const script = document.createElement('script');
  script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js';
  script.onload = () => window.dispatchEvent(new Event('aurora:signalr-ready'));
  document.head.appendChild(script);
})();
