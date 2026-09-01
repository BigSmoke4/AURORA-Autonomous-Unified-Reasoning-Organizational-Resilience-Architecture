// aurora.core.js — small shared utilities. No global namespace pollution: everything lives
// under window.Aurora (spec section 51).
window.Aurora = window.Aurora || {};

Aurora.core = {
  formatCurrency(value) {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(value || 0);
  },
  formatPercent(value) {
    return `${Math.round((value || 0) * 100)}%`;
  },
  debounce(fn, wait) {
    let t;
    return (...args) => {
      clearTimeout(t);
      t = setTimeout(() => fn(...args), wait);
    };
  }
};
