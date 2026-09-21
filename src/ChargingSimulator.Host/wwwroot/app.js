const out = document.querySelector('#output');
const form = document.querySelector('#profile');
const controls = document.querySelector('#controls');
const disconnect = document.querySelector('#disconnect');
let session = null;

async function responseJson(response) {
  const value = await response.json();
  if (!response.ok) throw new Error(value.detail || value.title || 'Request failed');
  return value;
}

async function refresh() {
  if (!session) { out.textContent = 'Not connected.'; return; }
  const state = await responseJson(await fetch(`/v1/manual/sessions/${session.sessionId}`, {
    headers: { 'X-Session-Lease': session.leaseToken }
  }));
  out.textContent = JSON.stringify({ session: { chargerId: session.chargerId, status: session.status }, charger: state }, null, 2);
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  try {
    const profile = Object.fromEntries(new FormData(form));
    const result = await responseJson(await fetch('/v1/manual/connect', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(profile)
    }));
    session = result.session;
    controls.hidden = false;
    disconnect.disabled = false;
    document.querySelector('#refresh').disabled = false;
    await refresh();
  } catch (error) { out.textContent = error.message; }
});

document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', async () => {
  if (!session) return;
  try {
    const action = button.dataset.action;
    const outcome = await responseJson(await fetch(`/v1/manual/sessions/${session.sessionId}/connectors/${document.querySelector('#connector').value}/actions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Session-Lease': session.leaseToken },
      body: JSON.stringify({ commandId: crypto.randomUUID(), action, idTag: document.querySelector('#id-tag').value, meterValue: Number(document.querySelector('#meter-value').value), leaseToken: session.leaseToken })
    }));
    out.textContent = JSON.stringify(outcome, null, 2);
    await refresh();
  } catch (error) { out.textContent = error.message; }
}));

disconnect.addEventListener('click', async () => {
  if (!session) return;
  try {
    await responseJson(await fetch('/v1/manual/disconnect', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ sessionId: session.sessionId, leaseToken: session.leaseToken })
    }));
    session = null; controls.hidden = true; disconnect.disabled = true; document.querySelector('#refresh').disabled = true; await refresh();
  } catch (error) { out.textContent = error.message; }
});

document.querySelector('#refresh').addEventListener('click', () => refresh().catch(error => { out.textContent = error.message; }));
