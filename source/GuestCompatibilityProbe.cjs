// Explicitly run with Node 18+ to check credentialless compatibility.
// Holds temporary session tokens in memory only, prints no credentials, and revokes all created sessions.
// --android changes the client identity only. It does not fabricate challenge data or solve verification.
const android = process.argv.includes('--android');
const label = android ? 'Android-identity' : 'Windows';
const headers = {
  'Content-Type': 'application/json', Accept: 'application/json',
  'x-pm-appversion': android ? 'android-vpn@5.20.57.0' : 'windows-vpn@5.1.8', 'x-pm-apiversion': '3',
  'User-Agent': 'Patchwork Windows guest compatibility probe'
};
const sessions = [];
async function request(path, method, session, body, windowsIdentity = false) {
  return fetch('https://vpn-api.proton.me' + path, {
    method, redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { ...headers, ...(windowsIdentity ? { 'x-pm-appversion': 'windows-vpn@5.1.8' } : {}), ...(session ? { Authorization: 'Bearer ' + session.AccessToken, 'x-pm-uid': session.UID } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {})
  });
}
(async () => {
  try {
    let response = await request('/auth/v4/sessions', 'POST');
    let body = await response.json();
    console.log(label + ' unauthenticated bootstrap: HTTP ' + response.status + ', code ' + body.Code);
    if (body.Code !== 1000 || !body.AccessToken || !body.UID) throw Error('Bootstrap did not produce a usable temporary session');
    sessions.push(body);
    response = await request('/auth/v4/credentialless', 'POST', body, { Payload: {} });
    body = await response.json();
    if (body.Code === 1000 && body.AccessToken && body.UID) sessions.push(body);
    console.log(label + ' credentialless request: HTTP ' + response.status + ', code ' + body.Code);
    if (body.Code === 5003 && body.Error === 'Platform expected to be in [Android, iOS]') console.log(body.Error);
    else if (body.Code === 1000 && body.AccessToken && body.UID) {
      console.log('Guest session issued. Checking Windows VPN-user compatibility; no credentials are logged.');
      response = await request('/vpn/v2', 'GET', body, null, true);
      const vpn = await response.json();
      const info = vpn.VPN || vpn.Vpn || vpn.VPNUser;
      console.log('Windows-identity VPN-user request: HTTP ' + response.status + ', code ' + vpn.Code + ', VPN info present: ' + Boolean(info));
      if (info) console.log('VPN connection credential fields present: ' + Boolean(info.Name && info.Password));
      if (response.ok && vpn.Code === 1000 && info) {
        const crypto = require('node:crypto');
        const keys = crypto.generateKeyPairSync('ed25519');
        const publicPem = keys.publicKey.export({type:'spki',format:'pem'});
        response = await request('/vpn/v1/certificate', 'POST', body, {ClientPublicKey:publicPem,ClientPublicKeyMode:'EC',DeviceName:'Patchwork compatibility probe',Mode:'session',Features:[]}, true);
        const certificate = await response.json();
        console.log('Windows-identity certificate request: HTTP ' + response.status + ', code ' + certificate.Code + ', certificate present: ' + Boolean(certificate.Certificate));
      }
      console.log('A working Windows port still requires protected storage, refresh, certificates, connection integration and a live tunnel test.');
    } else {
      const humanVerification = body.Code === 9001 || Boolean(body.Details && (body.Details.HumanVerificationMethods || body.Details.HumanVerificationToken));
      console.log(humanVerification ? 'Human verification required; probe stops without solving or bypassing it.' : 'Guest session unavailable; no credentials or raw response are printed.');
    }
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
