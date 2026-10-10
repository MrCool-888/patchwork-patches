// Explicitly run with Node 18+ to check Windows credentialless compatibility.
// Holds temporary session tokens in memory only, prints no credentials, and revokes all created sessions.
const headers = {
  'Content-Type': 'application/json', Accept: 'application/json',
  'x-pm-appversion': 'windows-vpn@5.1.8', 'x-pm-apiversion': '3',
  'User-Agent': 'Patchwork Windows guest compatibility probe'
};
const sessions = [];
async function request(path, method, session, body) {
  return fetch('https://vpn-api.proton.me' + path, {
    method, redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { ...headers, ...(session ? { Authorization: 'Bearer ' + session.AccessToken, 'x-pm-uid': session.UID } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {})
  });
}
(async () => {
  try {
    let response = await request('/auth/v4/sessions', 'POST');
    let body = await response.json();
    console.log('Windows unauthenticated bootstrap: HTTP ' + response.status + ', code ' + body.Code);
    if (body.Code !== 1000 || !body.AccessToken || !body.UID) throw Error('Bootstrap did not produce a usable temporary session');
    sessions.push(body);
    response = await request('/auth/v4/credentialless', 'POST', body, { Payload: {} });
    body = await response.json();
    if (body.Code === 1000 && body.AccessToken && body.UID) sessions.push(body);
    console.log('Windows credentialless request: HTTP ' + response.status + ', code ' + body.Code);
    if (body.Code === 5003 && body.Error === 'Platform expected to be in [Android, iOS]') console.log(body.Error);
    else if (body.Code === 1000) console.log('Platform accepted this probe. A functioning Windows guest client still requires challenge handling and the complete session/certificate lifecycle.');
    else console.log('Guest session unavailable; no credentials or raw response are printed.');
  } finally {
    for (const session of sessions.reverse()) {
      try {
        const response = await request('/auth', 'DELETE', session);
        console.log('Temporary session revocation: HTTP ' + response.status);
        if (!response.ok) process.exitCode = 1;
      } catch { console.error('Temporary session revocation failed; no token is logged.'); process.exitCode = 1; }
    }
  }
})().catch(() => { console.error('Guest compatibility probe failed; no credentials are logged.'); process.exitCode = 1; });
