import { useEffect, useState } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { BASE_URL } from '../api/client.js';
import { getSession } from '../auth/session.js';
import { watchSchedule } from '../lib/scheduleRealtime.js';

export const SCHEDULE_CHANGED = 'aives:schedule-changed';
export function useScheduleRealtime() {
  const [status, setStatus] = useState('Đang kết nối SignalR…');
  const token = getSession()?.token;
  useEffect(() => {
    if (!token) return;
    const refresh = event => window.dispatchEvent(new CustomEvent(SCHEDULE_CHANGED, { detail: event || {} }));
    const hub = new HubConnectionBuilder().withUrl(`${BASE_URL}/hubs/exam`, { accessTokenFactory: () => getSession()?.token || '' }).withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.None).build();
    return watchSchedule({ hub, refresh, status: setStatus, window, document });
  }, [token]);
  return status;
}
