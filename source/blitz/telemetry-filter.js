// Runs inside Blitz's existing onBeforeSendHeaders handler, including overlay sessions.
// Keep its authentication/CORS handlers and the desktop ad filter on their own events.
function patchworkIsTelemetryURL(url) {
  if (url.protocol !== "https:") return false;
  const host = url.hostname;
  return host === "science.v2.iesdev.com" ||
    host === "google-analytics.com" || host.endsWith(".google-analytics.com") ||
    (host === "www.googletagmanager.com" && url.pathname === "/gtag/js") ||
    (host === "data.v2.iesdev.com" &&
      url.pathname === "/api/v1/query_objects/prod/bi/machine_segment");
}
