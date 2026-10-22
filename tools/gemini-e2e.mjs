// Real localhost REST + SignalR + Google + MySQL test. Uses synthesized Vietnamese audio,
// not a person's microphone. Run seed-only first; credentials stay in ignored artifacts/.
import { readFile, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { HubConnectionBuilder, HttpTransportType, Subject, LogLevel } from '@microsoft/signalr';

const base = process.env.AIVES_BASE_URL || 'http://localhost:5234';
const demo = JSON.parse(await readFile('artifacts/demo-credentials.local.json', 'utf8'));
const wav = new Uint8Array(await readFile('artifacts/provider-probe.wav'));
function pcmInput(wav) {
  const view = new DataView(wav.buffer, wav.byteOffset, wav.byteLength); let rate = 0, input;
  const text = (at, n) => String.fromCharCode(...wav.slice(at, at + n));
  assert.equal(text(0, 4), 'RIFF');
  for (let offset = 12; offset + 8 <= wav.length;) {
    const size = view.getUint32(offset + 4, true); assert.ok(size <= wav.length - offset - 8);
    if (text(offset, 4) === 'fmt ') { assert.equal(view.getUint16(offset + 8, true), 1); assert.equal(view.getUint16(offset + 10, true), 1); assert.equal(view.getUint16(offset + 22, true), 16); rate = view.getUint32(offset + 12, true); }
    if (text(offset, 4) === 'data') input = wav.slice(offset + 8, offset + 8 + size);
    offset += 8 + size + (size & 1);
  }
  assert.ok(rate && input?.length); const src = new DataView(input.buffer); const count = Math.floor(input.length / 2 * 16000 / rate);
  const output = new Uint8Array(count * 2); const out = new DataView(output.buffer);
  for (let i = 0; i < count; i++) { const at = i * rate / 16000, n = Math.floor(at); const a = src.getInt16(n * 2, true), b = src.getInt16(Math.min(n + 1, input.length / 2 - 1) * 2, true); out.setInt16(i * 2, a + (b - a) * (at - n), true); }
  return output;
}
const pcm = pcmInput(wav);
let checks = 0;
async function request(path, token, body, method = body ? 'POST' : 'GET', expected = 200) {
  const response = await fetch(`${base}${path}`, { method, headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(45000) });
  assert.equal(response.status, expected, `${method} ${path} returned ${response.status}`); checks++;
  if (!response.headers.get('content-type')?.includes('json')) return new Uint8Array(await response.arrayBuffer());
  const json = await response.json(); return json.data ?? json;
}
const login = async email => (await request('/api/Users/login', null, { email, password: demo.password })).token;
const lecturer = await login(demo.lecturer), student = await login(demo.students[0]), outsider = await login(demo.students[1]);
const provider = await request('/api/workflow/providers', lecturer);
assert.equal(provider.speechProvider, 'Gemini'); assert.equal(provider.stt, 'Configured');
const inventory = await request('/api/workflow/gemini/models', lecturer);
for (const model of [provider.liveModel, provider.ttsModel]) assert.ok(inventory.models.some(m => m.name === `models/${model}`));
const start = new Date(Date.now() - 10000), end = new Date(Date.now() + 45 * 60000);
const { examId } = await request('/api/Exams', lecturer, { courseId: demo.courseId, classId: demo.classId, examName: `Gemini live E2E ${Date.now()}`, startTime: start.toISOString(), endTime: end.toISOString(), timePerStudent: 15, mainQuestionCount: 2, maxFollowUpCount: 2, maxFollowUpsPerQuestion: 1, secondsPerQuestion: 120, strategy: 'Random', studentIds: [demo.studentIds[0]] });
await request(`/api/Exams/${examId}/publish`, lecturer, {});
let state = await request(`/api/Exams/${examId}/attempt/start`, student, {});
await request(`/api/Exams/${examId}/attempt`, outsider, null, 'GET', 403);
const hub = new HubConnectionBuilder().withUrl(`${base}/hubs/exam`, { accessTokenFactory: () => student, transport: HttpTransportType.WebSockets, skipNegotiation: true }).configureLogging(LogLevel.None).build();
await hub.start(); await hub.invoke('JoinExam', examId); checks++;
let interimCount = 0, finalCount = 0, audioBytes = 0;
try {
  for (let turn = 0; state.status === 'InProgress' && turn < 4; turn++) {
    const turnId = state.turn.turnId;
    const audio = await request(`/api/Exams/${examId}/attempt/tts?turnId=${encodeURIComponent(turnId)}`, student);
    assert.equal(String.fromCharCode(...audio.slice(0, 4)), 'RIFF'); audioBytes += audio.length;
    const upload = new Subject(); let transcript = '', sending;
    const done = new Promise((resolve, reject) => {
      const timeout = setTimeout(() => { subscription.dispose(); reject(new Error('Live E2E exceeded 35 seconds')); }, 35000);
      const subscription = hub.stream('Transcribe', examId, turnId, upload).subscribe({
        next(event) {
          if (event.type === 'ready') {
            sending = (async () => {
              for (let i = 0; i < pcm.length; i += 3200) { upload.next(Buffer.from(pcm.slice(i, i + 3200)).toString('base64')); await new Promise(r => setTimeout(r, 100)); }
              upload.complete();
            })().catch(reject);
          }
          if (event.type === 'interim') interimCount++;
          if (event.type === 'final') { finalCount++; transcript = event.transcript; }
        },
        error(error) { clearTimeout(timeout); reject(error); },
        complete() { clearTimeout(timeout); resolve(); },
      });
    });
    await done; await sending; assert.ok(transcript.length > 0, 'Google did not produce a final transcript');
    const resumed = await request(`/api/Exams/${examId}/attempt`, student); assert.equal(resumed.turn.transcript, transcript); checks++;
    state = await request(`/api/Exams/${examId}/attempt/answer`, student, { turnId, transcript });
    assert.ok(state.followUpsUsed <= 2); checks++;
    console.log(`PASS turn ${turn + 1}: Gemini question WAV + Live final transcript persisted + Gemini answer decision; state=${state.status}`);
  }
  assert.equal(state.status, 'Completed'); assert.equal(state.mainQuestionCount, 2);
  const persisted = await request(`/api/Exams/${examId}/attempt`, student); assert.equal(persisted.status, 'Completed');
  assert.ok(persisted.history.every(q => (q.followUps?.length ?? 0) <= 1));
  const report = { examId, checks, interimCount, finalCount, ttsBytes: audioBytes, status: state.status, followUpsUsed: state.followUpsUsed, liveProviders: true, audioSource: 'synthetic Vietnamese Gemini TTS fixture, not physical microphone', checkedAt: new Date().toISOString() };
  await writeFile('artifacts/gemini-e2e-result.json', JSON.stringify(report, null, 2));
  console.log(`PASS real Gemini E2E: ${checks} checks; ${interimCount} interim / ${finalCount} final events; ${audioBytes} TTS bytes; exam ${examId} completed. Report: artifacts/gemini-e2e-result.json`);
} finally { await hub.stop(); }
