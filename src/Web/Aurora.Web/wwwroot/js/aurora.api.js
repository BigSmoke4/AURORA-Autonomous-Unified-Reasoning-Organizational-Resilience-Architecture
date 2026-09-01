// aurora.api.js — thin fetch wrapper for AURORA's JSON API (spec section 51).
window.Aurora = window.Aurora || {};

Aurora.api = {
  async get(url) {
    const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
    if (!res.ok) throw new Error(`GET ${url} failed: ${res.status}`);
    return res.json();
  },
  async post(url, body) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        ...(token ? { 'RequestVerificationToken': token } : {})
      },
      body: JSON.stringify(body)
    });
    if (!res.ok) throw new Error(`POST ${url} failed: ${res.status}`);
    return res.json();
  }
};
