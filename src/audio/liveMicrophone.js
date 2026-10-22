import { Subject, HubConnectionState } from '@microsoft/signalr';

export async function startLiveMicrophone({ hub, examId, turnId, signal, onEvent, onState, onError }) {
  if (hub?.state !== HubConnectionState.Connected) throw new Error('Microphone cần kết nối SignalR. Chờ nối lại rồi thử thu tiếp.');
  if (!navigator.mediaDevices?.getUserMedia || !window.AudioWorkletNode) throw new Error('Cần HTTPS/localhost và trình duyệt hỗ trợ AudioWorklet. Có thể nhập tay dự phòng.');
  const stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } });
  let context, source, node, gain, subscription, timeout;
  let closed = false, closing = false, inputFinished = false, sent = 0, acknowledged = 0;
  const upload = new Subject();
  const cleanup = () => {
    clearTimeout(timeout); node?.disconnect(); source?.disconnect(); gain?.disconnect();
    stream.getTracks().forEach(t => t.stop());
    context?.close().catch(() => {});
    signal?.removeEventListener('abort', cancel);
  };
  const complete = () => { if (closed) return; closed = true; cleanup(); onState('idle'); };
  const fail = error => {
    if (closed) return;
    closed = true; cleanup(); upload.complete(); subscription?.dispose();
    onState('idle'); onError(error.message || 'Gemini Live mất kết nối; transcript đã xác nhận được giữ.');
  };
  const cancel = () => { if (!closed) { closed = true; cleanup(); upload.complete(); subscription?.dispose(); onState('idle'); } };
  const finishInput = () => { if (inputFinished || closed) return; inputFinished = true; clearTimeout(timeout); cleanup(); upload.complete(); };
  const stop = () => {
    if (closed || closing) return;
    closing = true; onState('draining');
    if (node) {
      node.port.postMessage({ type: 'flush' });
      timeout = setTimeout(finishInput, 500);
    } else { cancel(); }
  };
  signal?.addEventListener('abort', cancel, { once: true });
  try {
    if (signal?.aborted) { cancel(); return { stop, cancel }; }
    context = new AudioContext({ sampleRate: 16000 });
    await context.resume();
    await context.audioWorklet.addModule(`${import.meta.env?.BASE_URL ?? '/'}audio/pcm-worklet.js`);
    if (closed || signal?.aborted) { cancel(); return { stop, cancel }; }
    source = context.createMediaStreamSource(stream);
    node = new AudioWorkletNode(context, 'aives-pcm16'); gain = context.createGain(); gain.gain.value = 0;
    node.onprocessorerror = () => fail(new Error('Bộ xử lý microphone bị lỗi. Hãy thu lại hoặc nhập tay.'));
    for (const track of stream.getAudioTracks()) track.addEventListener('ended', () => { if (!closed && !closing) fail(new Error('Microphone đã ngắt kết nối. Transcript đã xác nhận được giữ.')); }, { once: true });
    node.port.onmessage = event => {
      if (closed || inputFinished) return;
      if (event.data.type === 'flushed') { finishInput(); return; }
      if (event.data.type !== 'audio') return;
      if (sent - acknowledged >= 30) { fail(new Error('Mạng gửi audio bị nghẽn quá 3 giây. Đã dừng mic; chờ nối lại rồi thu tiếp phần còn thiếu.')); return; }
      const bytes = new Uint8Array(event.data.pcm);
      const encoded = btoa(String.fromCharCode(...bytes));
      sent++; upload.next(encoded);
    };
    onState('connecting');
    timeout = setTimeout(() => fail(new Error('Gemini Live chưa sẵn sàng sau 15 giây. Kiểm tra key, quota và mạng.')), 15000);
    subscription = hub.stream('Transcribe', Number(examId), turnId, upload).subscribe({
      next(event) {
        if (closed) return;
        if (event.type === 'ready') {
          clearTimeout(timeout); source.connect(node); node.connect(gain); gain.connect(context.destination); onState('recording');
        }
        if (event.type === 'audioAck') acknowledged = Number(event.text);
        onEvent(event);
      },
      error(error) { fail(error); },
      complete() { complete(); },
    });
    return { stop, cancel };
  } catch (error) { cancel(); throw error; }
}
