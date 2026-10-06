// Decode the actual CDP long-poll response, including Chromium's binary body
// representation. Preserve the received envelope for the separate contract reader.
function notificationsFromResponse(response) {
  const body = response.base64Encoded ? Buffer.from(response.body, 'base64').toString('utf8') : response.body;
  const events = [];
  for (const part of body.split('\x1e').filter(Boolean)) {
    const message = JSON.parse(part);
    if (message.type !== 1 || !['StateChanged', 'DiagnosticChanged', 'HandoffReady',
      'WholeTrayCompleted', 'ObservedUnlocked', 'FinalUnloadCompleted'].includes(message.target)) continue;
    const envelope = message.arguments?.[0] ?? null;
    events.push({ event: message.target, runId: envelope?.runId ?? null, envelope });
  }
  return events;
}
module.exports = { notificationsFromResponse };
