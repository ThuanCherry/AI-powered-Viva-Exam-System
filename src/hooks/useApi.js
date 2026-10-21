import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { SCHEDULE_CHANGED } from './useScheduleRealtime.js';

export function useApi(path) {
  const [version, setVersion] = useState(0);
  const [result, setResult] = useState({ path: null, version: -1, data: null, error: '' });
  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    api.get(path, { signal: controller.signal }).then(data => { if (!controller.signal.aborted) setResult({ path, version, data, error: '' }); })
      .catch(e => { if (e.name !== 'AbortError') setResult({ path, version, data: null, error: e.message }); });
    return () => controller.abort();
  }, [path, version]);
  const reload = useCallback(() => setVersion(v => v + 1), []);
  useEffect(() => {
    if (!path) return;
    window.addEventListener(SCHEDULE_CHANGED, reload);
    return () => window.removeEventListener(SCHEDULE_CHANGED, reload);
  }, [path, reload]);
  const loading = Boolean(path && (result.path !== path || result.version !== version));
  return { data: !path || result.path !== path ? null : result.data, error: loading || !path ? '' : result.error, loading, reload };
}
